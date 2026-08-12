using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DSPPluginManager.Tests
{
    internal static class RM27ShortcutContractTests
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
            Type shortcutType = contract.GetType(
                "DSPPluginManager.Contracts.KeyboardShortcut",
                true
            );
            ConstructorInfo constructor = shortcutType.GetConstructors().Single();
            Type keyCodeType = constructor.GetParameters()[0].ParameterType;
            MethodInfo tryParse = shortcutType.GetMethod(
                "TryParse",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            MethodInfo serialize = shortcutType.GetMethod(
                "ToPersistedString",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            VerifyConstruction(shortcutType, keyCodeType, constructor, serialize);
            VerifyRoundTrips(shortcutType, tryParse, serialize);
            VerifyParseFailures(shortcutType, tryParse);
        }

        private static void VerifyConstruction(
            Type shortcutType,
            Type keyCodeType,
            ConstructorInfo constructor,
            MethodInfo serialize
        )
        {
            object f9 = Key(keyCodeType, "F9");
            object leftShift = Key(keyCodeType, "LeftShift");
            object rightShift = Key(keyCodeType, "RightShift");
            Array supplied = Keys(
                keyCodeType,
                leftShift,
                rightShift,
                leftShift,
                f9
            );
            object normalized = constructor.Invoke(new[] { f9, supplied });
            supplied.SetValue(Key(keyCodeType, "A"), 0);
            TestAssert.Equal(
                "F9 + RightShift + LeftShift",
                normalized.ToString(),
                "normalized shortcut display"
            );
            TestAssert.Equal(
                "F9 + RightShift + LeftShift",
                (string)serialize.Invoke(normalized, null),
                "normalized shortcut persistence"
            );

            object same = constructor.Invoke(new[]
            {
                f9,
                Keys(keyCodeType, rightShift, leftShift)
            });
            object equality = shortcutType.GetMethod(
                "op_Equality",
                BindingFlags.Public | BindingFlags.Static
            ).Invoke(null, new[] { normalized, same });
            TestAssert.Equal(true, (bool)equality,
                "normalized shortcut equality");
            TestAssert.Equal(normalized.GetHashCode(), same.GetHashCode(),
                "normalized shortcut hash");

            object unset = shortcutType.GetProperty("Unset").GetValue(null, null);
            object constructedUnset = constructor.Invoke(new[]
            {
                Key(keyCodeType, "None"),
                Keys(keyCodeType)
            });
            TestAssert.Equal(unset, constructedUnset,
                "constructed unset shortcut");
            TestAssert.Equal(string.Empty,
                (string)serialize.Invoke(unset, null),
                "unset persisted scalar");
            TestAssert.Equal("Not set", unset.ToString(),
                "unset display scalar");

            AssertConstructionFailure<ArgumentNullException>(
                constructor,
                f9,
                null
            );
            AssertConstructionFailure<ArgumentException>(
                constructor,
                Key(keyCodeType, "None"),
                Keys(keyCodeType, leftShift)
            );
            AssertConstructionFailure<ArgumentOutOfRangeException>(
                constructor,
                f9,
                Keys(keyCodeType, Key(keyCodeType, "None"))
            );
            AssertConstructionFailure<ArgumentOutOfRangeException>(
                constructor,
                Key(keyCodeType, "Mouse0"),
                Keys(keyCodeType)
            );
            AssertConstructionFailure<ArgumentOutOfRangeException>(
                constructor,
                f9,
                Keys(keyCodeType, Key(keyCodeType, "JoystickButton0"))
            );
            AssertConstructionFailure<ArgumentOutOfRangeException>(
                constructor,
                Enum.ToObject(keyCodeType, 9999),
                Keys(keyCodeType)
            );
        }

        private static void VerifyRoundTrips(
            Type shortcutType,
            MethodInfo tryParse,
            MethodInfo serialize
        )
        {
            AssertRoundTrip(shortcutType, tryParse, serialize, string.Empty,
                string.Empty, "Not set");
            AssertRoundTrip(shortcutType, tryParse, serialize, "F8",
                "F8", "F8");
            AssertRoundTrip(shortcutType, tryParse, serialize, " F9 ",
                "F9", "F9");
            AssertRoundTrip(
                shortcutType,
                tryParse,
                serialize,
                "F9+LeftShift+RightShift+LeftShift+F9",
                "F9 + RightShift + LeftShift",
                "F9 + RightShift + LeftShift"
            );
        }

        private static void VerifyParseFailures(
            Type shortcutType,
            MethodInfo tryParse
        )
        {
            foreach (string invalid in new[]
            {
                null,
                "f9",
                "289",
                "F9 +",
                "+ F9",
                "F9 ++ LeftShift",
                "F9\t+\tLeftShift",
                "F9, LeftShift",
                "F9; LeftShift",
                "F9|LeftShift",
                "UnknownKey",
                "None",
                "Mouse0",
                "F9 + Mouse0",
                "JoystickButton0",
                "None + LeftShift"
            })
            {
                object parsed;
                TestAssert.Equal(
                    false,
                    InvokeTryParse(shortcutType, tryParse, invalid, out parsed),
                    "invalid shortcut parse: " + (invalid ?? "<null>")
                );
                TestAssert.Equal(
                    shortcutType.GetProperty("Unset").GetValue(null, null),
                    parsed,
                    "failed shortcut parse output"
                );
            }
        }

        private static void AssertRoundTrip(
            Type shortcutType,
            MethodInfo tryParse,
            MethodInfo serialize,
            string input,
            string expectedPersisted,
            string expectedDisplay
        )
        {
            object parsed;
            TestAssert.Equal(true,
                InvokeTryParse(shortcutType, tryParse, input, out parsed),
                "shortcut parse: " + input);
            TestAssert.Equal(expectedPersisted,
                (string)serialize.Invoke(parsed, null),
                "shortcut persisted text: " + input);
            TestAssert.Equal(expectedDisplay, parsed.ToString(),
                "shortcut display text: " + input);

            object reparsed;
            TestAssert.Equal(
                true,
                InvokeTryParse(
                    shortcutType,
                    tryParse,
                    expectedPersisted,
                    out reparsed
                ),
                "canonical shortcut reparse: " + input
            );
            TestAssert.Equal(parsed, reparsed,
                "shortcut round-trip value: " + input);
        }

        private static bool InvokeTryParse(
            Type shortcutType,
            MethodInfo method,
            string value,
            out object shortcut
        )
        {
            object[] arguments =
            {
                value,
                shortcutType.GetProperty("Unset").GetValue(null, null)
            };
            bool result = (bool)method.Invoke(null, arguments);
            shortcut = arguments[1];
            return result;
        }

        private static object Key(Type keyCodeType, string name)
        {
            return Enum.Parse(keyCodeType, name, false);
        }

        private static Array Keys(Type keyCodeType, params object[] values)
        {
            Array result = Array.CreateInstance(keyCodeType, values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                result.SetValue(values[index], index);
            }
            return result;
        }

        private static void AssertConstructionFailure<TException>(
            ConstructorInfo constructor,
            object mainKey,
            Array heldKeys
        ) where TException : Exception
        {
            try
            {
                constructor.Invoke(new object[] { mainKey, heldKeys });
                throw new InvalidOperationException(
                    "Expected " + typeof(TException).Name + "."
                );
            }
            catch (TargetInvocationException exception)
            {
                TestAssert.True(
                    exception.InnerException is TException,
                    "Expected " + typeof(TException).Name + "; found " +
                    exception.InnerException.GetType().Name + "."
                );
            }
        }
    }
}
