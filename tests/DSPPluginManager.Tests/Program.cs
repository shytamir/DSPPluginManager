using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;

namespace DSPPluginManager.Tests
{
    internal static class Program
    {
        private const string ExpectedAssemblyName = "DSPPluginManager";
        private const string ExpectedFramework = ".NETFramework,Version=v4.7.2";
        private const string ExpectedMarkerType =
            "DSPPluginManager.ProductMarker";

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3)
                {
                    throw new InvalidOperationException(
                        "Expected product DLL, assembly version, and release label."
                    );
                }

                ValidateFoundation(args[0], args[1], args[2]);
                Console.WriteLine("Compiled foundation tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void ValidateFoundation(
            string assemblyPath,
            string expectedAssemblyVersion,
            string expectedReleaseLabel
        )
        {
            string fullPath = Path.GetFullPath(assemblyPath);
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
            {
                throw new InvalidOperationException(
                    "The compiled product assembly is missing or empty."
                );
            }

            Assembly assembly = Assembly.LoadFile(fullPath);
            AssemblyName identity = assembly.GetName();
            Equal(ExpectedAssemblyName, identity.Name, "assembly name");
            Equal(
                expectedAssemblyVersion,
                identity.Version.ToString(),
                "assembly version"
            );

            TargetFrameworkAttribute framework =
                (TargetFrameworkAttribute)Attribute.GetCustomAttribute(
                    assembly,
                    typeof(TargetFrameworkAttribute)
                );
            if (framework == null)
            {
                throw new InvalidOperationException(
                    "The target framework attribute is missing."
                );
            }
            Equal(ExpectedFramework, framework.FrameworkName, "target framework");

            AssemblyInformationalVersionAttribute informational =
                (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    assembly,
                    typeof(AssemblyInformationalVersionAttribute)
                );
            if (informational == null)
            {
                throw new InvalidOperationException(
                    "The informational version attribute is missing."
                );
            }
            Equal(
                expectedReleaseLabel,
                informational.InformationalVersion,
                "informational version"
            );

            FileVersionInfo fileVersion = FileVersionInfo.GetVersionInfo(fullPath);
            Equal(expectedAssemblyVersion, fileVersion.FileVersion, "file version");
            Equal(expectedReleaseLabel, fileVersion.ProductVersion, "product version");

            Type marker = assembly.GetType(ExpectedMarkerType, false, false);
            if (marker == null || marker.IsPublic)
            {
                throw new InvalidOperationException(
                    "The internal product marker is missing or unexpectedly public."
                );
            }
            if (assembly.GetExportedTypes().Length != 0)
            {
                throw new InvalidOperationException(
                    "RM-01 must not introduce a public plugin contract."
                );
            }
        }

        private static void Equal(string expected, string actual, string field)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    field + " mismatch: expected '" + expected +
                    "', found '" + actual + "'."
                );
            }
        }
    }
}
