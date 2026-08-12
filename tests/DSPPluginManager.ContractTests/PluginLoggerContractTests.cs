using System;
using System.Linq;
using System.Reflection;

namespace DSPPluginManager.ContractTests
{
    internal static class PluginLoggerContractTests
    {
        internal static void Run(string contractPath)
        {
            string fullPath = System.IO.Path.GetFullPath(contractPath);
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies()
                .Single(candidate =>
                    !candidate.IsDynamic &&
                    string.Equals(
                        candidate.Location,
                        fullPath,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            Type loggerType = assembly.GetType(
                "DSPPluginManager.Contracts.PluginLogger",
                true
            );
            ConstructorInfo constructor = loggerType.GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic
            ).Single(candidate => candidate.GetParameters().Length == 5);

            int informationCalls = 0;
            int warningCalls = 0;
            int errorCalls = 0;
            object logger = constructor.Invoke(new object[]
            {
                "com.shytamir.dspmirrorblueprint",
                "DSP Mirror Blueprint",
                new Action<object>(payload => informationCalls++),
                new Action<object>(payload => warningCalls++),
                new Action<object>(payload => errorCalls++)
            });

            Invoke(loggerType, logger, "Information", "information");
            Invoke(loggerType, logger, "Warning", "warning");
            Invoke(loggerType, logger, "Error", "error");
            TestAssert.Equal(1, informationCalls, "information dispatch count");
            TestAssert.Equal(1, warningCalls, "warning dispatch count");
            TestAssert.Equal(1, errorCalls, "error dispatch count");
            TestAssert.Equal(
                "com.shytamir.dspmirrorblueprint",
                ReadAttribution(loggerType, logger, "Identifier"),
                "logger identifier attribution"
            );
            TestAssert.Equal(
                "DSP Mirror Blueprint",
                ReadAttribution(loggerType, logger, "DisplayName"),
                "logger display-name attribution"
            );

            object failingLogger = constructor.Invoke(new object[]
            {
                "fixture.failure",
                "Failure Fixture",
                new Action<object>(payload => payload.ToString()),
                new Action<object>(payload => throw new InvalidOperationException(
                    "simulated sink failure"
                )),
                new Action<object>(payload => throw new ApplicationException(
                    "simulated dispatch failure"
                ))
            });
            Invoke(
                loggerType,
                failingLogger,
                "Information",
                new ThrowingPayload()
            );
            Invoke(loggerType, failingLogger, "Warning", "warning");
            Invoke(loggerType, failingLogger, "Error", "error");
        }

        private static void Invoke(
            Type loggerType,
            object logger,
            string methodName,
            object payload
        )
        {
            loggerType.GetMethod(methodName).Invoke(logger, new[] { payload });
        }

        private static object ReadAttribution(
            Type loggerType,
            object logger,
            string propertyName
        )
        {
            return loggerType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(logger, null);
        }

        private sealed class ThrowingPayload
        {
            public override string ToString()
            {
                throw new InvalidOperationException(
                    "simulated formatting failure"
                );
            }
        }
    }
}
