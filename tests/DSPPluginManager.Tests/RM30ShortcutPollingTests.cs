using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace DSPPluginManager.Tests
{
    internal static class RM30ShortcutPollingTests
    {
        internal static void Run(
            string coreFacadePath,
            string contractPath,
            string inputFacadePath
        )
        {
            VerifyConfiguredShortcutBeforeInstallation(
                coreFacadePath,
                contractPath
            );

            Assembly contract = FindLoadedAssembly(contractPath);
            Assembly inputFacade = FindLoadedAssembly(inputFacadePath);
            Type shortcutType = contract.GetType(
                "DSPPluginManager.Contracts.KeyboardShortcut",
                true
            );
            ConstructorInfo constructor = shortcutType.GetConstructors().Single();
            Type keyCodeType = constructor.GetParameters()[0].ParameterType;
            Type inputType = inputFacade.GetType("UnityEngine.Input", true);
            object f9 = Shortcut(
                constructor,
                keyCodeType,
                "F9"
            );
            object shiftedF9 = Shortcut(
                constructor,
                keyCodeType,
                "F9",
                "LeftShift"
            );
            MethodInfo isDown = shortcutType.GetMethod("IsDown");

            SetState(inputType, keyCodeType, new string[0], new string[0]);
            TestAssert.Equal(false, (bool)isDown.Invoke(f9, null),
                "no-edge result");
            AssertQueries(
                inputType,
                "GetQueryLog",
                "Down:F9"
            );
            TestAssert.Equal(0, QueryCount(inputType, "GetHeldQueries"),
                "ordinary-frame broad scan count");

            SetState(inputType, keyCodeType,
                new[] { "F9" }, new string[0]);
            TestAssert.Equal(false, (bool)isDown.Invoke(shiftedF9, null),
                "missing held-key result");
            string[] missingLog = Queries(inputType, "GetQueryLog");
            TestAssert.Equal("Down:F9", missingLog[0],
                "main edge query order");
            TestAssert.Equal("Held:LeftShift", missingLog[1],
                "held query order");

            SetState(inputType, keyCodeType,
                new[] { "F9" }, new[] { "LeftShift" });
            TestAssert.Equal(true, (bool)isDown.Invoke(shiftedF9, null),
                "exact shortcut result");
            TestAssert.True(
                Queries(inputType, "GetHeldQueries").All(query =>
                    !string.Equals(
                        query,
                        "Mouse0",
                        StringComparison.Ordinal
                    )
                ),
                "Exact matching scanned mouse keys."
            );

            SetState(inputType, keyCodeType,
                new[] { "F9" }, new[] { "LeftShift", "A" });
            TestAssert.Equal(false, (bool)isDown.Invoke(shiftedF9, null),
                "extra keyboard-key result");

            SetState(inputType, keyCodeType,
                new[] { "F9" }, new[] { "LeftShift", "Mouse0" });
            TestAssert.Equal(true, (bool)isDown.Invoke(shiftedF9, null),
                "mouse coexistence result");

            SetState(inputType, keyCodeType,
                new[] { "F9" }, new[] { "LeftShift" });
            TestAssert.Equal(true, (bool)isDown.Invoke(shiftedF9, null),
                "first observer result");
            TestAssert.Equal(true, (bool)isDown.Invoke(shiftedF9, null),
                "second observer result");
            TestAssert.Equal(2, QueryCount(inputType, "GetDownQueries"),
                "non-consuming observer edge count");

            SetState(inputType, keyCodeType,
                new[] { "F9" }, new[] { "LeftShift" });
            Exception backgroundFailure = null;
            Thread background = new Thread(() =>
            {
                try
                {
                    isDown.Invoke(shiftedF9, null);
                }
                catch (TargetInvocationException exception)
                {
                    backgroundFailure = exception.InnerException;
                }
            });
            background.Start();
            background.Join();
            TestAssert.True(backgroundFailure is InvalidOperationException,
                "Background shortcut polling was accepted.");
            TestAssert.Equal(0, QueryCount(inputType, "GetDownQueries"),
                "background polling queried input");

            SetState(inputType, keyCodeType,
                new[] { "F9" }, new[] { "LeftShift" });
            object unset = shortcutType.GetProperty("Unset")
                .GetValue(null, null);
            TestAssert.Equal(false, (bool)isDown.Invoke(unset, null),
                "unset shortcut result");
            TestAssert.Equal(0, QueryCount(inputType, "GetDownQueries"),
                "unset edge query count");
            TestAssert.Equal(0, QueryCount(inputType, "GetHeldQueries"),
                "unset held query count");
        }

        private static void VerifyConfiguredShortcutBeforeInstallation(
            string coreFacadePath,
            string contractPath
        )
        {
            AppDomain domain = AppDomain.CreateDomain(
                "DSPPluginManager.RM30.Uninstalled." +
                Guid.NewGuid().ToString("N")
            );
            try
            {
                PollingBeforeInstallProbe probe =
                    (PollingBeforeInstallProbe)domain.CreateInstanceFromAndUnwrap(
                        typeof(PollingBeforeInstallProbe).Assembly.Location,
                        typeof(PollingBeforeInstallProbe).FullName
                    );
                string failureType = probe.Poll(
                    coreFacadePath,
                    contractPath
                );
                TestAssert.Equal(
                    typeof(InvalidOperationException).FullName,
                    failureType,
                    "configured pre-install polling failure"
                );
            }
            finally
            {
                AppDomain.Unload(domain);
            }
        }

        private static object Shortcut(
            ConstructorInfo constructor,
            Type keyCodeType,
            string main,
            params string[] held
        )
        {
            Array heldKeys = Keys(keyCodeType, held);
            return constructor.Invoke(new object[]
            {
                Enum.Parse(keyCodeType, main),
                heldKeys
            });
        }

        private static void SetState(
            Type inputType,
            Type keyCodeType,
            string[] down,
            string[] held
        )
        {
            inputType.GetMethod("SetState").Invoke(null, new object[]
            {
                Keys(keyCodeType, down),
                Keys(keyCodeType, held)
            });
        }

        private static Array Keys(Type keyCodeType, string[] names)
        {
            Array keys = Array.CreateInstance(keyCodeType, names.Length);
            for (int index = 0; index < names.Length; index++)
            {
                keys.SetValue(Enum.Parse(keyCodeType, names[index]), index);
            }
            return keys;
        }

        private static int QueryCount(Type inputType, string method)
        {
            return ((Array)inputType.GetMethod(method).Invoke(null, null)).Length;
        }

        private static string[] Queries(Type inputType, string method)
        {
            Array values = (Array)inputType.GetMethod(method)
                .Invoke(null, null);
            return values.Cast<object>()
                .Select(value => value.ToString())
                .ToArray();
        }

        private static void AssertQueries(
            Type inputType,
            string method,
            params string[] expected
        )
        {
            string[] actual = Queries(inputType, method);
            TestAssert.Equal(
                string.Join(",", expected),
                string.Join(",", actual),
                method
            );
        }

        private static Assembly FindLoadedAssembly(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return AppDomain.CurrentDomain.GetAssemblies().Single(assembly =>
                !assembly.IsDynamic &&
                string.Equals(
                    Path.GetFullPath(assembly.Location),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }
    }

    public sealed class PollingBeforeInstallProbe : MarshalByRefObject
    {
        public string Poll(string coreFacadePath, string contractPath)
        {
            Assembly.LoadFrom(Path.GetFullPath(coreFacadePath));
            Assembly contract = Assembly.LoadFrom(Path.GetFullPath(contractPath));
            Type shortcutType = contract.GetType(
                "DSPPluginManager.Contracts.KeyboardShortcut",
                true
            );
            ConstructorInfo constructor = shortcutType.GetConstructors().Single();
            Type keyCodeType = constructor.GetParameters()[0].ParameterType;
            Array held = Array.CreateInstance(keyCodeType, 0);
            object shortcut = constructor.Invoke(new object[]
            {
                Enum.Parse(keyCodeType, "F9"),
                held
            });
            try
            {
                shortcutType.GetMethod("IsDown").Invoke(shortcut, null);
                return null;
            }
            catch (TargetInvocationException exception)
            {
                return exception.InnerException.GetType().FullName;
            }
        }
    }
}
