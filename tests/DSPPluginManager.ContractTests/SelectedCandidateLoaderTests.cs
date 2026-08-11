using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DSPPluginManager.Discovery;
using DSPPluginManager.Loading;
using Mono.Cecil;

namespace DSPPluginManager.ContractTests
{
    internal static class SelectedCandidateLoaderTests
    {
        private const string FixtureTypeName =
            "DSPPluginManager.RM09Consumer.MirrorShapedPlugin";

        internal static void Run(
            string contractPath,
            string fixturePath,
            string dependencyDirectory,
            string gameManagedDirectory
        )
        {
            string root = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "rm14-runtime-fixtures"
            );
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            Directory.CreateDirectory(root);
            PluginMetadataReader reader = new PluginMetadataReader(
                new PluginInspectionReferences(
                    Path.GetFullPath(contractPath),
                    Path.GetFullPath(dependencyDirectory),
                    Path.GetFullPath(gameManagedDirectory)
                )
            );
            LoadRuntimeContract(contractPath, gameManagedDirectory);
            VerifyMissingDependency(reader, fixturePath, root);
            VerifySelectionBoundary(reader, fixturePath, root);
            VerifyIdentityAndTypeDrift(reader, fixturePath, root);
            VerifyChangedContent(reader, fixturePath, root);
            VerifyLoadFailureDoesNotRetryOrFallback(
                reader,
                fixturePath,
                root
            );
        }

        private static void VerifyMissingDependency(
            PluginMetadataReader reader,
            string source,
            string root
        )
        {
            string path = Path.Combine(root, "missing-dependency.dll");
            WriteVariant(
                source,
                path,
                "DSPPluginManager.RM14MissingDependency",
                "rm14.missing-dependency",
                "Missing Dependency",
                "1.0.0"
            );
            AddMissingInterface(path);
            CandidateReconciliationEntry selected = Selected(reader, path);
            int loadCount = 0;
            SelectedCandidateLoader loader = new SelectedCandidateLoader(
                assemblyPath =>
                {
                    loadCount++;
                    return Assembly.LoadFrom(assemblyPath);
                }
            );

            CandidateRuntimeLoadResult result = loader.Load(selected);
            TestAssert.True(!result.IsLoaded, "Missing dependency was accepted.");
            TestAssert.Equal(
                CandidateRuntimeLoadDiagnosticCode.MissingDependency,
                result.Diagnostic.Code,
                "missing-dependency diagnostic"
            );
            AssertCandidateContext(result.Diagnostic, selected.Candidate);
            TestAssert.True(
                result.Diagnostic.Exception != null &&
                result.Diagnostic.Exception.ToString().Length > 0,
                "Missing-dependency exception context was discarded."
            );
            TestAssert.True(
                result.Diagnostic.Detail.Contains(
                    "DSPPluginManager.RM14AbsentDependency"
                ),
                "The missing assembly identity was absent from the diagnostic."
            );
            TestAssert.True(
                object.ReferenceEquals(result, loader.Load(selected)),
                "A failed selected path did not retain its first outcome."
            );
            TestAssert.Equal(1, loadCount, "missing-dependency load attempts");
        }

        private static void LoadRuntimeContract(
            string contractPath,
            string gameManagedDirectory
        )
        {
            string unityPath = Path.Combine(
                Path.GetFullPath(gameManagedDirectory),
                "UnityEngine.CoreModule.dll"
            );
            Assembly.LoadFrom(unityPath);
            Assembly.LoadFrom(Path.GetFullPath(contractPath));
        }

        private static void VerifySelectionBoundary(
            PluginMetadataReader reader,
            string source,
            string root
        )
        {
            string selectedPath = Path.Combine(root, "a-selected.dll");
            string redundantPath = Path.Combine(root, "z-redundant.dll");
            string supersededPath = Path.Combine(root, "old.dll");
            string ambiguousOne = Path.Combine(root, "ambiguous-a.dll");
            string ambiguousTwo = Path.Combine(root, "ambiguous-b.dll");
            string rejectedPath = Path.Combine(root, "rejected.dll");

            WriteVariant(
                source,
                selectedPath,
                "DSPPluginManager.RM14Selection",
                "rm14.selection",
                "Selected",
                "2.0.0"
            );
            File.Copy(selectedPath, redundantPath);
            WriteVariant(
                source,
                supersededPath,
                "DSPPluginManager.RM14Selection",
                "rm14.selection",
                "Superseded",
                "1.0.0"
            );
            WriteVariant(
                source,
                ambiguousOne,
                "DSPPluginManager.RM14Ambiguous",
                "rm14.ambiguous",
                "Ambiguous One",
                "1.0.0"
            );
            WriteVariant(
                source,
                ambiguousTwo,
                "DSPPluginManager.RM14Ambiguous",
                "rm14.ambiguous",
                "Ambiguous Two",
                "1.0.0"
            );
            File.WriteAllText(rejectedPath, "not a managed assembly");

            string[] paths =
            {
                rejectedPath,
                ambiguousTwo,
                supersededPath,
                redundantPath,
                selectedPath,
                ambiguousOne
            };
            CandidateReconciliationResult reconciliation =
                new CandidateReconciler().Reconcile(
                    paths.Select(reader.Inspect)
                );
            TestAssert.Equal(
                1,
                Count(reconciliation, CandidateReconciliationState.Selected),
                "runtime fixture selected count"
            );
            TestAssert.Equal(
                1,
                Count(reconciliation, CandidateReconciliationState.Redundant),
                "runtime fixture redundant count"
            );
            TestAssert.Equal(
                1,
                Count(reconciliation, CandidateReconciliationState.Superseded),
                "runtime fixture superseded count"
            );
            TestAssert.Equal(
                2,
                Count(reconciliation, CandidateReconciliationState.Ambiguous),
                "runtime fixture ambiguous count"
            );
            TestAssert.Equal(
                1,
                Count(reconciliation, CandidateReconciliationState.Rejected),
                "runtime fixture rejected count"
            );

            List<string> attemptedPaths = new List<string>();
            SelectedCandidateLoader loader = new SelectedCandidateLoader(
                assemblyPath =>
                {
                    attemptedPaths.Add(Path.GetFullPath(assemblyPath));
                    return Assembly.LoadFrom(assemblyPath);
                }
            );
            foreach (CandidateReconciliationEntry entry in reconciliation.Entries
                .Where(candidate =>
                    candidate.State != CandidateReconciliationState.Selected
                ))
            {
                CandidateRuntimeLoadResult rejected = loader.Load(entry);
                TestAssert.True(
                    !rejected.IsLoaded &&
                    rejected.Diagnostic.Code ==
                        CandidateRuntimeLoadDiagnosticCode.CandidateNotSelected,
                    "A non-selected reconciliation entry crossed the loader."
                );
            }
            TestAssert.Equal(
                0,
                attemptedPaths.Count,
                "non-selected runtime load attempts"
            );

            CandidateReconciliationEntry selected = reconciliation.Entries
                .Single(entry =>
                    entry.State == CandidateReconciliationState.Selected
                );
            CandidateRuntimeLoadResult first = loader.Load(selected);
            CandidateRuntimeLoadResult second = loader.Load(selected);
            CandidateRuntimeLoadResult fromAnotherLoader =
                new SelectedCandidateLoader(path =>
                {
                    throw new InvalidOperationException(
                        "A second loader attempted the selected path."
                    );
                }).Load(selected);
            TestAssert.True(
                first.IsLoaded,
                first.IsLoaded
                    ? string.Empty
                    : "The selected candidate did not load: " +
                        first.Diagnostic.Code + " " +
                        first.Diagnostic.Detail +
                        (first.Diagnostic.Exception == null
                            ? string.Empty
                            : " " + DescribeException(
                                first.Diagnostic.Exception
                            ))
            );
            TestAssert.True(
                object.ReferenceEquals(first, second) &&
                object.ReferenceEquals(first, fromAnotherLoader),
                "A selected path retained more than one process outcome."
            );
            TestAssert.Equal(1, attemptedPaths.Count, "selected load attempts");
            TestAssert.Equal(
                Path.GetFullPath(selectedPath),
                attemptedPaths[0],
                "selected load path"
            );
            TestAssert.Equal(
                FixtureTypeName,
                first.PluginType.FullName,
                "resolved plugin type"
            );
            TestAssert.True(
                object.ReferenceEquals(first.Assembly, first.PluginType.Assembly),
                "The resolved type belongs to another assembly."
            );

            HashSet<string> loadedPaths = LoadedPaths();
            foreach (string nonSelectedPath in new[]
            {
                redundantPath,
                supersededPath,
                ambiguousOne,
                ambiguousTwo
            })
            {
                TestAssert.True(
                    !loadedPaths.Contains(Path.GetFullPath(nonSelectedPath)),
                    "A non-selected fixture was runtime-loaded: " +
                        nonSelectedPath
                );
            }
        }

        private static void VerifyIdentityAndTypeDrift(
            PluginMetadataReader reader,
            string source,
            string root
        )
        {
            string identityPath = Path.Combine(root, "identity-drift.dll");
            WriteVariant(
                source,
                identityPath,
                "DSPPluginManager.RM14IdentityActual",
                "rm14.identity-drift",
                "Identity Drift",
                "1.0.0"
            );
            RecognizedPluginCandidate identityCandidate =
                Selected(reader, identityPath).Candidate;
            RecognizedPluginCandidate wrongIdentity = CopyCandidate(
                identityCandidate,
                "DSPPluginManager.RM14IdentityExpected, Version=1.0.0.0, " +
                    "Culture=neutral, PublicKeyToken=null",
                identityCandidate.TypeName
            );
            int identityLoads = 0;
            CandidateRuntimeLoadResult identityResult =
                new SelectedCandidateLoader(path =>
                {
                    identityLoads++;
                    return Assembly.LoadFrom(path);
                }).Load(Selected(wrongIdentity));
            TestAssert.Equal(
                CandidateRuntimeLoadDiagnosticCode.AssemblyIdentityMismatch,
                identityResult.Diagnostic.Code,
                "identity-drift diagnostic"
            );
            TestAssert.Equal(0, identityLoads, "identity-drift load attempts");
            AssertCandidateContext(identityResult.Diagnostic, wrongIdentity);

            string typePath = Path.Combine(root, "type-drift.dll");
            WriteVariant(
                source,
                typePath,
                "DSPPluginManager.RM14TypeDrift",
                "rm14.type-drift",
                "Type Drift",
                "1.0.0"
            );
            RecognizedPluginCandidate typeCandidate =
                Selected(reader, typePath).Candidate;
            RecognizedPluginCandidate wrongType = CopyCandidate(
                typeCandidate,
                typeCandidate.AssemblyIdentity,
                typeCandidate.TypeName + ".Missing"
            );
            CandidateRuntimeLoadResult typeResult =
                new SelectedCandidateLoader().Load(Selected(wrongType));
            TestAssert.Equal(
                CandidateRuntimeLoadDiagnosticCode.PluginTypeNotFound,
                typeResult.Diagnostic.Code,
                "type-drift diagnostic"
            );
            AssertCandidateContext(typeResult.Diagnostic, wrongType);
        }

        private static void VerifyChangedContent(
            PluginMetadataReader reader,
            string source,
            string root
        )
        {
            string path = Path.Combine(root, "changed-content.dll");
            WriteVariant(
                source,
                path,
                "DSPPluginManager.RM14ChangedContent",
                "rm14.changed-content",
                "Before Inspection",
                "1.0.0"
            );
            CandidateReconciliationEntry selected = Selected(reader, path);
            WriteVariant(
                source,
                path,
                "DSPPluginManager.RM14ChangedContent",
                "rm14.changed-content",
                "After Inspection",
                "1.0.0"
            );
            int loadCount = 0;
            CandidateRuntimeLoadResult result =
                new SelectedCandidateLoader(assemblyPath =>
                {
                    loadCount++;
                    return Assembly.LoadFrom(assemblyPath);
                }).Load(selected);
            TestAssert.Equal(
                CandidateRuntimeLoadDiagnosticCode.CandidateContentChanged,
                result.Diagnostic.Code,
                "changed-content diagnostic"
            );
            TestAssert.Equal(0, loadCount, "changed-content load attempts");
        }

        private static void VerifyLoadFailureDoesNotRetryOrFallback(
            PluginMetadataReader reader,
            string source,
            string root
        )
        {
            string highPath = Path.Combine(root, "failure-high.dll");
            string lowPath = Path.Combine(root, "failure-low.dll");
            WriteVariant(
                source,
                highPath,
                "DSPPluginManager.RM14Failure",
                "rm14.failure",
                "Failure High",
                "2.0.0"
            );
            WriteVariant(
                source,
                lowPath,
                "DSPPluginManager.RM14Failure",
                "rm14.failure",
                "Failure Low",
                "1.0.0"
            );
            CandidateReconciliationResult reconciliation =
                new CandidateReconciler().Reconcile(new[]
                {
                    reader.Inspect(lowPath),
                    reader.Inspect(highPath)
                });
            CandidateReconciliationEntry selected = reconciliation.Entries
                .Single(entry =>
                    entry.State == CandidateReconciliationState.Selected
                );
            CandidateReconciliationEntry superseded = reconciliation.Entries
                .Single(entry =>
                    entry.State == CandidateReconciliationState.Superseded
                );
            List<string> attempts = new List<string>();
            SelectedCandidateLoader loader = new SelectedCandidateLoader(path =>
            {
                attempts.Add(Path.GetFullPath(path));
                throw new InvalidOperationException(
                    "RM-14 forced runtime-load failure.\r\n" +
                    "The complete exception must be retained."
                );
            });

            CandidateRuntimeLoadResult first = loader.Load(selected);
            CandidateRuntimeLoadResult second = loader.Load(selected);
            CandidateRuntimeLoadResult older = loader.Load(superseded);
            TestAssert.Equal(
                CandidateRuntimeLoadDiagnosticCode.AssemblyLoadFailed,
                first.Diagnostic.Code,
                "forced-load diagnostic"
            );
            TestAssert.True(
                object.ReferenceEquals(first, second),
                "A failed runtime load was retried."
            );
            TestAssert.Equal(1, attempts.Count, "forced-load attempts");
            TestAssert.Equal(
                Path.GetFullPath(highPath),
                attempts[0],
                "forced-load selected path"
            );
            TestAssert.True(
                !older.IsLoaded &&
                older.Diagnostic.Code ==
                    CandidateRuntimeLoadDiagnosticCode.CandidateNotSelected,
                "The loader fell back to the superseded candidate."
            );
            TestAssert.True(
                first.Diagnostic.Exception != null &&
                first.Diagnostic.Exception.ToString().Contains(
                    "The complete exception must be retained."
                ),
                "The complete load exception was discarded."
            );
            AssertCandidateContext(first.Diagnostic, selected.Candidate);
        }

        private static CandidateReconciliationEntry Selected(
            PluginMetadataReader reader,
            string path
        )
        {
            PluginInspectionResult inspection = reader.Inspect(path);
            TestAssert.True(
                inspection.IsRecognized,
                "Runtime fixture was not statically recognized: " + path +
                    (inspection.Diagnostic == null
                        ? string.Empty
                        : " " + inspection.Diagnostic.Code + ": " +
                            inspection.Diagnostic.Detail)
            );
            return Selected(inspection.Candidate);
        }

        private static CandidateReconciliationEntry Selected(
            RecognizedPluginCandidate candidate
        )
        {
            return new CandidateReconciliationEntry(
                CandidateReconciliationState.Selected,
                candidate,
                null,
                null
            );
        }

        private static RecognizedPluginCandidate CopyCandidate(
            RecognizedPluginCandidate source,
            string assemblyIdentity,
            string typeName
        )
        {
            return new RecognizedPluginCandidate(
                source.Identifier,
                source.IdentifierComparisonKey,
                source.DisplayName,
                source.Version,
                assemblyIdentity,
                source.AssemblyPath,
                typeName,
                source.ContentHash
            );
        }

        private static void AssertCandidateContext(
            CandidateRuntimeLoadDiagnostic diagnostic,
            RecognizedPluginCandidate candidate
        )
        {
            TestAssert.Equal(candidate.Identifier, diagnostic.Identifier,
                "diagnostic identifier");
            TestAssert.Equal(candidate.Version.ToString(3), diagnostic.Version,
                "diagnostic version");
            TestAssert.Equal(candidate.AssemblyPath, diagnostic.AssemblyPath,
                "diagnostic path");
            TestAssert.Equal(candidate.TypeName, diagnostic.TypeName,
                "diagnostic type");
            TestAssert.Equal("runtime-load", diagnostic.Phase,
                "diagnostic phase");
            TestAssert.True(
                !string.IsNullOrWhiteSpace(diagnostic.Detail),
                "Runtime-load diagnostic detail is empty."
            );
        }

        private static int Count(
            CandidateReconciliationResult result,
            CandidateReconciliationState state
        )
        {
            return result.Entries.Count(entry => entry.State == state);
        }

        private static HashSet<string> LoadedPaths()
        {
            return new HashSet<string>(
                AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic)
                    .Select(assembly =>
                    {
                        try
                        {
                            return assembly.Location;
                        }
                        catch (NotSupportedException)
                        {
                            return null;
                        }
                    })
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase
            );
        }

        private static string DescribeException(Exception exception)
        {
            ReflectionTypeLoadException reflectionFailure =
                exception as ReflectionTypeLoadException;
            if (reflectionFailure == null ||
                reflectionFailure.LoaderExceptions == null)
            {
                return exception.ToString();
            }
            return exception + " LoaderExceptions: " + string.Join(
                " | ",
                reflectionFailure.LoaderExceptions.Select(item =>
                    item == null ? "<null>" : item.ToString()
                )
            );
        }

        private static void WriteVariant(
            string source,
            string destination,
            string assemblyName,
            string identifier,
            string displayName,
            string version
        )
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
                source,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            {
                assembly.Name.Name = assemblyName;
                assembly.MainModule.Name = assemblyName + ".dll";
                TypeDefinition type = assembly.MainModule.GetType(
                    FixtureTypeName
                );
                CustomAttribute marker = type.CustomAttributes.Single(attribute =>
                    attribute.AttributeType.FullName ==
                        PluginContractRules.MetadataTypeName
                );
                marker.ConstructorArguments[0] = new CustomAttributeArgument(
                    assembly.MainModule.TypeSystem.String,
                    identifier
                );
                marker.ConstructorArguments[1] = new CustomAttributeArgument(
                    assembly.MainModule.TypeSystem.String,
                    displayName
                );
                marker.ConstructorArguments[2] = new CustomAttributeArgument(
                    assembly.MainModule.TypeSystem.String,
                    version
                );
                assembly.Write(destination);
            }
        }

        private static void AddMissingInterface(string path)
        {
            using (AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
                path,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            {
                AssemblyNameReference missing = new AssemblyNameReference(
                    "DSPPluginManager.RM14AbsentDependency",
                    new Version(1, 0, 0, 0)
                );
                assembly.MainModule.AssemblyReferences.Add(missing);
                TypeReference missingInterface = new TypeReference(
                    "DSPPluginManager.RM14AbsentDependency",
                    "IMissingRuntimeDependency",
                    assembly.MainModule,
                    missing
                );
                TypeDefinition type = assembly.MainModule.GetType(
                    FixtureTypeName
                );
                type.Interfaces.Add(new InterfaceImplementation(
                    missingInterface
                ));
                assembly.Write(path);
            }
        }
    }
}
