using System;
using System.IO;
using System.Linq;
using DSPPluginManager.Discovery;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DSPPluginManager.ContractTests
{
    internal static class Milestone1Fixture
    {
        internal static string[] Create(
            string contractPath,
            string validFixturePath,
            string dependencyDirectory,
            string gameManagedDirectory,
            string fixtureRoot,
            string executionSentinelPath
        )
        {
            string root = Path.GetFullPath(fixtureRoot);
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            Directory.CreateDirectory(root);

            string selectedDirectory = CreateDirectory(root, "selected");
            string oldDirectory = CreateDirectory(root, "old");
            string ambiguousDirectory = CreateDirectory(root, "ambiguous");
            string invalidDirectory = CreateDirectory(root, "invalid");
            string rejectedDirectory = CreateDirectory(root, "rejected");

            string selected = Path.Combine(selectedDirectory, "a.dll");
            Mutate(validFixturePath, selected, assembly =>
                AddExecutionSentinel(assembly, executionSentinelPath)
            );
            File.Copy(selected, Path.Combine(selectedDirectory, "z.dll"));

            Mutate(selected, Path.Combine(oldDirectory, "old.dll"), assembly =>
                SetMetadata(assembly, null, null, "1.0.0")
            );
            Mutate(
                selected,
                Path.Combine(ambiguousDirectory, "a.dll"),
                assembly => SetMetadata(
                    assembly,
                    "com.example.ambiguous",
                    "Ambiguous A",
                    "2.0.0"
                )
            );
            Mutate(
                selected,
                Path.Combine(ambiguousDirectory, "b.dll"),
                assembly => SetMetadata(
                    assembly,
                    "com.example.ambiguous",
                    "Ambiguous B",
                    "2.0.0"
                )
            );
            Mutate(
                selected,
                Path.Combine(invalidDirectory, "invalid.dll"),
                assembly => SetMetadata(assembly, "invalid/id", null, null)
            );
            File.WriteAllBytes(
                Path.Combine(rejectedDirectory, "native.dll"),
                new byte[] { 1, 2, 3, 4 }
            );

            CandidateDiscoveryPlan plan = CandidateDiscoveryPlanner.Create(
                root,
                new PluginInspectionReferences(
                    Path.GetFullPath(contractPath),
                    Path.GetFullPath(dependencyDirectory),
                    Path.GetFullPath(gameManagedDirectory)
                )
            );
            TestAssert.Equal(7, plan.EnumeratedCandidateCount,
                "milestone fixture candidate count");
            TestAssert.Equal(0, plan.EnumerationDiagnostics.Count,
                "milestone fixture enumeration diagnostics");
            TestAssert.Equal(0, plan.RuntimeLoadedCandidateCount,
                "milestone fixture runtime-loaded count");
            TestAssert.True(
                !File.Exists(executionSentinelPath),
                "Fixture generation or discovery executed candidate code."
            );
            return plan.ReportLines.ToArray();
        }

        private static string CreateDirectory(string root, string name)
        {
            string path = Path.Combine(root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        private static void Mutate(
            string source,
            string destination,
            Action<AssemblyDefinition> mutation
        )
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
                Path.GetFullPath(source),
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            {
                mutation(assembly);
                assembly.Write(destination);
            }
        }

        private static void AddExecutionSentinel(
            AssemblyDefinition assembly,
            string sentinelPath
        )
        {
            TypeDefinition moduleType = assembly.MainModule.Types.Single(type =>
                type.Name == "<Module>"
            );
            MethodDefinition initializer = new MethodDefinition(
                ".cctor",
                MethodAttributes.Private |
                    MethodAttributes.Static |
                    MethodAttributes.SpecialName |
                    MethodAttributes.RTSpecialName,
                assembly.MainModule.TypeSystem.Void
            );
            MethodReference writeAllText = assembly.MainModule.ImportReference(
                typeof(File).GetMethod(
                    "WriteAllText",
                    new[] { typeof(string), typeof(string) }
                )
            );
            ILProcessor processor = initializer.Body.GetILProcessor();
            processor.Append(processor.Create(
                OpCodes.Ldstr,
                Path.GetFullPath(sentinelPath)
            ));
            processor.Append(processor.Create(
                OpCodes.Ldstr,
                "Candidate code executed."
            ));
            processor.Append(processor.Create(OpCodes.Call, writeAllText));
            processor.Append(processor.Create(OpCodes.Ret));
            moduleType.Methods.Add(initializer);
        }

        private static void SetMetadata(
            AssemblyDefinition assembly,
            string identifier,
            string displayName,
            string version
        )
        {
            TypeDefinition plugin = assembly.MainModule.GetType(
                "DSPPluginManager.RM09Consumer.MirrorShapedPlugin"
            );
            CustomAttribute marker = plugin.CustomAttributes.Single(attribute =>
                attribute.AttributeType.FullName ==
                    PluginContractRules.MetadataTypeName
            );
            string[] values = { identifier, displayName, version };
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] != null)
                {
                    marker.ConstructorArguments[index] =
                        new CustomAttributeArgument(
                            assembly.MainModule.TypeSystem.String,
                            values[index]
                        );
                }
            }
        }
    }
}
