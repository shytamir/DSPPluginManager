using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPPluginManager.Discovery;
using Mono.Cecil;

namespace DSPPluginManager.ContractTests
{
    internal static class ContractSliceTests
    {
        internal static void Run(
            string contractPath,
            string fixturePath,
            string assemblyVersion
        )
        {
            string contract = Path.GetFullPath(contractPath);
            string fixture = Path.GetFullPath(fixturePath);
            TestAssert.True(File.Exists(contract), "Contract DLL is missing.");
            TestAssert.True(File.Exists(fixture), "Consumer fixture is missing.");

            ValidateRules();
            ValidateContract(contract, assemblyVersion);
            ValidateConsumer(fixture);

            HashSet<string> loaded = new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic)
                    .Select(assembly => assembly.Location)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase
            );
            TestAssert.True(
                !loaded.Contains(contract) && !loaded.Contains(fixture),
                "Metadata inspection executed a contract or consumer load."
            );
        }

        private static void ValidateRules()
        {
            foreach (string value in new[]
            {
                "com.shytamir.dspmirrorblueprint",
                "local.dsp.progressionstatusexporter",
                "A-1_b.c"
            })
            {
                TestAssert.True(
                    PluginContractRules.IsValidIdentifier(value),
                    "Valid identifier was rejected: " + value
                );
            }
            foreach (string value in new[]
            {
                null, "", "contains space", "contains/slash", "nonascii-\u00e9"
            })
            {
                TestAssert.True(
                    !PluginContractRules.IsValidIdentifier(value),
                    "Invalid identifier was accepted: " + value
                );
            }
            TestAssert.Equal(
                0,
                PluginContractRules.IdentifierComparer.Compare("Plugin.Id", "plugin.id"),
                "identifier comparer"
            );

            Version parsed;
            TestAssert.True(
                PluginContractRules.TryParseVersion("0.1.0", out parsed),
                "Canonical version was rejected."
            );
            TestAssert.Equal("0.1.0", parsed.ToString(3), "parsed version");
            foreach (string value in new[]
            {
                null, "", "1.0", "1.0.0.0", "01.0.0", "1.00.0",
                "1.0.-1", "1.0.0-preview", "2147483648.0.0"
            })
            {
                TestAssert.True(
                    !PluginContractRules.TryParseVersion(value, out parsed),
                    "Invalid version was accepted: " + value
                );
            }
        }

        private static void ValidateContract(string path, string assemblyVersion)
        {
            using (AssemblyDefinition assembly = Read(path))
            {
                TestAssert.Equal(
                    PluginContractRules.ContractAssemblyName,
                    assembly.Name.Name,
                    "contract assembly name"
                );
                TestAssert.Equal(
                    assemblyVersion,
                    assembly.Name.Version.ToString(),
                    "contract assembly version"
                );
                TestAssert.True(
                    string.IsNullOrEmpty(assembly.Name.Culture) &&
                    (assembly.Name.PublicKeyToken == null ||
                     assembly.Name.PublicKeyToken.Length == 0),
                    "The contract assembly must be neutral and unsigned."
                );
                AssertNet472(assembly);

                TypeDefinition[] types = assembly.MainModule.Types
                    .Where(type => type.IsPublic)
                    .ToArray();
                TestAssert.Equal(2, types.Length, "public contract type count");

                TypeDefinition marker = types.Single(type =>
                    type.FullName == PluginContractRules.MetadataTypeName
                );
                TestAssert.True(marker.IsSealed, "The marker must be sealed.");
                TestAssert.Equal("System.Attribute", marker.BaseType.FullName, "marker base");
                MethodDefinition constructor = marker.Methods.Single(method =>
                    method.IsPublic && method.IsConstructor
                );
                TestAssert.Equal(3, constructor.Parameters.Count, "marker arity");
                TestAssert.True(
                    constructor.Parameters.All(parameter =>
                        parameter.ParameterType.FullName == "System.String"
                    ),
                    "Marker constructor parameters must all be strings."
                );
                TestAssert.Equal(
                    "DisplayName,Identifier,Version",
                    string.Join(",", marker.Properties
                        .Where(property => property.GetMethod.IsPublic)
                        .Select(property => property.Name)
                        .OrderBy(name => name, StringComparer.Ordinal)),
                    "marker properties"
                );
                CustomAttribute usage = marker.CustomAttributes.Single(attribute =>
                    attribute.AttributeType.FullName ==
                        "System.AttributeUsageAttribute"
                );
                TestAssert.Equal(
                    (int)AttributeTargets.Class,
                    (int)usage.ConstructorArguments[0].Value,
                    "marker target"
                );
                TestAssert.True(
                    usage.Properties.Any(property =>
                        property.Name == "AllowMultiple" &&
                        object.Equals(property.Argument.Value, false)
                    ) && usage.Properties.Any(property =>
                        property.Name == "Inherited" &&
                        object.Equals(property.Argument.Value, false)
                    ),
                    "The marker must be single-use and non-inherited."
                );

                TypeDefinition pluginBase = types.Single(type =>
                    type.FullName == PluginContractRules.BaseTypeName
                );
                TestAssert.True(pluginBase.IsAbstract, "Plugin base must be abstract.");
                TestAssert.Equal(
                    "UnityEngine.MonoBehaviour",
                    pluginBase.BaseType.FullName,
                    "plugin base type"
                );
                TestAssert.Equal(
                    "UnityEngine.CoreModule",
                    pluginBase.BaseType.Scope.Name,
                    "Unity assembly identity"
                );
                TestAssert.Equal(0, pluginBase.Properties.Count, "base service properties");
                TestAssert.Equal(
                    0,
                    pluginBase.Methods.Count(method => !method.IsConstructor),
                    "base service methods"
                );
                TestAssert.True(
                    !assembly.MainModule.AssemblyReferences.Any(reference =>
                        reference.Name == "BepInEx"
                    ),
                    "The contract must not reference BepInEx."
                );
            }
        }

        private static void ValidateConsumer(string path)
        {
            using (AssemblyDefinition assembly = Read(path))
            {
                AssertNet472(assembly);
                TypeDefinition plugin = assembly.MainModule.GetType(
                    "DSPPluginManager.RM09Consumer.MirrorShapedPlugin"
                );
                TestAssert.True(plugin != null && !plugin.IsAbstract, "Fixture type missing.");
                TestAssert.Equal(
                    PluginContractRules.BaseTypeName,
                    plugin.BaseType.FullName,
                    "fixture base type"
                );
                CustomAttribute marker = plugin.CustomAttributes.Single(attribute =>
                    attribute.AttributeType.FullName == PluginContractRules.MetadataTypeName
                );
                object[] expected =
                {
                    "com.shytamir.dspmirrorblueprint", "DSP Mirror Blueprint", "1.2.3"
                };
                TestAssert.Equal(expected.Length, marker.ConstructorArguments.Count, "fixture marker arity");
                for (int index = 0; index < expected.Length; index++)
                {
                    TestAssert.Equal(
                        expected[index],
                        marker.ConstructorArguments[index].Value,
                        "fixture marker value " + index
                    );
                }
                TestAssert.True(
                    assembly.MainModule.AssemblyReferences.Any(reference =>
                        reference.Name == PluginContractRules.ContractAssemblyName
                    ) && !assembly.MainModule.AssemblyReferences.Any(reference =>
                        reference.Name == "BepInEx"
                    ),
                    "Fixture references the wrong contract."
                );
            }
        }

        private static AssemblyDefinition Read(string path)
        {
            return AssemblyDefinition.ReadAssembly(
                path,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            );
        }

        private static void AssertNet472(AssemblyDefinition assembly)
        {
            CustomAttribute framework = assembly.CustomAttributes.Single(attribute =>
                attribute.AttributeType.FullName ==
                    "System.Runtime.Versioning.TargetFrameworkAttribute"
            );
            TestAssert.Equal(
                ".NETFramework,Version=v4.7.2",
                framework.ConstructorArguments[0].Value,
                "target framework"
            );
        }
    }
}
