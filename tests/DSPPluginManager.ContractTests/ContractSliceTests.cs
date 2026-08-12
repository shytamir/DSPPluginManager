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
                TestAssert.Equal(3, types.Length, "public contract type count");

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
                PropertyDefinition loggerProperty = pluginBase.Properties.Single();
                TestAssert.Equal("Logger", loggerProperty.Name, "base logger property");
                TestAssert.Equal(
                    "DSPPluginManager.Contracts.PluginLogger",
                    loggerProperty.PropertyType.FullName,
                    "base logger property type"
                );
                TestAssert.True(
                    loggerProperty.GetMethod.IsPublic &&
                    loggerProperty.SetMethod == null,
                    "The plugin logger handle must be public read-only."
                );
                TestAssert.Equal(
                    "get_Logger",
                    string.Join(",", pluginBase.Methods
                        .Where(method => method.IsPublic && !method.IsConstructor)
                        .Select(method => method.Name)),
                    "public base service methods"
                );

                TypeDefinition logger = types.Single(type =>
                    type.FullName == "DSPPluginManager.Contracts.PluginLogger"
                );
                TestAssert.True(logger.IsSealed, "Plugin logger must be sealed.");
                TestAssert.Equal("System.Object", logger.BaseType.FullName, "logger base");
                TestAssert.Equal(0, logger.Properties.Count(property =>
                    property.GetMethod != null && property.GetMethod.IsPublic
                ), "public logger properties");
                TestAssert.Equal(0, logger.Fields.Count(field => field.IsPublic),
                    "public logger fields");
                TestAssert.Equal(
                    "Error,Information,Warning",
                    string.Join(",", logger.Methods
                        .Where(method => method.IsPublic && !method.IsConstructor)
                        .Select(method => method.Name)
                        .OrderBy(name => name, StringComparer.Ordinal)),
                    "public logger methods"
                );
                foreach (MethodDefinition method in logger.Methods.Where(method =>
                    method.IsPublic && !method.IsConstructor
                ))
                {
                    TestAssert.Equal(1, method.Parameters.Count,
                        method.Name + " arity");
                    TestAssert.Equal("System.Object",
                        method.Parameters[0].ParameterType.FullName,
                        method.Name + " payload type");
                    TestAssert.Equal("System.Void", method.ReturnType.FullName,
                        method.Name + " return type");
                }
                TestAssert.True(
                    !logger.Methods.Any(method => method.IsPublic && method.IsConstructor),
                    "Plugins must not construct loggers or choose attribution."
                );
                foreach (string fieldName in new[] { "identifier", "displayName" })
                {
                    FieldDefinition field = logger.Fields.Single(candidate =>
                        candidate.Name == fieldName
                    );
                    TestAssert.True(field.IsPrivate && field.IsInitOnly,
                        fieldName + " attribution must be immutable.");
                }
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

                FieldDefinition retainedLogger = plugin.Fields.Single(field =>
                    field.Name == "logger"
                );
                TestAssert.Equal(
                    "DSPPluginManager.Contracts.PluginLogger",
                    retainedLogger.FieldType.FullName,
                    "retained logger type"
                );
                MethodDefinition capture = plugin.Methods.Single(method =>
                    method.Name == "CaptureLogger"
                );
                TestAssert.True(capture.Body.Instructions.Any(instruction =>
                    instruction.Operand == retainedLogger
                ), "Fixture did not retain the logger handle.");

                TypeDefinition helper = assembly.MainModule.GetType(
                    "DSPPluginManager.RM09Consumer.MirrorLoggingHelper"
                );
                TestAssert.True(helper != null, "Logging helper is missing.");
                MethodDefinition report = helper.Methods.Single(method =>
                    method.Name == "Report"
                );
                TestAssert.Equal(
                    "DSPPluginManager.Contracts.PluginLogger",
                    report.Parameters.Single().ParameterType.FullName,
                    "helper logger parameter"
                );
                TestAssert.Equal(
                    "Error,Information,Warning",
                    string.Join(",", report.Body.Instructions
                        .Select(instruction => instruction.Operand as MethodReference)
                        .Where(method => method != null &&
                            method.DeclaringType.FullName ==
                                "DSPPluginManager.Contracts.PluginLogger")
                        .Select(method => method.Name)
                        .Distinct()
                        .OrderBy(name => name, StringComparer.Ordinal)),
                    "fixture logging calls"
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
