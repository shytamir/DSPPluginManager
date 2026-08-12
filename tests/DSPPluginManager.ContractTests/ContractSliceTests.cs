using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPPluginManager.Discovery;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
                TestAssert.Equal(6, types.Length, "public contract type count");

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
                TestAssert.Equal(
                    "Config,Logger,WritableRoot",
                    string.Join(",", pluginBase.Properties
                        .Select(property => property.Name)
                        .OrderBy(name => name, StringComparer.Ordinal)),
                    "base service properties"
                );
                PropertyDefinition loggerProperty = pluginBase.Properties.Single(
                    property => property.Name == "Logger"
                );
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
                PropertyDefinition writableRootProperty =
                    pluginBase.Properties.Single(property =>
                        property.Name == "WritableRoot"
                    );
                TestAssert.Equal(
                    "System.String",
                    writableRootProperty.PropertyType.FullName,
                    "writable-root property type"
                );
                TestAssert.True(
                    writableRootProperty.GetMethod.IsPublic &&
                    writableRootProperty.SetMethod == null,
                    "The plugin writable root must be public read-only."
                );
                PropertyDefinition configurationProperty =
                    pluginBase.Properties.Single(property =>
                        property.Name == "Config"
                    );
                TestAssert.Equal(
                    "DSPPluginManager.Contracts.PluginConfiguration",
                    configurationProperty.PropertyType.FullName,
                    "base configuration property type"
                );
                TestAssert.True(
                    configurationProperty.GetMethod.IsPublic &&
                    configurationProperty.SetMethod == null,
                    "The plugin configuration handle must be public read-only."
                );
                TestAssert.Equal(
                    "Activate,Deactivate,get_Config,get_Logger,get_WritableRoot",
                    string.Join(",", pluginBase.Methods
                        .Where(method => method.IsPublic && !method.IsConstructor)
                        .Select(method => method.Name)
                        .OrderBy(name => name, StringComparer.Ordinal)),
                    "public base contract methods"
                );
                foreach (string callbackName in new[]
                {
                    "Activate", "Deactivate"
                })
                {
                    MethodDefinition callback = pluginBase.Methods.Single(
                        method => method.Name == callbackName
                    );
                    TestAssert.True(
                        callback.IsPublic && callback.IsAbstract &&
                        callback.Parameters.Count == 0 &&
                        callback.ReturnType.FullName == "System.Void",
                        callbackName +
                            " must be a public abstract parameterless void callback."
                    );
                }

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
                ValidateConfigurationContract(assembly);
                TestAssert.True(
                    !assembly.MainModule.AssemblyReferences.Any(reference =>
                        reference.Name == "BepInEx"
                    ),
                    "The contract must not reference BepInEx."
                );
            }
        }

        private static void ValidateConfigurationContract(
            AssemblyDefinition assembly
        )
        {
            TypeDefinition configuration = assembly.MainModule.GetType(
                "DSPPluginManager.Contracts.PluginConfiguration"
            );
            TestAssert.True(
                configuration != null && configuration.IsPublic &&
                configuration.IsSealed,
                "PluginConfiguration must be public and sealed."
            );
            TestAssert.True(
                !configuration.Methods.Any(method =>
                    method.IsPublic && method.IsConstructor
                ),
                "Plugins must not construct configuration handles."
            );
            MethodDefinition[] binds = configuration.Methods.Where(method =>
                method.IsPublic && method.Name == "Bind"
            ).ToArray();
            TestAssert.Equal(3, binds.Length, "configuration Bind overloads");
            TestAssert.Equal(
                "DSPPluginManager.Contracts.KeyboardShortcut," +
                "System.Boolean,System.String",
                string.Join(",", binds
                    .Select(method => method.Parameters[2].ParameterType.FullName)
                    .OrderBy(name => name, StringComparer.Ordinal)),
                "configuration value domain"
            );
            foreach (MethodDefinition bind in binds)
            {
                TestAssert.Equal(
                    "System.String,System.String," +
                    bind.Parameters[2].ParameterType.FullName +
                    ",System.String",
                    string.Join(",", bind.Parameters.Select(parameter =>
                        parameter.ParameterType.FullName
                    )),
                    "Bind parameter contract"
                );
                GenericInstanceType returnType =
                    bind.ReturnType as GenericInstanceType;
                TestAssert.True(
                    returnType != null &&
                    returnType.ElementType.FullName ==
                        "DSPPluginManager.Contracts.PluginConfigurationEntry`1" &&
                    returnType.GenericArguments.Single().FullName ==
                        bind.Parameters[2].ParameterType.FullName,
                    "Bind must return the matching closed entry."
                );
            }
            TestAssert.Equal(
                "Bind,Bind,Bind,Save",
                string.Join(",", configuration.Methods
                    .Where(method => method.IsPublic && !method.IsConstructor)
                    .Select(method => method.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)),
                "configuration public methods"
            );
            MethodDefinition configurationConstructor =
                configuration.Methods.Single(method => method.IsConstructor);
            TestAssert.True(
                configurationConstructor.IsAssembly &&
                configurationConstructor.Parameters.Count == 4 &&
                configurationConstructor.Parameters.Take(3).All(parameter =>
                    parameter.ParameterType.FullName.StartsWith(
                        "System.Func`5<",
                        StringComparison.Ordinal
                    )
                ) && configurationConstructor.Parameters[3]
                    .ParameterType.FullName == "System.Action",
                "The host-only configuration delegation seam is invalid."
            );

            TypeDefinition entry = assembly.MainModule.GetType(
                "DSPPluginManager.Contracts.PluginConfigurationEntry`1"
            );
            TestAssert.True(
                entry != null && entry.IsPublic && entry.IsSealed,
                "PluginConfigurationEntry<T> must be public and sealed."
            );
            TestAssert.True(
                !entry.Methods.Any(method =>
                    method.IsPublic && method.IsConstructor
                ),
                "Plugins must not construct configuration entries."
            );
            PropertyDefinition value = entry.Properties.Single(property =>
                property.Name == "Value"
            );
            TestAssert.True(
                value.GetMethod.IsPublic && value.SetMethod.IsPublic,
                "Configuration entry Value must be publicly read/write."
            );
            TestAssert.Equal(
                "get_Value,set_Value",
                string.Join(",", entry.Methods
                    .Where(method => method.IsPublic && !method.IsConstructor)
                    .Select(method => method.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)),
                "entry public methods"
            );
            MethodDefinition entryConstructor = entry.Methods.Single(method =>
                method.IsConstructor
            );
            TestAssert.True(
                entryConstructor.IsAssembly &&
                entryConstructor.Parameters.Count == 2 &&
                entryConstructor.Parameters[0].ParameterType.FullName.StartsWith(
                    "System.Func`1<",
                    StringComparison.Ordinal
                ) && entryConstructor.Parameters[1]
                    .ParameterType.FullName.StartsWith(
                        "System.Action`1<",
                        StringComparison.Ordinal
                    ),
                "The host-only entry delegation seam is invalid."
            );

            TypeDefinition shortcut = assembly.MainModule.GetType(
                "DSPPluginManager.Contracts.KeyboardShortcut"
            );
            TestAssert.True(
                shortcut != null && shortcut.IsPublic && shortcut.IsSealed &&
                shortcut.IsValueType,
                "KeyboardShortcut must be a public readonly value type."
            );
            TestAssert.True(
                shortcut.CustomAttributes.Any(attribute =>
                    attribute.AttributeType.FullName ==
                        "System.Runtime.CompilerServices.IsReadOnlyAttribute"
                ),
                "KeyboardShortcut must be readonly."
            );
            TestAssert.True(
                shortcut.Interfaces.Any(implementation =>
                    implementation.InterfaceType.FullName ==
                        "System.IEquatable`1<DSPPluginManager.Contracts.KeyboardShortcut>"
                ),
                "KeyboardShortcut must implement value equality."
            );
            MethodDefinition shortcutConstructor = shortcut.Methods.Single(
                method => method.IsPublic && method.IsConstructor
            );
            TestAssert.Equal(
                "UnityEngine.KeyCode,UnityEngine.KeyCode[]",
                string.Join(",", shortcutConstructor.Parameters.Select(parameter =>
                    parameter.ParameterType.FullName
                )),
                "shortcut constructor parameters"
            );
            TestAssert.True(
                shortcutConstructor.Parameters[1].CustomAttributes.Any(attribute =>
                    attribute.AttributeType.FullName ==
                        "System.ParamArrayAttribute"
                ),
                "Shortcut held keys must be a params array."
            );
            PropertyDefinition unset = shortcut.Properties.Single(property =>
                property.Name == "Unset"
            );
            TestAssert.True(
                unset.GetMethod.IsPublic && unset.GetMethod.IsStatic &&
                unset.SetMethod == null,
                "Unset must be a public get-only static property."
            );
            TestAssert.Equal(
                "Equals,Equals,GetHashCode,IsDown,ToString,get_Unset," +
                "op_Equality,op_Inequality",
                string.Join(",", shortcut.Methods
                    .Where(method => method.IsPublic && !method.IsConstructor)
                    .Select(method => method.Name)
                    .OrderBy(name => name, StringComparer.Ordinal)),
                "shortcut public methods"
            );
            TestAssert.True(
                !assembly.MainModule.AssemblyReferences.Any(reference =>
                    reference.Name == "UnityEngine.InputLegacyModule"
                ),
                "The contract must not reference Unity input implementation."
            );
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
                foreach (string callbackName in new[]
                {
                    "Activate", "Deactivate"
                })
                {
                    MethodDefinition callback = plugin.Methods.Single(
                        method => method.Name == callbackName
                    );
                    TestAssert.True(
                        callback.IsPublic && callback.IsVirtual &&
                        !callback.IsAbstract && callback.Parameters.Count == 0 &&
                        callback.ReturnType.FullName == "System.Void",
                        "Fixture does not implement " + callbackName + "."
                    );
                }

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

                FieldDefinition retainedRoot = plugin.Fields.Single(field =>
                    field.Name == "writableRoot"
                );
                TestAssert.Equal(
                    "System.String",
                    retainedRoot.FieldType.FullName,
                    "retained writable-root type"
                );
                MethodDefinition captureRoot = plugin.Methods.Single(method =>
                    method.Name == "CaptureWritableRoot"
                );
                TestAssert.True(captureRoot.Body.Instructions.Any(instruction =>
                    instruction.Operand == retainedRoot
                ), "Fixture did not retain the writable root.");

                TypeDefinition outputHelper = assembly.MainModule.GetType(
                    "DSPPluginManager.RM09Consumer.MirrorOutputHelper"
                );
                TestAssert.True(outputHelper != null,
                    "Writable-root output helper is missing.");
                MethodDefinition writeSnapshot = outputHelper.Methods.Single(
                    method => method.Name == "WriteSnapshot"
                );
                TestAssert.Equal(
                    "System.String,System.String",
                    string.Join(",", writeSnapshot.Parameters.Select(parameter =>
                        parameter.ParameterType.FullName
                    )),
                    "output helper parameters"
                );

                ValidateConfigurationConsumerShapes(assembly);
            }
        }

        private static void ValidateConfigurationConsumerShapes(
            AssemblyDefinition assembly
        )
        {
            TypeDefinition mirror = assembly.MainModule.GetType(
                "DSPPluginManager.RM09Consumer.MirrorConfigurationShape"
            );
            TypeDefinition guide = assembly.MainModule.GetType(
                "DSPPluginManager.RM09Consumer.GuideConfigurationShape"
            );
            TestAssert.True(
                mirror != null && guide != null,
                "RM-24 consumer-shaped fixtures are missing."
            );

            MethodReference[] mirrorCalls = mirror.Methods
                .SelectMany(method => method.Body == null
                    ? Enumerable.Empty<Instruction>()
                    : method.Body.Instructions)
                .Select(instruction => instruction.Operand as MethodReference)
                .Where(method => method != null)
                .ToArray();
            TestAssert.Equal(
                3,
                mirrorCalls.Count(method =>
                    method.DeclaringType.FullName ==
                        "DSPPluginManager.Contracts.PluginConfiguration" &&
                    method.Name == "Bind"
                ),
                "Mirror-shaped fixed binds"
            );
            TestAssert.True(
                mirrorCalls.Any(method =>
                    method.DeclaringType.FullName ==
                        "DSPPluginManager.Contracts.KeyboardShortcut" &&
                    method.Name == "IsDown"
                ) && mirrorCalls.Any(method =>
                    method.Name == "ToString"
                ),
                "Mirror-shaped shortcut polling/display calls are missing."
            );

            MethodReference[] guideCalls = guide.Methods
                .SelectMany(method => method.Body == null
                    ? Enumerable.Empty<Instruction>()
                    : method.Body.Instructions)
                .Select(instruction => instruction.Operand as MethodReference)
                .Where(method => method != null)
                .ToArray();
            TestAssert.Equal(
                3,
                guideCalls.Count(method =>
                    method.DeclaringType.FullName ==
                        "DSPPluginManager.Contracts.PluginConfiguration" &&
                    method.Name == "Bind"
                ),
                "Guide-shaped fixed and late binds"
            );
            TestAssert.True(
                guideCalls.Any(method =>
                    method.DeclaringType.FullName ==
                        "DSPPluginManager.Contracts.PluginConfiguration" &&
                    method.Name == "Save"
                ),
                "Guide-shaped explicit save call is missing."
            );
            TestAssert.True(
                !assembly.MainModule.AssemblyReferences.Any(reference =>
                    reference.Name == "BepInEx"
                ),
                "RM-24 fixtures must not reference BepInEx."
            );
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
