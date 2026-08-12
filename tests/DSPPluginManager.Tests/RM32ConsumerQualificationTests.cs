using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DSPPluginManager.Configuration;

namespace DSPPluginManager.Tests
{
    internal static class RM32ConsumerQualificationTests
    {
        internal static void Run(
            string unityHostPath,
            string contractPath,
            string inputFacadePath,
            string mirrorFixturePath,
            string guideFixturePath
        )
        {
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.RM32.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(sandbox);
            try
            {
                Assembly unityHost = FindLoadedAssembly(unityHostPath);
                Assembly contract = FindLoadedAssembly(contractPath);
                Assembly input = FindLoadedAssembly(inputFacadePath);
                Assembly mirror = Assembly.LoadFrom(
                    Path.GetFullPath(mirrorFixturePath)
                );
                Assembly guide = Assembly.LoadFrom(
                    Path.GetFullPath(guideFixturePath)
                );
                Type serviceType = unityHost.GetType(
                    "DSPPluginManager.UnityHost.PluginConfigurationService",
                    true
                );
                Type shortcutType = contract.GetType(
                    "DSPPluginManager.Contracts.KeyboardShortcut",
                    true
                );
                Type keyCodeType = shortcutType.GetConstructors().Single()
                    .GetParameters()[0].ParameterType;

                VerifyReferenceBoundaries(mirror, guide);
                VerifyConsumerPatterns(
                    sandbox,
                    serviceType,
                    shortcutType,
                    keyCodeType,
                    input,
                    mirror,
                    guide
                );
                VerifyFailureIsolation(
                    sandbox,
                    serviceType,
                    mirror,
                    guide
                );
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static void VerifyReferenceBoundaries(
            Assembly mirror,
            Assembly guide
        )
        {
            string[] mirrorReferences = mirror.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            string[] guideReferences = guide.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();

            TestAssert.True(
                mirrorReferences.Contains("DSPPluginManager.Contracts") &&
                mirrorReferences.Contains("UnityEngine.CoreModule") &&
                mirrorReferences.Contains("0Harmony"),
                "The Mirror-shaped fixture is missing a required reference."
            );
            TestAssert.True(
                guideReferences.Contains("DSPPluginManager.Contracts") &&
                guideReferences.Contains("UnityEngine.CoreModule") &&
                !guideReferences.Contains("0Harmony"),
                "The Guide-shaped fixture reference boundary is incorrect."
            );
            foreach (string reference in mirrorReferences.Concat(
                guideReferences
            ))
            {
                TestAssert.True(
                    !reference.StartsWith("BepInEx", StringComparison.Ordinal) &&
                    !reference.StartsWith("MonoMod", StringComparison.Ordinal) &&
                    !string.Equals(
                        reference,
                        "Mono.Cecil",
                        StringComparison.Ordinal
                    ),
                    "An RM-32 fixture contains a forbidden reference: " +
                    reference
                );
            }
        }

        private static void VerifyConsumerPatterns(
            string sandbox,
            Type serviceType,
            Type shortcutType,
            Type keyCodeType,
            Assembly input,
            Assembly mirrorAssembly,
            Assembly guideAssembly
        )
        {
            string directory = Path.Combine(sandbox, "configuration");
            Directory.CreateDirectory(directory);
            string mirrorPath = Path.Combine(directory, "mirror.plugin.cfg");
            string guidePath = Path.Combine(directory, "guide.plugin.cfg");
            File.WriteAllText(
                mirrorPath,
                "[Diagnostics]\nEnabled = true\nVerbose = invalid\n" +
                "Shortcut = F9 + LeftShift\n"
            );
            File.WriteAllText(
                guidePath,
                "[General]\nShow Panel = false\nToggle Shortcut = \n" +
                "[Phase Selection]\nCurrent = current\nLegacy = legacy\n"
            );

            List<string> mirrorWarnings = new List<string>();
            object mirror = CreateConsumer(
                mirrorAssembly,
                "DSPPluginManager.RM32MirrorQualification." +
                    "MirrorConfigurationFixture",
                OpenConfiguration(
                    serviceType,
                    directory,
                    "Mirror.Plugin",
                    mirrorWarnings
                )
            );
            TestAssert.Equal(true, Property<bool>(mirror, "Enabled"),
                "Mirror stored Boolean");
            TestAssert.Equal(false, Property<bool>(mirror, "Verbose"),
                "Mirror malformed Boolean default");
            TestAssert.Equal("F9 + LeftShift", Invoke<string>(
                mirror,
                "DisplayShortcut"
            ), "Mirror stored shortcut display");
            TestAssert.True(
                mirrorWarnings.Any(message =>
                    message.Contains("mirror.plugin") &&
                    message.Contains("Verbose") &&
                    message.Contains("malformed")
                ),
                "The Mirror malformed value did not retain an identified diagnostic."
            );
            SetInput(input, keyCodeType,
                new[] { "F9" }, new[] { "LeftShift" });
            TestAssert.Equal(true, Invoke<bool>(mirror, "IsShortcutDown"),
                "Mirror configured shortcut poll");
            Invoke(mirror, "SetVerbose", true);

            List<string> guideWarnings = new List<string>();
            object guide = CreateConsumer(
                guideAssembly,
                "DSPPluginManager.RM32GuideQualification." +
                    "GuideConfigurationFixture",
                OpenConfiguration(
                    serviceType,
                    directory,
                    "Guide.Plugin",
                    guideWarnings
                )
            );
            TestAssert.Equal(false, Property<bool>(guide, "ShowPanel"),
                "Guide stored Boolean");
            TestAssert.Equal("Not set", Invoke<string>(
                guide,
                "DisplayShortcut"
            ), "Guide unset shortcut display");
            SetInput(input, keyCodeType, new[] { "F8" }, new string[0]);
            TestAssert.Equal(false, Invoke<bool>(guide, "IsShortcutDown"),
                "Guide unset shortcut poll");

            string earlySave = File.ReadAllText(guidePath);
            AssertContains(earlySave, "Current = current");
            AssertContains(earlySave, "Legacy = legacy");
            Invoke(guide, "SelectSave", "Current", "next phase");
            Invoke(
                guide,
                "SetShortcut",
                CreateShortcut(shortcutType, keyCodeType, "F8")
            );
            string lateSave = File.ReadAllText(guidePath);
            AssertContains(lateSave, "Current = next phase");
            AssertContains(lateSave, "Legacy = legacy");

            mirror = CreateConsumer(
                mirrorAssembly,
                "DSPPluginManager.RM32MirrorQualification." +
                    "MirrorConfigurationFixture",
                OpenConfiguration(
                    serviceType,
                    directory,
                    "Mirror.Plugin",
                    new List<string>()
                )
            );
            guide = CreateConsumer(
                guideAssembly,
                "DSPPluginManager.RM32GuideQualification." +
                    "GuideConfigurationFixture",
                OpenConfiguration(
                    serviceType,
                    directory,
                    "Guide.Plugin",
                    new List<string>()
                )
            );
            TestAssert.Equal(true, Property<bool>(mirror, "Verbose"),
                "Mirror Boolean round-trip");
            TestAssert.Equal("F9 + LeftShift", Invoke<string>(
                mirror,
                "DisplayShortcut"
            ), "Mirror multi-key/F9 round-trip");
            TestAssert.Equal("F8", Invoke<string>(guide, "DisplayShortcut"),
                "Guide F8 round-trip");
            Invoke(guide, "SelectSave", "Current", "next phase");
            TestAssert.Equal("next phase", Property<string>(
                guide,
                "Selection"
            ), "Guide string round-trip");

            string mirrorContents = File.ReadAllText(mirrorPath);
            string guideContents = File.ReadAllText(guidePath);
            TestAssert.True(
                !mirrorContents.Contains("Show Panel") &&
                !mirrorContents.Contains("Phase Selection") &&
                !guideContents.Contains("Diagnostics"),
                "Consumer configuration files or entries collided."
            );
        }

        private static void VerifyFailureIsolation(
            string sandbox,
            Type serviceType,
            Assembly mirrorAssembly,
            Assembly guideAssembly
        )
        {
            string directory = Path.Combine(sandbox, "failure-isolation");
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(
                directory,
                "guide.failed.cfg"
            ));
            List<string> warnings = new List<string>();
            object failedGuide = CreateConsumer(
                guideAssembly,
                "DSPPluginManager.RM32GuideQualification." +
                    "GuideConfigurationFixture",
                OpenConfiguration(
                    serviceType,
                    directory,
                    "Guide.Failed",
                    warnings
                )
            );
            TestAssert.Equal(true, Property<bool>(failedGuide, "ShowPanel"),
                "failed Guide default");
            TestAssert.Equal("F8", Invoke<string>(
                failedGuide,
                "DisplayShortcut"
            ), "failed Guide shortcut default");
            TestAssert.True(
                warnings.Any(message =>
                    message.Contains("guide.failed") &&
                    message.Contains("writes are blocked")
                ),
                "The unavailable Guide source did not retain an identified diagnostic."
            );

            object healthyMirror = CreateConsumer(
                mirrorAssembly,
                "DSPPluginManager.RM32MirrorQualification." +
                    "MirrorConfigurationFixture",
                OpenConfiguration(
                    serviceType,
                    directory,
                    "Mirror.Healthy",
                    new List<string>()
                )
            );
            TestAssert.Equal(false, Property<bool>(healthyMirror, "Enabled"),
                "healthy Mirror default after Guide failure");
            TestAssert.Equal("F9", Invoke<string>(
                healthyMirror,
                "DisplayShortcut"
            ), "healthy Mirror after Guide failure");
        }

        private static object OpenConfiguration(
            Type serviceType,
            string directory,
            string identifier,
            List<string> warnings
        )
        {
            PluginConfigurationScope scope = PluginConfigurationScope.Create(
                directory,
                identifier
            );
            PluginConfigurationDocument document =
                PluginConfigurationDocument.Parse(
                    scope.IsUsable ? scope.Contents : string.Empty
                );
            object service = Activator.CreateInstance(
                serviceType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new object[]
                {
                    scope,
                    document,
                    new Action<string>(warnings.Add)
                },
                null
            );
            return serviceType.GetProperty(
                "Handle",
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(service, null);
        }

        private static object CreateConsumer(
            Assembly assembly,
            string typeName,
            object configuration
        )
        {
            return Activator.CreateInstance(
                assembly.GetType(typeName, true),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { configuration },
                null
            );
        }

        private static object CreateShortcut(
            Type shortcutType,
            Type keyCodeType,
            string main,
            params string[] held
        )
        {
            Array heldKeys = Keys(keyCodeType, held);
            return shortcutType.GetConstructors().Single().Invoke(new object[]
            {
                Enum.Parse(keyCodeType, main),
                heldKeys
            });
        }

        private static void SetInput(
            Assembly input,
            Type keyCodeType,
            string[] down,
            string[] held
        )
        {
            input.GetType("UnityEngine.Input", true).GetMethod("SetState")
                .Invoke(null, new object[]
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

        private static T Property<T>(object target, string name)
        {
            return (T)target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(target, null);
        }

        private static T Invoke<T>(object target, string name)
        {
            return (T)Invoke(target, name, new object[0]);
        }

        private static object Invoke(
            object target,
            string name,
            params object[] arguments
        )
        {
            return target.GetType().GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            ).Invoke(target, arguments);
        }

        private static void AssertContains(string actual, string expected)
        {
            TestAssert.True(
                actual.Contains(expected),
                "Expected text '" + expected + "' was absent from: " + actual
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
}
