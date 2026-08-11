using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPPluginManager.Discovery;
using Mono.Cecil;

namespace DSPPluginManager.ContractTests
{
    internal static class CandidateReconcilerTests
    {
        internal static void Run(
            string contractPath,
            string validFixturePath,
            string dependencyDirectory,
            string gameManagedDirectory
        )
        {
            PluginMetadataReader reader = new PluginMetadataReader(
                new PluginInspectionReferences(
                    Path.GetFullPath(contractPath),
                    Path.GetFullPath(dependencyDirectory),
                    Path.GetFullPath(gameManagedDirectory)
                )
            );
            VerifyCreationOrderAndCopyReconciliation(reader, validFixturePath);
            VerifyAmbiguityAndRejectionMatrix();
        }

        private static void VerifyCreationOrderAndCopyReconciliation(
            PluginMetadataReader reader,
            string validFixturePath
        )
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.ReconciliationTests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(root);
            try
            {
                CreateFixture(root, validFixturePath, false);
                CandidateReconciliationResult first = ReconcileFixture(
                    reader,
                    root,
                    false
                );

                Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                CreateFixture(root, validFixturePath, true);
                CandidateReconciliationResult second = ReconcileFixture(
                    reader,
                    root,
                    true
                );

                AssertSequence(
                    Serialize(first),
                    Serialize(second),
                    "creation-order reconciliation"
                );
                TestAssert.Equal(4, first.Entries.Count, "fixture entry count");
                TestAssert.Equal(1, Count(first, CandidateReconciliationState.Selected),
                    "selected count");
                TestAssert.Equal(2, Count(first, CandidateReconciliationState.Redundant),
                    "redundant count");
                TestAssert.Equal(1, Count(first, CandidateReconciliationState.Superseded),
                    "superseded count");
                TestAssert.Equal(
                    Path.Combine(root, "a.dll"),
                    first.SelectedCandidates[0].AssemblyPath,
                    "ordinal selected path"
                );
                TestAssert.Equal(3, first.Diagnostics.Count, "fixture diagnostics");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void CreateFixture(
            string root,
            string source,
            bool reverse
        )
        {
            string[] copies =
            {
                Path.Combine(root, "z.dll"),
                Path.Combine(root, "a.dll")
            };
            if (reverse)
            {
                Array.Reverse(copies);
            }
            foreach (string copy in copies)
            {
                File.Copy(source, copy);
            }

            string oldPath = Path.Combine(
                root,
                reverse ? "old-z.dll" : "old-a.dll"
            );
            using (AssemblyDefinition old = AssemblyDefinition.ReadAssembly(
                source,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            {
                TypeDefinition type = old.MainModule.GetType(
                    "DSPPluginManager.RM09Consumer.MirrorShapedPlugin"
                );
                CustomAttribute marker = type.CustomAttributes.Single(attribute =>
                    attribute.AttributeType.FullName ==
                        PluginContractRules.MetadataTypeName
                );
                marker.ConstructorArguments[2] = new CustomAttributeArgument(
                    old.MainModule.TypeSystem.String,
                    "1.0.0"
                );
                old.Write(oldPath);
            }
            File.Copy(
                oldPath,
                Path.Combine(root, reverse ? "old-a.dll" : "old-z.dll")
            );
        }

        private static CandidateReconciliationResult ReconcileFixture(
            PluginMetadataReader reader,
            string root,
            bool reverse
        )
        {
            CandidateEnumerationResult enumeration =
                new CandidateFileEnumerator(root).Enumerate();
            TestAssert.Equal(0, enumeration.Diagnostics.Count,
                "fixture enumeration diagnostics");
            IEnumerable<string> paths = reverse ?
                enumeration.CandidatePaths.Reverse() :
                enumeration.CandidatePaths;
            PluginInspectionResult[] inspections = paths
                .Select(reader.Inspect)
                .ToArray();
            TestAssert.True(
                inspections.All(result => result.IsRecognized),
                "A reconciliation fixture failed recognition."
            );
            return new CandidateReconciler().Reconcile(inspections);
        }

        private static void VerifyAmbiguityAndRejectionMatrix()
        {
            PluginInspectionResult[] inputs =
            {
                Recognized("beta", "3.0.0", "b.dll", "HASH-B", "Type", "Asm"),
                Recognized("BETA", "3.0.0", "a.dll", "HASH-A", "Type", "Asm"),
                Recognized("beta", "2.0.0", "old.dll", "HASH-OLD", "Type", "Asm"),
                Recognized("gamma", "1.0.0", "g1.dll", "HASH-G", "Type.One", "Asm"),
                Recognized("gamma", "1.0.0", "g2.dll", "HASH-G", "Type.Two", "Asm"),
                Recognized("delta", "1.0.0", "d1.dll", "HASH-D", "Type", "Asm.One"),
                Recognized("delta", "1.0.0", "d2.dll", "HASH-D", "Type", "Asm.Two"),
                Recognized("epsilon", "2.0.0", "e2.dll", "HASH-E2", "Type", "Asm"),
                Recognized("epsilon", "1.0.0", "e1a.dll", "HASH-E1A", "Type", "Asm"),
                Recognized("epsilon", "1.0.0", "e1b.dll", "HASH-E1B", "Type", "Asm"),
                PluginInspectionResult.Rejected(new PluginInspectionDiagnostic(
                    PluginInspectionDiagnosticCode.MultiplePluginTypes,
                    Path.GetFullPath(@"C:\plugins\multiple.dll"),
                    "Two eligible plugin types."
                ))
            };
            CandidateReconciler reconciler = new CandidateReconciler();
            CandidateReconciliationResult first = reconciler.Reconcile(inputs);
            CandidateReconciliationResult reversed = reconciler.Reconcile(
                inputs.Reverse()
            );
            CandidateReconciliationResult rotated = reconciler.Reconcile(
                inputs.Skip(3).Concat(inputs.Take(3))
            );

            AssertSequence(Serialize(first), Serialize(reversed),
                "reversed reconciliation");
            AssertSequence(Serialize(first), Serialize(rotated),
                "rotated reconciliation");
            TestAssert.Equal(0, first.SelectedCandidates.Count,
                "ambiguous fallback selection count");
            TestAssert.Equal(10, Count(first, CandidateReconciliationState.Ambiguous),
                "ambiguous entry count");
            TestAssert.Equal(1, Count(first, CandidateReconciliationState.Rejected),
                "rejected entry count");
            TestAssert.True(
                first.Entries.Where(entry =>
                    entry.Candidate != null &&
                    entry.Candidate.IdentifierComparisonKey == "BETA"
                ).All(entry =>
                    entry.State == CandidateReconciliationState.Ambiguous
                ),
                "An older beta candidate was retained as a fallback."
            );
            TestAssert.True(
                first.Diagnostics.Any(diagnostic =>
                    diagnostic.Code ==
                        CandidateReconciliationDiagnosticCode.InspectionRejected &&
                    diagnostic.Detail.Contains("MultiplePluginTypes")
                ),
                "Multiple eligible types were not retained as a rejection."
            );
        }

        private static PluginInspectionResult Recognized(
            string identifier,
            string version,
            string fileName,
            string hash,
            string typeName,
            string assemblyIdentity
        )
        {
            Version parsed;
            TestAssert.True(
                PluginContractRules.TryParseVersion(version, out parsed),
                "Synthetic version is invalid."
            );
            return PluginInspectionResult.Recognized(
                new RecognizedPluginCandidate(
                    identifier,
                    PluginContractRules.GetIdentifierComparisonKey(identifier),
                    identifier,
                    parsed,
                    assemblyIdentity,
                    Path.GetFullPath(Path.Combine(@"C:\plugins", fileName)),
                    typeName,
                    hash
                )
            );
        }

        private static int Count(
            CandidateReconciliationResult result,
            CandidateReconciliationState state
        )
        {
            return result.Entries.Count(entry => entry.State == state);
        }

        private static string[] Serialize(CandidateReconciliationResult result)
        {
            return result.Entries.Select(entry =>
            {
                string path = entry.Candidate == null ?
                    entry.InspectionDiagnostic.AssemblyPath :
                    entry.Candidate.AssemblyPath;
                string identity = entry.Candidate == null ?
                    "-" : entry.Candidate.IdentifierComparisonKey;
                string version = entry.Candidate == null ?
                    "-" : entry.Candidate.Version.ToString(3);
                string diagnostic = entry.Diagnostic == null ?
                    "-" : entry.Diagnostic.Code + ":" + entry.Diagnostic.Detail;
                return identity + "|" + version + "|" + path + "|" +
                    entry.State + "|" + diagnostic;
            }).ToArray();
        }

        private static void AssertSequence<T>(
            IEnumerable<T> expected,
            IEnumerable<T> actual,
            string field
        )
        {
            TestAssert.Equal(
                string.Join("\n", expected),
                string.Join("\n", actual),
                field
            );
        }
    }
}
