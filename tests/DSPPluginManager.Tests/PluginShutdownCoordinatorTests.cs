using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using DSPPluginManager.Discovery;
using DSPPluginManager.Hosting;
using DSPPluginManager.Lifecycle;
using DSPPluginManager.Loading;
using DSPPluginManager.Logging;

namespace DSPPluginManager.Tests
{
    internal static class PluginShutdownCoordinatorTests
    {
        private const string FailureIdentifier =
            "fixture.rm22.a-cleanup-failure";
        private const string SuccessIdentifier =
            "fixture.rm22.b-cleanup-success";

        internal static void Run(
            string dependencyDirectory,
            string unityHostPath,
            string facadePath,
            string contractPath,
            string failurePath,
            string successPath
        )
        {
            string writableParent = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(writableParent);
            string configurationDirectory = Path.Combine(
                writableParent,
                "configuration"
            );
            Directory.CreateDirectory(Path.Combine(
                configurationDirectory,
                FailureIdentifier + ".cfg"
            ));
            try
            {
                PluginMetadataReader reader = new PluginMetadataReader(
                    new PluginInspectionReferences(
                        Path.GetFullPath(contractPath),
                        Path.GetFullPath(dependencyDirectory),
                        Path.GetDirectoryName(Path.GetFullPath(facadePath))
                    )
                );
                RecognizedPluginCandidate failure = Inspect(
                    reader,
                    failurePath
                );
                RecognizedPluginCandidate success = Inspect(
                    reader,
                    successPath
                );
                CandidateReconciliationResult reconciliation =
                    new CandidateReconciliationResult(new[]
                    {
                        Selected(failure),
                        Selected(success)
                    });
                CollectingSink sink = new CollectingSink();
                UnityHostBridge unityHost = new UnityHostBridge(
                    unityHostPath,
                    contractPath
                );
                unityHost.EnsureCreated(Thread.CurrentThread.ManagedThreadId);
                PluginActivationCoordinator coordinator =
                    new PluginActivationCoordinator(
                        new SelectedCandidateLoader(),
                        new LogDispatcher(sink),
                        writableParent,
                        configurationDirectory,
                        unityHost
                    );

                IReadOnlyList<PluginActivationOutcome> activations =
                    coordinator.ActivateSelected(reconciliation);
                TestAssert.True(
                    activations.Count == 2 &&
                    activations[0].IsActive && activations[1].IsActive,
                    "RM-22 fixtures did not become active."
                );
                TestAssert.True(
                    sink.Contains(
                        FailureIdentifier,
                        "configuration source",
                        "unavailable"
                    ),
                    "Unavailable configuration diagnostic lost plugin identity."
                );

                IReadOnlyList<PluginStopOutcome> stops = coordinator.StopAll();
                TestAssert.Equal(2, stops.Count, "orderly stop count");
                AssertStopFailed(stops[0], failure);
                TestAssert.True(stops[1].IsStopped,
                    "Cleanup success did not reach Stopped.");
                TestAssert.Equal(
                    PluginLifecycleState.Stopped,
                    coordinator.GetLifecycleRecord(SuccessIdentifier).State,
                    "successful terminal lifecycle state"
                );
                AssertEvidence(
                    activations[0].RuntimeLoad.Assembly,
                    "DSPPluginManager.RM22CleanupFailure.CleanupFailurePlugin",
                    Path.Combine(
                        writableParent,
                        FailureIdentifier,
                        "RM22-FAILURE-EVIDENCE.log"
                    )
                );
                AssertEvidence(
                    activations[1].RuntimeLoad.Assembly,
                    "DSPPluginManager.RM22CleanupSuccess.CleanupSuccessPlugin",
                    Path.Combine(
                        writableParent,
                        SuccessIdentifier,
                        "RM22-SUCCESS-EVIDENCE.log"
                    )
                );
                AssertDestroyed(unityHostPath, facadePath, FailureIdentifier);
                AssertDestroyed(unityHostPath, facadePath, SuccessIdentifier);
                TestAssert.True(
                    Directory.Exists(Path.Combine(
                        configurationDirectory,
                        FailureIdentifier + ".cfg"
                    )),
                    "Write-blocked configuration path was replaced or deleted."
                );
                TestAssert.True(
                    File.Exists(Path.Combine(
                        configurationDirectory,
                        SuccessIdentifier + ".cfg"
                    )),
                    "Stopped plugin configuration file was deleted."
                );
                int failureLog = sink.IndexOf(
                    FailureIdentifier,
                    "RM-22 failure fixture cleanup entered."
                );
                int successLog = sink.IndexOf(
                    SuccessIdentifier,
                    "RM-22 success fixture cleanup entered."
                );
                TestAssert.True(failureLog >= 0 && successLog > failureLog,
                    "A failed cleanup prevented or reordered later cleanup.");

                IReadOnlyList<PluginStopOutcome> repeated =
                    coordinator.StopAll();
                TestAssert.True(
                    object.ReferenceEquals(stops[0], repeated[0]) &&
                    object.ReferenceEquals(stops[1], repeated[1]),
                    "Repeated orderly stop did not retain terminal outcomes."
                );
                TestAssert.Equal(1, CleanupCount(
                    activations[0].RuntimeLoad.Assembly,
                    "DSPPluginManager.RM22CleanupFailure.CleanupFailurePlugin"
                ), "failed cleanup callback count");
                TestAssert.Equal(1, CleanupCount(
                    activations[1].RuntimeLoad.Assembly,
                    "DSPPluginManager.RM22CleanupSuccess.CleanupSuccessPlugin"
                ), "successful cleanup callback count");
            }
            finally
            {
                if (Directory.Exists(writableParent))
                {
                    Directory.Delete(writableParent, true);
                }
            }
        }

        private static RecognizedPluginCandidate Inspect(
            PluginMetadataReader reader,
            string path
        )
        {
            PluginInspectionResult result = reader.Inspect(path);
            TestAssert.True(result.IsRecognized,
                "RM-22 fixture was not recognized: " + path);
            return result.Candidate;
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

        private static void AssertStopFailed(
            PluginStopOutcome outcome,
            RecognizedPluginCandidate candidate
        )
        {
            TestAssert.Equal(
                PluginLifecycleState.StopFailed,
                outcome.Lifecycle.State,
                "failed terminal lifecycle state"
            );
            PluginLifecycleFailure failure = outcome.Lifecycle.Failure;
            TestAssert.True(
                failure != null && failure.Identifier == candidate.Identifier &&
                failure.Version.Equals(candidate.Version) &&
                failure.AssemblyPath == candidate.AssemblyPath &&
                failure.TypeName == candidate.TypeName &&
                failure.Phase == "deactivation" &&
                failure.ExceptionText.IndexOf(
                    "RM-22 intentional cleanup failure.",
                    StringComparison.Ordinal
                ) >= 0,
                "StopFailed did not retain attributable failure context."
            );
        }

        private static void AssertEvidence(
            Assembly assembly,
            string typeName,
            string evidencePath
        )
        {
            TestAssert.Equal(1, CleanupCount(assembly, typeName),
                typeName + " cleanup count");
            string evidence = File.ReadAllText(evidencePath);
            foreach (string expected in new[]
            {
                "cleanupCount=1",
                "loggerAvailable=True",
                "writableRootAvailable=True",
                "configurationAvailable=True",
                "configurationValue=True",
                "componentAvailable=True",
                "contractAvailable=True",
                "unityAvailable=True"
            })
            {
                TestAssert.True(
                    evidence.IndexOf(expected, StringComparison.Ordinal) >= 0,
                    typeName + " missing evidence: " + expected
                );
            }
            TestAssert.True(
                evidence.IndexOf(
                    "cleanupThread=" + Thread.CurrentThread.ManagedThreadId,
                    StringComparison.Ordinal
                ) >= 0,
                typeName + " cleanup left the Unity main thread."
            );
        }

        private static int CleanupCount(Assembly assembly, string typeName)
        {
            return (int)assembly.GetType(typeName, true, false).GetField(
                "cleanupCount",
                BindingFlags.Static | BindingFlags.NonPublic
            ).GetValue(null);
        }

        private static void AssertDestroyed(
            string unityHostPath,
            string facadePath,
            string identifier
        )
        {
            Assembly unityHost = Assembly.LoadFrom(
                Path.GetFullPath(unityHostPath)
            );
            object container = unityHost.GetType(
                "DSPPluginManager.UnityHost.UnityHostEntrypoint",
                true,
                false
            ).GetProperty(
                "Current",
                BindingFlags.Static | BindingFlags.NonPublic
            ).GetValue(null, null);
            object slot = container.GetType().GetMethod(
                "GetOrCreatePluginObject",
                BindingFlags.Instance | BindingFlags.NonPublic
            ).Invoke(container, new object[] { identifier });
            TestAssert.True(slot.GetType().GetProperty(
                    "Configuration",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ).GetValue(slot, null) != null,
                identifier + " released configuration before stop completed."
            );
            TestAssert.Equal(null, slot.GetType().GetProperty(
                "Instance",
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(slot, null), identifier + " retained stopped instance");
            object gameObject = slot.GetType().GetProperty(
                "GameObject",
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(slot, null);
            Type runtime = Assembly.LoadFrom(Path.GetFullPath(facadePath))
                .GetType("UnityEngine.FacadeRuntime", true, false);
            TestAssert.Equal(0, runtime.GetMethod("AttachedComponentCount")
                .Invoke(null, new[] { gameObject }),
                identifier + " attached component count after stop");
        }

        private sealed class CollectingSink : ILogSink
        {
            private readonly List<LogRecord> records =
                new List<LogRecord>();

            public void Write(LogRecord record)
            {
                records.Add(record);
            }

            internal int IndexOf(string identifier, string message)
            {
                return records.FindIndex(record =>
                    record.Source.Identifier == identifier &&
                    record.Message == message
                );
            }

            internal bool Contains(
                string identifier,
                params string[] messageParts
            )
            {
                return records.Exists(record =>
                    record.Source.Identifier == identifier &&
                    Array.TrueForAll(messageParts, part =>
                        record.Message.IndexOf(
                            part,
                            StringComparison.OrdinalIgnoreCase
                        ) >= 0
                    )
                );
            }
        }
    }
}
