using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DSPPluginManager.Configuration;

namespace DSPPluginManager.Tests
{
    internal static class RM28TypedConfigurationTests
    {
        internal static void Run(
            string unityHostPath,
            string contractPath
        )
        {
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.RM28.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(sandbox);
            try
            {
                Assembly unityHost = FindLoadedAssembly(unityHostPath);
                Assembly contract = FindLoadedAssembly(contractPath);
                Type serviceType = unityHost.GetType(
                    "DSPPluginManager.UnityHost.PluginConfigurationService",
                    true
                );
                Type configurationType = contract.GetType(
                    "DSPPluginManager.Contracts.PluginConfiguration",
                    true
                );
                Type shortcutType = contract.GetType(
                    "DSPPluginManager.Contracts.KeyboardShortcut",
                    true
                );
                Type keyCodeType = shortcutType.GetConstructors().Single()
                    .GetParameters()[0].ParameterType;

                VerifyBindingAndMutation(
                    sandbox,
                    serviceType,
                    configurationType,
                    shortcutType,
                    keyCodeType
                );
                VerifyMalformedValuesAndIsolation(
                    sandbox,
                    serviceType,
                    configurationType,
                    shortcutType,
                    keyCodeType
                );
                VerifyScalarCodecs(serviceType);
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static void VerifyBindingAndMutation(
            string sandbox,
            Type serviceType,
            Type configurationType,
            Type shortcutType,
            Type keyCodeType
        )
        {
            PluginConfigurationDocument document =
                PluginConfigurationDocument.Parse(
                    "[General]\n" +
                    "Enabled = TrUe\n" +
                    "Text = nav2;phase=3;seed=123=456\\nline2\\tΩ\n" +
                    "Shortcut = F9 + LeftShift\n" +
                    "Unclaimed = keep-me\n"
                );
            List<string> warnings = new List<string>();
            object service = CreateService(
                sandbox,
                serviceType,
                "Fixture.Plugin",
                document,
                warnings
            );
            object configuration = GetProperty(service, "Handle");

            MethodInfo boolBind = Bind(configurationType, typeof(bool));
            object enabled = boolBind.Invoke(configuration, new object[]
            {
                "General", "Enabled", false, "first description"
            });
            TestAssert.Equal(true, GetValue<bool>(enabled),
                "stored Boolean value");
            TestAssert.Equal(3, document.Count,
                "Boolean bind claim count");

            object repeated = boolBind.Invoke(configuration, new object[]
            {
                "General", "Enabled", true, "replacement description"
            });
            TestAssert.True(object.ReferenceEquals(enabled, repeated),
                "Repeated same-type bind did not return the authoritative entry.");
            TestAssert.Equal(3, document.Count,
                "Repeated bind claimed another definition.");

            MethodInfo stringBind = Bind(configurationType, typeof(string));
            AssertInvocationFailure<InvalidOperationException>(
                stringBind,
                configuration,
                new object[]
                {
                    "General", "Enabled", "wrong type", "conflict"
                },
                "fixture.plugin",
                "General",
                "Enabled",
                "Boolean",
                "String"
            );
            TestAssert.Equal(true, GetValue<bool>(enabled),
                "conflict changed authoritative entry");

            object text = stringBind.Invoke(configuration, new object[]
            {
                "General", "Text", "fallback", "text description"
            });
            TestAssert.Equal(
                "nav2;phase=3;seed=123=456\nline2\tΩ",
                GetValue<string>(text),
                "stored string value"
            );
            object empty = stringBind.Invoke(configuration, new object[]
            {
                "General", "Empty", string.Empty, string.Empty
            });
            TestAssert.Equal(string.Empty, GetValue<string>(empty),
                "empty string default");

            object f8 = CreateShortcut(shortcutType, keyCodeType, "F8");
            MethodInfo shortcutBind = Bind(configurationType, shortcutType);
            object shortcut = shortcutBind.Invoke(configuration, new[]
            {
                "General", "Shortcut", f8, "shortcut description"
            });
            TestAssert.Equal("F9 + LeftShift", GetRawValue(shortcut).ToString(),
                "stored shortcut value");
            TestAssert.Equal(1, document.Count,
                "unclaimed entry count");
            string unclaimed;
            TestAssert.True(
                document.TryGetSerializedValue(
                    "General",
                    "Unclaimed",
                    out unclaimed
                ),
                "Unrelated serialized entry was claimed."
            );
            TestAssert.Equal("keep-me", unclaimed,
                "unclaimed serialized value");

            int initialVersion = (int)GetProperty(service, "MutationVersion");
            SetRawValue(enabled, true);
            TestAssert.Equal(initialVersion,
                (int)GetProperty(service, "MutationVersion"),
                "equal assignment mutation version");
            SetRawValue(enabled, false);
            TestAssert.Equal(initialVersion + 1,
                (int)GetProperty(service, "MutationVersion"),
                "changed assignment mutation version");
            TestAssert.Equal(false, GetValue<bool>(enabled),
                "changed in-memory Boolean value");
            AssertPropertyFailure<ArgumentNullException>(text, null, "value");
            TestAssert.Equal(0, warnings.Count,
                "valid binding warnings");
        }

        private static void VerifyMalformedValuesAndIsolation(
            string sandbox,
            Type serviceType,
            Type configurationType,
            Type shortcutType,
            Type keyCodeType
        )
        {
            PluginConfigurationDocument brokenDocument =
                PluginConfigurationDocument.Parse(
                    "[General]\n" +
                    "Enabled = perhaps\n" +
                    "Text = bad\\escape\n" +
                    "Shortcut = f9\n"
                );
            List<string> brokenWarnings = new List<string>();
            object broken = CreateService(
                sandbox,
                serviceType,
                "Broken.Plugin",
                brokenDocument,
                brokenWarnings
            );
            object brokenConfig = GetProperty(broken, "Handle");
            object boolEntry = Bind(configurationType, typeof(bool)).Invoke(
                brokenConfig,
                new object[] { "General", "Enabled", true, "description" }
            );
            object stringEntry = Bind(configurationType, typeof(string)).Invoke(
                brokenConfig,
                new object[] { "General", "Text", "fallback", "description" }
            );
            object defaultShortcut = CreateShortcut(
                shortcutType,
                keyCodeType,
                "F8"
            );
            object shortcutEntry = Bind(configurationType, shortcutType).Invoke(
                brokenConfig,
                new[]
                {
                    "General", "Shortcut", defaultShortcut, "description"
                }
            );
            TestAssert.Equal(true, GetValue<bool>(boolEntry),
                "malformed Boolean default");
            TestAssert.Equal("fallback", GetValue<string>(stringEntry),
                "malformed string default");
            TestAssert.Equal("F8", GetRawValue(shortcutEntry).ToString(),
                "malformed shortcut default");
            TestAssert.Equal(3, brokenWarnings.Count,
                "malformed warning count");
            foreach (string warning in brokenWarnings)
            {
                foreach (string expected in new[]
                {
                    "broken.plugin", "General", "malformed", "default"
                })
                {
                    TestAssert.True(
                        warning.IndexOf(
                            expected,
                            StringComparison.OrdinalIgnoreCase
                        ) >= 0,
                        "Malformed warning omitted '" + expected + "': " +
                        warning
                    );
                }
            }

            PluginConfigurationDocument healthyDocument =
                PluginConfigurationDocument.Parse(
                    "[General]\nEnabled = false\n"
                );
            List<string> healthyWarnings = new List<string>();
            object healthy = CreateService(
                sandbox,
                serviceType,
                "Healthy.Plugin",
                healthyDocument,
                healthyWarnings
            );
            object healthyEntry = Bind(configurationType, typeof(bool)).Invoke(
                GetProperty(healthy, "Handle"),
                new object[] { "General", "Enabled", true, "description" }
            );
            TestAssert.Equal(false, GetValue<bool>(healthyEntry),
                "unrelated plugin stored value");
            TestAssert.Equal(0, healthyWarnings.Count,
                "unrelated plugin warnings");

            PluginConfigurationDocument throwingDocument =
                PluginConfigurationDocument.Parse(
                    "[General]\nEnabled = invalid\n"
                );
            object throwing = Activator.CreateInstance(
                serviceType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    PluginConfigurationScope.Create(
                        Path.Combine(sandbox, "throwing"),
                        "Throwing.Plugin"
                    ),
                    throwingDocument,
                    new Action<string>(message =>
                    {
                        throw new InvalidOperationException("sink failed");
                    })
                },
                null
            );
            object retained = Bind(configurationType, typeof(bool)).Invoke(
                GetProperty(throwing, "Handle"),
                new object[] { "General", "Enabled", true, "description" }
            );
            TestAssert.Equal(true, GetValue<bool>(retained),
                "warning-sink failure default");
        }

        private static void VerifyScalarCodecs(Type serviceType)
        {
            MethodInfo serializeBoolean = serviceType.GetMethod(
                "SerializeBoolean",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            MethodInfo serializeString = serviceType.GetMethod(
                "SerializeString",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            TestAssert.Equal("true",
                serializeBoolean.Invoke(null, new object[] { true }),
                "canonical true");
            TestAssert.Equal("false",
                serializeBoolean.Invoke(null, new object[] { false }),
                "canonical false");
            string value = " Ω;phase=3\\next\nline\rreturn\ttab\u0001 ";
            string encoded = (string)serializeString.Invoke(
                null,
                new object[] { value }
            );
            TestAssert.Equal(
                "\\u0020Ω;phase=3\\\\next\\nline\\rreturn\\ttab\\u0001\\u0020",
                encoded,
                "canonical string scalar"
            );
            MethodInfo parseString = serviceType.GetMethod(
                "TryParseString",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            object[] parseArguments = { encoded, null };
            TestAssert.Equal(
                true,
                (bool)parseString.Invoke(null, parseArguments),
                "canonical string reparse"
            );
            TestAssert.Equal(value, (string)parseArguments[1],
                "string scalar round-trip");
        }

        private static object CreateService(
            string sandbox,
            Type serviceType,
            string identifier,
            PluginConfigurationDocument document,
            List<string> warnings
        )
        {
            return Activator.CreateInstance(
                serviceType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    PluginConfigurationScope.Create(
                        Path.Combine(sandbox, identifier),
                        identifier
                    ),
                    document,
                    new Action<string>(warnings.Add)
                },
                null
            );
        }

        private static MethodInfo Bind(Type configurationType, Type valueType)
        {
            return configurationType.GetMethods().Single(method =>
                method.Name == "Bind" &&
                method.GetParameters()[2].ParameterType == valueType
            );
        }

        private static object CreateShortcut(
            Type shortcutType,
            Type keyCodeType,
            string keyName
        )
        {
            Array held = Array.CreateInstance(keyCodeType, 0);
            return shortcutType.GetConstructors().Single().Invoke(new object[]
            {
                Enum.Parse(keyCodeType, keyName, false),
                held
            });
        }

        private static object GetProperty(object target, string name)
        {
            return target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(target, null);
        }

        private static T GetValue<T>(object entry)
        {
            return (T)GetRawValue(entry);
        }

        private static object GetRawValue(object entry)
        {
            return entry.GetType().GetProperty("Value").GetValue(entry, null);
        }

        private static void SetRawValue(object entry, object value)
        {
            entry.GetType().GetProperty("Value").SetValue(entry, value, null);
        }

        private static void AssertPropertyFailure<TException>(
            object entry,
            object value,
            params string[] expectedMessageParts
        ) where TException : Exception
        {
            try
            {
                SetRawValue(entry, value);
                throw new InvalidOperationException(
                    "Expected " + typeof(TException).Name + "."
                );
            }
            catch (TargetInvocationException exception)
            {
                TestAssert.True(exception.InnerException is TException,
                    "Unexpected property failure type.");
                foreach (string part in expectedMessageParts)
                {
                    TestAssert.True(
                        exception.InnerException.Message.IndexOf(
                            part,
                            StringComparison.OrdinalIgnoreCase
                        ) >= 0,
                        "Property failure omitted '" + part + "'."
                    );
                }
            }
        }

        private static void AssertInvocationFailure<TException>(
            MethodInfo method,
            object target,
            object[] arguments,
            params string[] expectedMessageParts
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
                TestAssert.True(exception.InnerException is TException,
                    "Unexpected invocation failure type.");
                foreach (string part in expectedMessageParts)
                {
                    TestAssert.True(
                        exception.InnerException.Message.IndexOf(
                            part,
                            StringComparison.OrdinalIgnoreCase
                        ) >= 0,
                        "Invocation failure omitted '" + part + "': " +
                        exception.InnerException.Message
                    );
                }
            }
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
}
