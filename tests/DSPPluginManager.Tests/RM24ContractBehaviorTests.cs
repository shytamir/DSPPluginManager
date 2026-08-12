using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace DSPPluginManager.Tests
{
    internal static class RM24ContractBehaviorTests
    {
        internal static void Run(string contractPath)
        {
            Assembly contract = AppDomain.CurrentDomain.GetAssemblies()
                .Single(assembly =>
                    !assembly.IsDynamic &&
                    string.Equals(
                        Path.GetFullPath(assembly.Location),
                        Path.GetFullPath(contractPath),
                        StringComparison.OrdinalIgnoreCase
                    )
                );
            Type configuration = contract.GetType(
                "DSPPluginManager.Contracts.PluginConfiguration",
                true
            );
            object uninitialized = FormatterServices.GetUninitializedObject(
                configuration
            );
            MethodInfo stringBind = configuration.GetMethods()
                .Single(method =>
                    method.Name == "Bind" &&
                    method.GetParameters()[2].ParameterType == typeof(string)
                );

            AssertInvocationFailure<ArgumentException>(
                stringBind,
                uninitialized,
                new object[] { string.Empty, "Key", "value", "description" }
            );
            AssertInvocationFailure<ArgumentException>(
                stringBind,
                uninitialized,
                new object[] { "Section", " Key", "value", "description" }
            );
            AssertInvocationFailure<ArgumentNullException>(
                stringBind,
                uninitialized,
                new object[] { "Section", "Key", null, "description" }
            );
            AssertInvocationFailure<ArgumentNullException>(
                stringBind,
                uninitialized,
                new object[] { "Section", "Key", "value", null }
            );

            Type shortcut = contract.GetType(
                "DSPPluginManager.Contracts.KeyboardShortcut",
                true
            );
            Type keyCode = shortcut.GetConstructors().Single()
                .GetParameters()[0].ParameterType;
            object f9 = Enum.Parse(keyCode, "F9");
            Array noHeldKeys = Array.CreateInstance(keyCode, 0);
            object configured = Activator.CreateInstance(
                shortcut,
                new object[] { f9, noHeldKeys }
            );
            TestAssert.Equal("F9", configured.ToString(),
                "configured shortcut display");

            object unset = shortcut.GetProperty("Unset")
                .GetValue(null, null);
            TestAssert.Equal("Not set", unset.ToString(),
                "unset shortcut display");
            TestAssert.Equal(
                false,
                (bool)shortcut.GetMethod("IsDown").Invoke(unset, null),
                "unset shortcut polling"
            );
            TestAssert.Equal(
                true,
                (bool)shortcut.GetMethod(
                    "op_Equality",
                    BindingFlags.Public | BindingFlags.Static
                ).Invoke(null, new[] { configured, configured }),
                "shortcut value equality"
            );
        }

        private static void AssertInvocationFailure<TException>(
            MethodInfo method,
            object target,
            object[] arguments
        ) where TException : Exception
        {
            try
            {
                method.Invoke(target, arguments);
                throw new InvalidOperationException(
                    "Expected " + typeof(TException).Name + "."
                );
            }
            catch (TargetInvocationException exception)
            {
                TestAssert.True(
                    exception.InnerException is TException,
                    "Expected " + typeof(TException).Name +
                    "; found " + exception.InnerException.GetType().Name + "."
                );
            }
        }
    }
}
