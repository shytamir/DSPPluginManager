using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using DSPPluginManager.Discovery;
using DSPPluginManager.Hosting;
using DSPPluginManager.Lifecycle;
using DSPPluginManager.Loading;
using DSPPluginManager.Logging;

namespace DSPPluginManager.Tests
{
    internal static class PluginActivationCoordinatorTests
    {
        private const string SuccessIdentifier =
            "com.shytamir.dspmirrorblueprint";
        private const string ConstructionIdentifier =
            "fixture.rm20.construction-failure";
        private const string ActivationIdentifier =
            "fixture.rm20.activation-failure";

        internal static void Run(
            string dependencyDirectory,
            string unityHostPath,
            string facadePath,
            string contractPath,
            string consumerPath,
            string constructionFailurePath,
            string activationFailurePath
        )
        {
            string writableParent = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(writableParent);

            AssemblyLoadEventHandler observerInstaller = null;
            PropertyInfo observerProperty = null;
            try
            {
                PluginMetadataReader reader = new PluginMetadataReader(
                    new PluginInspectionReferences(
                        Path.GetFullPath(contractPath),
                        Path.GetFullPath(dependencyDirectory),
                        Path.GetDirectoryName(Path.GetFullPath(facadePath))
                    )
                );
                CandidateReconciliationEntry construction = Selected(
                    Inspect(reader, constructionFailurePath)
                );
                CandidateReconciliationEntry activation = Selected(
                    Inspect(reader, activationFailurePath)
                );
                CandidateReconciliationEntry success = Selected(
                    Inspect(reader, consumerPath)
                );
                CandidateReconciliationResult reconciliation =
                    CreateMixedReconciliation(
                        construction,
                        activation,
                        success
                    );

                CollectingSink sink = new CollectingSink();
                UnityHostBridge unityHost = new UnityHostBridge(
                    unityHostPath,
                    contractPath
                );
                unityHost.EnsureCreated(
                    System.Threading.Thread.CurrentThread.ManagedThreadId
                );
                PluginActivationCoordinator coordinator =
                    new PluginActivationCoordinator(
                        new SelectedCandidateLoader(),
                        new LogDispatcher(sink),
                        writableParent,
                        unityHost
                    );
                AssertMissingDependencyFailure(
                    success.Candidate,
                    coordinator,
                    unityHost,
                    sink,
                    writableParent
                );

                PluginLifecycleState stateSeenInsideActivate =
                    PluginLifecycleState.Selected;
                observerInstaller = (sender, eventArgs) =>
                {
                    Assembly loaded = eventArgs.LoadedAssembly;
                    if (!string.Equals(
                            loaded.GetName().Name,
                            "DSPPluginManager.RM09Consumer",
                            StringComparison.Ordinal
                        ))
                    {
                        return;
                    }

                    Type evidence = loaded.GetType(
                        "DSPPluginManager.RM09Consumer.MirrorActivationEvidence",
                        true,
                        false
                    );
                    observerProperty = evidence.GetProperty(
                        "Observer",
                        BindingFlags.Static | BindingFlags.NonPublic
                    );
                    observerProperty.SetValue(
                        null,
                        new Action(() =>
                        {
                            stateSeenInsideActivate = coordinator
                                .GetLifecycleRecord(SuccessIdentifier).State;
                        }),
                        null
                    );
                };
                AppDomain.CurrentDomain.AssemblyLoad += observerInstaller;

                IReadOnlyList<PluginActivationOutcome> outcomes =
                    coordinator.ActivateSelected(reconciliation);
                AppDomain.CurrentDomain.AssemblyLoad -= observerInstaller;
                observerInstaller = null;

                TestAssert.Equal(3, outcomes.Count,
                    "selected activation outcome count");
                PluginActivationOutcome constructionFailure = outcomes[0];
                PluginActivationOutcome activationFailure = outcomes[1];
                PluginActivationOutcome active = outcomes[2];
                AssertFailure(
                    constructionFailure,
                    construction.Candidate,
                    "component-construction",
                    "intentional construction failure"
                );
                AssertFailure(
                    activationFailure,
                    activation.Candidate,
                    "activation",
                    "intentional activation failure"
                );
                AssertCleanFailureSlot(
                    unityHostPath,
                    facadePath,
                    ConstructionIdentifier
                );
                AssertCleanFailureSlot(
                    unityHostPath,
                    facadePath,
                    ActivationIdentifier
                );
                AssertActiveSuccess(
                    active,
                    coordinator,
                    sink,
                    writableParent,
                    facadePath,
                    stateSeenInsideActivate
                );

                TestAssert.True(
                    sink.Records.Exists(record =>
                        record.Source.Identifier == ActivationIdentifier &&
                        record.Message.IndexOf(
                            "reached explicit startup",
                            StringComparison.Ordinal
                        ) >= 0
                    ),
                    "The startup failure lost plugin log attribution."
                );
                TestAssert.Equal(
                    null,
                    coordinator.GetLifecycleRecord("fixture.rm20.redundant"),
                    "redundant lifecycle record"
                );
                TestAssert.Equal(
                    null,
                    coordinator.GetLifecycleRecord("fixture.rm20.ambiguous"),
                    "ambiguous lifecycle record"
                );

            }
            finally
            {
                if (observerInstaller != null)
                {
                    AppDomain.CurrentDomain.AssemblyLoad -= observerInstaller;
                }
                if (observerProperty != null)
                {
                    observerProperty.SetValue(null, null, null);
                }
                Directory.Delete(writableParent, true);
            }
        }

        private static RecognizedPluginCandidate Inspect(
            PluginMetadataReader reader,
            string path
        )
        {
            PluginInspectionResult inspection = reader.Inspect(
                Path.GetFullPath(path)
            );
            TestAssert.True(
                inspection.IsRecognized,
                "Activation fixture was not statically recognized: " + path
            );
            return inspection.Candidate;
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

        private static CandidateReconciliationResult CreateMixedReconciliation(
            CandidateReconciliationEntry construction,
            CandidateReconciliationEntry activation,
            CandidateReconciliationEntry success
        )
        {
            RecognizedPluginCandidate redundant = CloneCandidate(
                success.Candidate,
                "fixture.rm20.redundant",
                success.Candidate.Version,
                success.Candidate.AssemblyPath,
                success.Candidate.ContentHash
            );
            RecognizedPluginCandidate ambiguous = CloneCandidate(
                success.Candidate,
                "fixture.rm20.ambiguous",
                success.Candidate.Version,
                success.Candidate.AssemblyPath,
                success.Candidate.ContentHash
            );
            RecognizedPluginCandidate olderFallback = CloneCandidate(
                success.Candidate,
                success.Candidate.Identifier,
                new Version(1, 0, 0),
                success.Candidate.AssemblyPath,
                success.Candidate.ContentHash
            );
            return new CandidateReconciliationResult(new[]
            {
                construction,
                activation,
                success,
                Classified(
                    CandidateReconciliationState.Redundant,
                    redundant,
                    CandidateReconciliationDiagnosticCode.RedundantCopy
                ),
                Classified(
                    CandidateReconciliationState.Ambiguous,
                    ambiguous,
                    CandidateReconciliationDiagnosticCode.AmbiguousIdentity
                ),
                Classified(
                    CandidateReconciliationState.Superseded,
                    olderFallback,
                    CandidateReconciliationDiagnosticCode.SupersededVersion
                )
            });
        }

        private static CandidateReconciliationEntry Classified(
            CandidateReconciliationState state,
            RecognizedPluginCandidate candidate,
            CandidateReconciliationDiagnosticCode code
        )
        {
            return new CandidateReconciliationEntry(
                state,
                candidate,
                null,
                new CandidateReconciliationDiagnostic(
                    code,
                    candidate.AssemblyPath,
                    "RM-20 excluded-entry fixture."
                )
            );
        }

        private static void AssertFailure(
            PluginActivationOutcome outcome,
            RecognizedPluginCandidate candidate,
            string phase,
            string exceptionText
        )
        {
            TestAssert.Equal(
                PluginLifecycleState.Failed,
                outcome.Lifecycle.State,
                candidate.Identifier + " lifecycle state"
            );
            PluginLifecycleFailure failure = outcome.Lifecycle.Failure;
            TestAssert.True(failure != null,
                "Failure context was not retained.");
            TestAssert.Equal(candidate.Identifier, failure.Identifier,
                "failure identifier");
            TestAssert.Equal(candidate.Version, failure.Version,
                "failure version");
            TestAssert.Equal(candidate.AssemblyPath, failure.AssemblyPath,
                "failure assembly path");
            TestAssert.Equal(candidate.TypeName, failure.TypeName,
                "failure type");
            TestAssert.Equal(phase, failure.Phase, "failure phase");
            TestAssert.True(
                failure.ExceptionText.IndexOf(
                    exceptionText,
                    StringComparison.OrdinalIgnoreCase
                ) >= 0,
                "Failure did not retain the complete exception."
            );
        }

        private static void AssertCleanFailureSlot(
            string unityHostPath,
            string facadePath,
            string identifier
        )
        {
            Assembly unityHost = Assembly.LoadFrom(
                Path.GetFullPath(unityHostPath)
            );
            Type entrypoint = unityHost.GetType(
                "DSPPluginManager.UnityHost.UnityHostEntrypoint",
                true,
                false
            );
            object container = entrypoint.GetProperty(
                "Current",
                BindingFlags.Static | BindingFlags.NonPublic
            ).GetValue(null, null);
            object slot = container.GetType().GetMethod(
                "GetOrCreatePluginObject",
                BindingFlags.Instance | BindingFlags.NonPublic
            ).Invoke(container, new object[] { identifier });
            TestAssert.Equal(
                null,
                slot.GetType().GetProperty(
                    "Instance",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ).GetValue(slot, null),
                identifier + " retained failed instance"
            );
            object gameObject = slot.GetType().GetProperty(
                "GameObject",
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(slot, null);
            Type facadeRuntime = Assembly.LoadFrom(
                Path.GetFullPath(facadePath)
            ).GetType("UnityEngine.FacadeRuntime", true, false);
            int components = (int)facadeRuntime.GetMethod(
                "AttachedComponentCount"
            ).Invoke(null, new[] { gameObject });
            TestAssert.Equal(0, components,
                identifier + " attached component count after failure");
        }

        private static void AssertActiveSuccess(
            PluginActivationOutcome active,
            PluginActivationCoordinator coordinator,
            CollectingSink sink,
            string writableParent,
            string facadePath,
            PluginLifecycleState stateSeenInsideActivate
        )
        {
            TestAssert.True(active.IsActive,
                "Unrelated selected fixture did not reach Active.");
            TestAssert.Equal(
                PluginLifecycleState.Activating,
                stateSeenInsideActivate,
                "lifecycle state observed inside Activate"
            );
            TestAssert.True(
                active.RuntimeLoad != null && active.RuntimeLoad.IsLoaded &&
                active.RuntimeLoad.PluginType == active.Instance.GetType(),
                "Activation did not attach the exact inspected runtime type."
            );
            Type evidence = active.RuntimeLoad.Assembly.GetType(
                "DSPPluginManager.RM09Consumer.MirrorActivationEvidence",
                true,
                false
            );
            TestAssert.Equal(1, ReadEvidence<int>(evidence, "ActivationCount"),
                "activation callback count");
            TestAssert.Equal(true,
                ReadEvidence<bool>(evidence, "LoggerAvailable"),
                "logger availability during activation");
            TestAssert.Equal(
                Path.Combine(writableParent, SuccessIdentifier),
                ReadEvidence<string>(evidence, "WritableRoot"),
                "writable root during activation"
            );
            TestAssert.Equal(true,
                ReadEvidence<bool>(evidence, "InitiallyEnabled"),
                "initial enabled state");
            TestAssert.Equal(true,
                ReadEvidence<bool>(evidence, "AttachedGameObject"),
                "Unity attachment during activation");
            TestAssert.True(
                sink.Records.Exists(record =>
                    record.Source.Identifier == SuccessIdentifier &&
                    record.Message == "RM-19 activation acknowledged."
                ),
                "Successful activation lost plugin log attribution."
            );

            PluginActivationOutcome repeated = coordinator.Activate(
                new CandidateReconciliationEntry(
                    CandidateReconciliationState.Selected,
                    active.Lifecycle.Candidate,
                    null,
                    null
                )
            );
            TestAssert.True(
                object.ReferenceEquals(active, repeated) &&
                object.ReferenceEquals(active.Instance, repeated.Instance),
                "Repeated activation did not reuse the retained outcome."
            );
            TestAssert.Equal(1, ReadEvidence<int>(evidence, "ActivationCount"),
                "repeated activation callback count");

            object gameObject = active.Instance.GetType()
                .GetProperty("gameObject")
                .GetValue(active.Instance, null);
            Type facadeRuntime = Assembly.LoadFrom(
                Path.GetFullPath(facadePath)
            ).GetType("UnityEngine.FacadeRuntime", true, false);
            int components = (int)facadeRuntime.GetMethod(
                "AttachedComponentCount"
            ).Invoke(null, new[] { gameObject });
            TestAssert.Equal(1, components,
                "successful plugin component count");
        }

        private static void AssertMissingDependencyFailure(
            RecognizedPluginCandidate source,
            PluginActivationCoordinator priorCoordinator,
            UnityHostBridge unityHost,
            CollectingSink sink,
            string writableParent
        )
        {
            string path = Path.Combine(
                writableParent,
                "runtime-dependency-fixture.dll"
            );
            File.Copy(source.AssemblyPath, path);
            RecognizedPluginCandidate candidate = CloneCandidate(
                source,
                "fixture.rm20.runtime-dependency",
                source.Version,
                path,
                ComputeHash(path)
            );
            PluginActivationCoordinator coordinator =
                new PluginActivationCoordinator(
                    new SelectedCandidateLoader(candidatePath =>
                    {
                        throw new FileNotFoundException(
                            "RM-20 missing runtime dependency.",
                            "Fixture.Dependency.dll"
                        );
                    }),
                    new LogDispatcher(sink),
                    writableParent,
                    unityHost
                );
            PluginActivationOutcome outcome = coordinator.Activate(
                Selected(candidate)
            );
            AssertFailure(
                outcome,
                candidate,
                "runtime-load",
                "MissingDependency"
            );
            TestAssert.Equal(
                CandidateRuntimeLoadDiagnosticCode.MissingDependency,
                outcome.RuntimeLoad.Diagnostic.Code,
                "runtime dependency diagnostic code"
            );
            TestAssert.Equal(
                null,
                priorCoordinator.GetLifecycleRecord(candidate.Identifier),
                "runtime dependency failure leaked into prior activation set"
            );
        }

        private static RecognizedPluginCandidate CloneCandidate(
            RecognizedPluginCandidate source,
            string identifier,
            Version version,
            string assemblyPath,
            string contentHash
        )
        {
            return new RecognizedPluginCandidate(
                identifier,
                PluginContractRules.GetIdentifierComparisonKey(identifier),
                source.DisplayName,
                version,
                source.AssemblyIdentity,
                Path.GetFullPath(assemblyPath),
                source.TypeName,
                contentHash
            );
        }

        private static string ComputeHash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }

        private static T ReadEvidence<T>(Type evidence, string propertyName)
        {
            return (T)evidence.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.NonPublic
            ).GetValue(null, null);
        }

        private sealed class CollectingSink : ILogSink
        {
            internal List<LogRecord> Records { get; } =
                new List<LogRecord>();

            public void Write(LogRecord record)
            {
                Records.Add(record);
            }
        }
    }
}
