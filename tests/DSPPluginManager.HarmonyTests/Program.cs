using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DSPPluginManager.Dependencies;
using DSPPluginManager.Discovery;
using DSPPluginManager.Hosting;
using DSPPluginManager.Lifecycle;
using DSPPluginManager.Loading;
using DSPPluginManager.Logging;

namespace DSPPluginManager.HarmonyTests
{
    internal static class Program
    {
        private const string NonHarmonyIdentifier =
            "fixture.rm22.b-cleanup-success";
        private const string FailureIdentifier =
            "fixture.rm23.a-harmony-activation-failure";
        private const string HarmonyIdentifier =
            "fixture.rm23.b-harmony-lifecycle";

        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 7)
                {
                    throw new InvalidOperationException(
                        "Expected dependency directory, Unity host, facade, " +
                        "contract, non-Harmony fixture, Harmony failure " +
                        "fixture, and Harmony lifecycle fixture."
                    );
                }
                Run(args);
                Console.WriteLine(
                    "RM-23 isolated Harmony lifecycle tests passed."
                );
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void Run(string[] args)
        {
            string dependencyDirectory = Path.GetFullPath(args[0]);
            string unityHostPath = Path.GetFullPath(args[1]);
            string facadePath = Path.GetFullPath(args[2]);
            string contractPath = Path.GetFullPath(args[3]);
            string nonHarmonyPath = Path.GetFullPath(args[4]);
            string failurePath = Path.GetFullPath(args[5]);
            string harmonyPath = Path.GetFullPath(args[6]);
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.HarmonyTests",
                Guid.NewGuid().ToString("N")
            );
            string pluginDirectory = Path.Combine(sandbox, "plugins");
            string writableParent = Path.Combine(sandbox, "writable");
            Directory.CreateDirectory(pluginDirectory);
            Directory.CreateDirectory(writableParent);

            try
            {
                AssertFixtureDoesNotCopyDependencies(failurePath);
                AssertFixtureDoesNotCopyDependencies(harmonyPath);
                AssertHarmonyReference(failurePath);
                AssertHarmonyReference(harmonyPath);

                using (ReservedDependencyResolver resolver =
                    new ReservedDependencyResolver(
                        dependencyDirectory,
                        pluginDirectory
                    ))
                {
                    resolver.Install();
                    Assembly.LoadFrom(facadePath);
                    PluginMetadataReader reader = new PluginMetadataReader(
                        new PluginInspectionReferences(
                            contractPath,
                            dependencyDirectory,
                            Path.GetDirectoryName(facadePath)
                        )
                    );
                    RecognizedPluginCandidate nonHarmony = Inspect(
                        reader,
                        nonHarmonyPath
                    );
                    RecognizedPluginCandidate failure = Inspect(
                        reader,
                        failurePath
                    );
                    RecognizedPluginCandidate harmony = Inspect(
                        reader,
                        harmonyPath
                    );
                    CandidateReconciliationResult reconciliation =
                        new CandidateReconciliationResult(new[]
                        {
                            Selected(nonHarmony),
                            Selected(failure),
                            Selected(harmony)
                        });
                    CollectingSink sink = new CollectingSink();
                    UnityHostBridge unityHost = new UnityHostBridge(
                        unityHostPath,
                        contractPath
                    );
                    unityHost.EnsureCreated(
                        Thread.CurrentThread.ManagedThreadId
                    );
                    PluginActivationCoordinator coordinator =
                        new PluginActivationCoordinator(
                            new SelectedCandidateLoader(),
                            new LogDispatcher(sink),
                            writableParent,
                            unityHost
                        );

                    IReadOnlyList<PluginActivationOutcome> activations =
                        coordinator.ActivateSelected(reconciliation);
                    if (!(activations.Count == 3 &&
                        activations[0].IsActive &&
                        activations[1].Lifecycle.State ==
                            PluginLifecycleState.Failed &&
                        activations[2].IsActive))
                    {
                        throw new InvalidOperationException(
                            "Harmony failure isolation produced invalid " +
                            "activation outcomes: " + string.Join(
                                " | ",
                                activations.Select(DescribeActivation)
                                    .ToArray()
                            )
                        );
                    }
                    PluginLifecycleFailure activationFailure =
                        activations[1].Lifecycle.Failure;
                    Require(
                        activationFailure != null &&
                        activationFailure.Phase == "activation" &&
                        activationFailure.ExceptionText.IndexOf(
                            "Null method",
                            StringComparison.OrdinalIgnoreCase
                        ) >= 0,
                        "The Harmony patch failure was not retained as an " +
                        "attributable activation failure."
                    );
                    AssertExactLoadedClosure(dependencyDirectory);
                    Require(
                        sink.Contains(
                            HarmonyIdentifier,
                            "RM-23 attributable Harmony postfix applied:"
                        ),
                        "The attributable postfix result was not logged."
                    );

                    IReadOnlyList<PluginStopOutcome> stops =
                        coordinator.StopAll();
                    Require(
                        stops.Count == 2 && stops[0].IsStopped &&
                        stops[1].IsStopped &&
                        coordinator.GetLifecycleRecord(
                            NonHarmonyIdentifier
                        ).State == PluginLifecycleState.Stopped &&
                        coordinator.GetLifecycleRecord(
                            HarmonyIdentifier
                        ).State == PluginLifecycleState.Stopped,
                        "A Harmony failure prevented an unrelated lifecycle " +
                        "or successful Harmony cleanup."
                    );
                    AssertExactLoadedClosure(dependencyDirectory);
                    AssertEvidence(
                        Path.Combine(
                            writableParent,
                            HarmonyIdentifier,
                            "RM23-HARMONY-EVIDENCE.log"
                        ),
                        dependencyDirectory
                    );
                    Require(
                        sink.Contains(
                            HarmonyIdentifier,
                            "RM-23 Harmony cleanup verified:"
                        ),
                        "Harmony cleanup verification was not logged."
                    );
                }
            }
            finally
            {
                if (Directory.Exists(sandbox))
                {
                    Directory.Delete(sandbox, true);
                }
            }
        }

        private static void AssertFixtureDoesNotCopyDependencies(string path)
        {
            string directory = Path.GetDirectoryName(path);
            string[] forbidden =
            {
                "0Harmony.dll",
                "MonoMod.RuntimeDetour.dll",
                "MonoMod.Utils.dll",
                "Mono.Cecil.dll"
            };
            foreach (string fileName in forbidden)
            {
                Require(
                    !File.Exists(Path.Combine(directory, fileName)),
                    "A manager-owned dependency leaked into fixture output: " +
                    Path.Combine(directory, fileName)
                );
            }
        }

        private static void AssertHarmonyReference(string path)
        {
            AssemblyName[] references = Assembly.ReflectionOnlyLoadFrom(path)
                .GetReferencedAssemblies();
            AssemblyName[] harmony = references.Where(reference =>
                reference.Name == "0Harmony"
            ).ToArray();
            Require(
                harmony.Length == 1 &&
                harmony[0].Version.ToString() == "2.5.5.0",
                "Fixture does not reference the exact manager-owned Harmony " +
                "identity: " + path
            );
            Require(
                references.All(reference =>
                    reference.Name != "MonoMod.RuntimeDetour" &&
                    reference.Name != "MonoMod.Utils" &&
                    reference.Name != "Mono.Cecil"
                ),
                "Fixture directly references an internal runtime dependency: " +
                path
            );
        }

        private static RecognizedPluginCandidate Inspect(
            PluginMetadataReader reader,
            string path
        )
        {
            PluginInspectionResult result = reader.Inspect(path);
            Require(result.IsRecognized,
                "RM-23 fixture was not recognized: " + path);
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

        private static void AssertExactLoadedClosure(string dependencyDirectory)
        {
            Dictionary<string, string> expected =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "0Harmony", "2.5.5.0" },
                    { "MonoMod.RuntimeDetour", "21.9.19.1" },
                    { "MonoMod.Utils", "21.9.19.1" },
                    { "Mono.Cecil", "0.10.4.0" }
                };
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Assembly[] matches = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => assembly.GetName().Name == pair.Key)
                    .ToArray();
                Require(matches.Length == 1,
                    "Expected one loaded " + pair.Key + " assembly.");
                Require(
                    matches[0].GetName().Version.ToString() == pair.Value &&
                    string.Equals(
                        Path.GetFullPath(matches[0].Location),
                        Path.Combine(
                            dependencyDirectory,
                            pair.Key + ".dll"
                        ),
                        StringComparison.OrdinalIgnoreCase
                    ),
                    "Loaded dependency did not use the exact host path: " +
                    matches[0].FullName + " at '" + matches[0].Location + "'."
                );
            }
        }

        private static void AssertEvidence(
            string evidencePath,
            string dependencyDirectory
        )
        {
            Dictionary<string, string> values = File.ReadAllLines(evidencePath)
                .Select(line => line.Split(new[] { '=' }, 2))
                .ToDictionary(fields => fields[0], fields => fields[1]);
            Dictionary<string, string> expected =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "activationCount", "1" },
                    { "cleanupCount", "1" },
                    { "patchedResult", "112" },
                    { "ownedRemovalResult", "102" },
                    { "finalResult", "2" },
                    { "ownedPatchAttributed", "True" },
                    { "controlPatchAttributed", "True" },
                    { "ownedPatchRemoved", "True" },
                    { "controlPatchPreserved", "True" },
                    { "allPatchesRemoved", "True" }
                };
            foreach (KeyValuePair<string, string> pair in expected)
            {
                Require(values[pair.Key] == pair.Value,
                    "Unexpected Harmony evidence for " + pair.Key + ".");
            }
            Require(
                values["activationThread"] == values["cleanupThread"],
                "Harmony activation and cleanup used different threads."
            );
            foreach (string phase in new[] { "activation", "cleanup" })
            {
                foreach (string name in new[]
                {
                    "0Harmony",
                    "MonoMod.RuntimeDetour",
                    "MonoMod.Utils",
                    "Mono.Cecil"
                })
                {
                    Require(
                        string.Equals(
                            values[phase + "." + name + ".path"],
                            Path.Combine(dependencyDirectory, name + ".dll"),
                            StringComparison.OrdinalIgnoreCase
                        ),
                        "Evidence did not retain the host-owned path for " +
                        phase + " " + name + "."
                    );
                }
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static string DescribeActivation(
            PluginActivationOutcome outcome
        )
        {
            PluginLifecycleFailure failure = outcome.Lifecycle.Failure;
            return outcome.Lifecycle.Candidate.Identifier + "=" +
                outcome.Lifecycle.State + (failure == null
                    ? string.Empty
                    : "/" + failure.Phase + "/" +
                        failure.ExceptionText);
        }

        private sealed class CollectingSink : ILogSink
        {
            private readonly List<LogRecord> records =
                new List<LogRecord>();

            public void Write(LogRecord record)
            {
                records.Add(record);
            }

            internal bool Contains(string identifier, string prefix)
            {
                return records.Exists(record =>
                    record.Source.Identifier == identifier &&
                    record.Message.StartsWith(
                        prefix,
                        StringComparison.Ordinal
                    )
                );
            }
        }
    }
}
