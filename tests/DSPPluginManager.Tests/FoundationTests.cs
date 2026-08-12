using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;

namespace DSPPluginManager.Tests
{
    internal static class FoundationTests
    {
        private const string ExpectedAssemblyName = "DSPPluginManager";
        private const string ExpectedFramework = ".NETFramework,Version=v4.7.2";
        private const string ExpectedMarkerType =
            "DSPPluginManager.ProductMarker";
        private const string ExpectedEntrypointType =
            "DSPPluginManager.Bootstrap.DoorstopEntrypoint";

        internal static void Run(string[] args)
        {
            if (args.Length != 10)
            {
                throw new InvalidOperationException(
                    "Expected product DLL, assembly version, release label, " +
                    "managed dependency directory, Unity host, facade, " +
                    "plugin contract, selected consumer fixture, and two " +
                    "activation-failure fixtures."
                );
            }

            ValidateFoundation(args[0], args[1], args[2]);
        }

        private static void ValidateFoundation(
            string assemblyPath,
            string expectedAssemblyVersion,
            string expectedReleaseLabel
        )
        {
            string fullPath = Path.GetFullPath(assemblyPath);
            TestAssert.True(
                File.Exists(fullPath) && new FileInfo(fullPath).Length > 0,
                "The compiled product assembly is missing or empty."
            );

            Assembly assembly = Assembly.LoadFile(fullPath);
            AssemblyName identity = assembly.GetName();
            TestAssert.Equal(
                ExpectedAssemblyName,
                identity.Name,
                "assembly name"
            );
            TestAssert.Equal(
                expectedAssemblyVersion,
                identity.Version.ToString(),
                "assembly version"
            );

            TargetFrameworkAttribute framework =
                (TargetFrameworkAttribute)Attribute.GetCustomAttribute(
                    assembly,
                    typeof(TargetFrameworkAttribute)
                );
            TestAssert.True(
                framework != null,
                "The target framework attribute is missing."
            );
            TestAssert.Equal(
                ExpectedFramework,
                framework.FrameworkName,
                "target framework"
            );

            AssemblyInformationalVersionAttribute informational =
                (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    assembly,
                    typeof(AssemblyInformationalVersionAttribute)
                );
            TestAssert.True(
                informational != null,
                "The informational version attribute is missing."
            );
            TestAssert.Equal(
                expectedReleaseLabel,
                informational.InformationalVersion,
                "informational version"
            );

            FileVersionInfo fileVersion = FileVersionInfo.GetVersionInfo(fullPath);
            TestAssert.Equal(
                expectedAssemblyVersion,
                fileVersion.FileVersion,
                "file version"
            );
            TestAssert.Equal(
                expectedReleaseLabel,
                fileVersion.ProductVersion,
                "product version"
            );

            Type marker = assembly.GetType(ExpectedMarkerType, false, false);
            TestAssert.True(
                marker != null && !marker.IsPublic,
                "The internal product marker is missing or unexpectedly public."
            );
            Type[] exported = assembly.GetExportedTypes();
            TestAssert.Equal(1, exported.Length, "exported type count");
            TestAssert.Equal(
                ExpectedEntrypointType,
                exported[0].FullName,
                "sole exported bootstrap type"
            );
            MethodInfo main = exported[0].GetMethod(
                "Main",
                BindingFlags.Public |
                    BindingFlags.Static |
                    BindingFlags.DeclaredOnly
            );
            TestAssert.True(
                main != null &&
                    main.ReturnType == typeof(void) &&
                    main.GetParameters().Length == 0,
                "The public parameterless Doorstop Main entrypoint is missing."
            );
        }
    }
}
