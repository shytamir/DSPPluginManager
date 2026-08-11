using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPPluginManager.Discovery;
using Mono.Cecil;

namespace DSPPluginManager.ContractTests
{
    internal static class PluginMetadataReaderTests
    {
        internal static void Run(
            string contractPath,
            string validFixturePath,
            string dependencyDirectory,
            string gameManagedDirectory
        )
        {
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.ContractTests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(sandbox);
            List<string> candidatePaths = new List<string>();
            try
            {
                string valid = CopyFixture(
                    validFixturePath,
                    sandbox,
                    "valid.dll"
                );
                candidatePaths.Add(valid);
                PluginInspectionReferences references =
                    new PluginInspectionReferences(
                        Path.GetFullPath(contractPath),
                        Path.GetFullPath(dependencyDirectory),
                        Path.GetFullPath(gameManagedDirectory)
                    );
                PluginMetadataReader reader = new PluginMetadataReader(references);

                ValidateRecognized(reader.Inspect(valid), valid);
                ValidateRejected(
                    reader,
                    WriteBytes(sandbox, "native.dll", new byte[] { 1, 2, 3 }),
                    PluginInspectionDiagnosticCode.NonManagedFile,
                    candidatePaths
                );
                ValidateRejected(
                    reader,
                    WriteBytes(sandbox, "malformed.dll", new byte[] { 0x4D, 0x5A }),
                    PluginInspectionDiagnosticCode.MalformedAssembly,
                    candidatePaths
                );

                ValidateRejected(
                    reader,
                    Mutate(valid, sandbox, "no-plugin.dll", assembly =>
                        PluginType(assembly).CustomAttributes.Clear()
                    ),
                    PluginInspectionDiagnosticCode.NoPluginType,
                    candidatePaths
                );
                ValidateRejected(
                    reader,
                    Mutate(valid, sandbox, "wrong-base.dll", assembly =>
                        PluginType(assembly).BaseType =
                            assembly.MainModule.TypeSystem.Object
                    ),
                    PluginInspectionDiagnosticCode.UnsupportedPluginType,
                    candidatePaths
                );
                ValidateRejected(
                    reader,
                    Mutate(valid, sandbox, "abstract.dll", assembly =>
                        PluginType(assembly).Attributes |= TypeAttributes.Abstract
                    ),
                    PluginInspectionDiagnosticCode.AbstractPluginType,
                    candidatePaths
                );
                ValidateRejected(
                    reader,
                    Mutate(valid, sandbox, "invalid-metadata.dll", assembly =>
                        Marker(PluginType(assembly)).ConstructorArguments[0] =
                            new CustomAttributeArgument(
                                assembly.MainModule.TypeSystem.String,
                                "invalid/id"
                            )
                    ),
                    PluginInspectionDiagnosticCode.InvalidMetadata,
                    candidatePaths
                );
                ValidateRejected(
                    reader,
                    Mutate(valid, sandbox, "multiple.dll", AddSecondPluginType),
                    PluginInspectionDiagnosticCode.MultiplePluginTypes,
                    candidatePaths
                );

                string missing = Mutate(
                    valid,
                    sandbox,
                    "missing-reference.dll",
                    assembly => SetExternalBase(
                        assembly,
                        "Missing.PluginSupport",
                        "Missing",
                        "IntermediatePlugin"
                    )
                );
                candidatePaths.Add(missing);
                string unapproved = Path.Combine(sandbox, "unapproved");
                Directory.CreateDirectory(unapproved);
                WriteIntermediateAssembly(
                    valid,
                    Path.Combine(unapproved, "Missing.PluginSupport.dll"),
                    "Missing.PluginSupport",
                    "Missing",
                    "IntermediatePlugin"
                );
                ValidateRejected(
                    reader,
                    missing,
                    PluginInspectionDiagnosticCode.MissingReference,
                    null
                );

                string localDependency = Path.Combine(
                    sandbox,
                    "Local.PluginSupport.dll"
                );
                WriteIntermediateAssembly(
                    valid,
                    localDependency,
                    "Local.PluginSupport",
                    "Local",
                    "IntermediatePlugin"
                );
                string indirect = Mutate(
                    valid,
                    sandbox,
                    "indirect.dll",
                    assembly => SetExternalBase(
                        assembly,
                        "Local.PluginSupport",
                        "Local",
                        "IntermediatePlugin"
                    )
                );
                candidatePaths.Add(indirect);
                ValidateRecognized(reader.Inspect(indirect), indirect);

                HashSet<string> loaded = LoadedAssemblyLocations();
                foreach (string candidate in candidatePaths)
                {
                    TestAssert.True(
                        !loaded.Contains(Path.GetFullPath(candidate)),
                        "Static inspection loaded candidate code: " + candidate
                    );
                }
                TestAssert.True(
                    !loaded.Contains(Path.GetFullPath(localDependency)),
                    "Static resolution loaded a candidate dependency."
                );
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static void ValidateRecognized(
            PluginInspectionResult result,
            string path
        )
        {
            TestAssert.True(result.IsRecognized, "Valid plugin was rejected.");
            TestAssert.True(result.Diagnostic == null, "Valid plugin has a diagnostic.");
            RecognizedPluginCandidate candidate = result.Candidate;
            TestAssert.Equal(
                "com.shytamir.dspmirrorblueprint",
                candidate.Identifier,
                "recognized identifier"
            );
            TestAssert.Equal(
                "COM.SHYTAMIR.DSPMIRRORBLUEPRINT",
                candidate.IdentifierComparisonKey,
                "identifier comparison key"
            );
            TestAssert.Equal(
                "DSP Mirror Blueprint",
                candidate.DisplayName,
                "recognized display name"
            );
            TestAssert.Equal("1.2.3", candidate.Version.ToString(3), "version");
            TestAssert.Equal(
                Path.GetFullPath(path),
                candidate.AssemblyPath,
                "assembly path"
            );
            TestAssert.True(
                candidate.AssemblyIdentity.StartsWith(
                    "DSPPluginManager.RM09Consumer, Version=",
                    StringComparison.Ordinal
                ),
                "Assembly identity was not retained."
            );
            TestAssert.Equal(
                "DSPPluginManager.RM09Consumer.MirrorShapedPlugin",
                candidate.TypeName,
                "plugin type name"
            );
        }

        private static void ValidateRejected(
            PluginMetadataReader reader,
            string path,
            PluginInspectionDiagnosticCode expected,
            List<string> candidatePaths
        )
        {
            if (candidatePaths != null)
            {
                candidatePaths.Add(path);
            }
            PluginInspectionResult result = reader.Inspect(path);
            TestAssert.True(!result.IsRecognized, "Invalid fixture was recognized.");
            TestAssert.True(result.Candidate == null, "Rejected fixture has a candidate.");
            TestAssert.Equal(expected, result.Diagnostic.Code, "diagnostic code");
            TestAssert.Equal(
                Path.GetFullPath(path),
                result.Diagnostic.AssemblyPath,
                "diagnostic path"
            );
            TestAssert.True(
                !string.IsNullOrWhiteSpace(result.Diagnostic.Detail),
                "Diagnostic detail is missing."
            );
        }

        private static string CopyFixture(
            string source,
            string directory,
            string name
        )
        {
            string destination = Path.Combine(directory, name);
            File.Copy(Path.GetFullPath(source), destination);
            return destination;
        }

        private static string WriteBytes(
            string directory,
            string name,
            byte[] content
        )
        {
            string path = Path.Combine(directory, name);
            File.WriteAllBytes(path, content);
            return path;
        }

        private static string Mutate(
            string source,
            string directory,
            string name,
            Action<AssemblyDefinition> mutation
        )
        {
            string path = Path.Combine(directory, name);
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
                source,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            {
                mutation(assembly);
                assembly.Write(path);
            }
            return path;
        }

        private static TypeDefinition PluginType(AssemblyDefinition assembly)
        {
            return assembly.MainModule.GetType(
                "DSPPluginManager.RM09Consumer.MirrorShapedPlugin"
            );
        }

        private static CustomAttribute Marker(TypeDefinition type)
        {
            return type.CustomAttributes.Single(attribute =>
                attribute.AttributeType.FullName ==
                    PluginContractRules.MetadataTypeName
            );
        }

        private static void AddSecondPluginType(AssemblyDefinition assembly)
        {
            TypeDefinition original = PluginType(assembly);
            TypeDefinition second = new TypeDefinition(
                "DSPPluginManager.RM09Consumer",
                "SecondPlugin",
                TypeAttributes.Public | TypeAttributes.Class,
                original.BaseType
            );
            CustomAttribute originalMarker = Marker(original);
            CustomAttribute marker = new CustomAttribute(
                originalMarker.Constructor
            );
            foreach (CustomAttributeArgument argument in
                originalMarker.ConstructorArguments)
            {
                marker.ConstructorArguments.Add(argument);
            }
            second.CustomAttributes.Add(marker);
            assembly.MainModule.Types.Add(second);
        }

        private static void SetExternalBase(
            AssemblyDefinition assembly,
            string assemblyName,
            string typeNamespace,
            string typeName
        )
        {
            AssemblyNameReference reference = new AssemblyNameReference(
                assemblyName,
                new Version(1, 0, 0, 0)
            );
            assembly.MainModule.AssemblyReferences.Add(reference);
            PluginType(assembly).BaseType = new TypeReference(
                typeNamespace,
                typeName,
                assembly.MainModule,
                reference
            );
        }

        private static void WriteIntermediateAssembly(
            string validFixture,
            string path,
            string assemblyName,
            string typeNamespace,
            string typeName
        )
        {
            TypeReference contractBase;
            using (AssemblyDefinition valid = AssemblyDefinition.ReadAssembly(
                validFixture,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            {
                contractBase = PluginType(valid).BaseType;
                AssemblyDefinition dependency = AssemblyDefinition.CreateAssembly(
                    new AssemblyNameDefinition(
                        assemblyName,
                        new Version(1, 0, 0, 0)
                    ),
                    assemblyName,
                    ModuleKind.Dll
                );
                using (dependency)
                {
                    TypeReference importedBase =
                        dependency.MainModule.ImportReference(contractBase);
                    TypeDefinition intermediate = new TypeDefinition(
                        typeNamespace,
                        typeName,
                        TypeAttributes.Public | TypeAttributes.Abstract |
                            TypeAttributes.Class,
                        importedBase
                    );
                    dependency.MainModule.Types.Add(intermediate);
                    dependency.Write(path);
                }
            }
        }

        private static HashSet<string> LoadedAssemblyLocations()
        {
            return new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic)
                    .Select(assembly => assembly.Location)
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase
            );
        }
    }
}
