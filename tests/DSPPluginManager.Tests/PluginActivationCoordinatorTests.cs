using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DSPPluginManager.Discovery;
using DSPPluginManager.Hosting;
using DSPPluginManager.Lifecycle;
using DSPPluginManager.Loading;
using DSPPluginManager.Logging;

namespace DSPPluginManager.Tests
{
    internal static class PluginActivationCoordinatorTests
    {
        private const string Identifier =
            "com.shytamir.dspmirrorblueprint";

        internal static void Run(
            string dependencyDirectory,
            string unityHostPath,
            string facadePath,
            string contractPath,
            string consumerPath
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
                PluginInspectionResult inspection =
                    new PluginMetadataReader(
                        new PluginInspectionReferences(
                            Path.GetFullPath(contractPath),
                            Path.GetFullPath(dependencyDirectory),
                            Path.GetDirectoryName(Path.GetFullPath(facadePath))
                        )
                    ).Inspect(Path.GetFullPath(consumerPath));
                TestAssert.True(
                    inspection.IsRecognized,
                    "The activation fixture was not statically recognized."
                );
                CandidateReconciliationEntry selected =
                    new CandidateReconciliationEntry(
                        CandidateReconciliationState.Selected,
                        inspection.Candidate,
                        null,
                        null
                    );

                CollectingSink sink = new CollectingSink();
                UnityHostBridge unityHost = new UnityHostBridge(unityHostPath);
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
                                .GetLifecycleRecord(Identifier).State;
                        }),
                        null
                    );
                };
                AppDomain.CurrentDomain.AssemblyLoad += observerInstaller;

                PluginActivationOutcome first = coordinator.Activate(selected);
                AppDomain.CurrentDomain.AssemblyLoad -= observerInstaller;
                observerInstaller = null;

                TestAssert.True(
                    first.IsActive,
                    "Normal Activate return did not acknowledge Active: " +
                    (first.Lifecycle.Failure == null
                        ? "no failure context"
                        : first.Lifecycle.Failure.Phase + " " +
                            first.Lifecycle.Failure.ExceptionText)
                );
                TestAssert.Equal(
                    PluginLifecycleState.Activating,
                    stateSeenInsideActivate,
                    "lifecycle state observed inside Activate"
                );
                TestAssert.True(
                    first.RuntimeLoad != null && first.RuntimeLoad.IsLoaded &&
                    first.RuntimeLoad.PluginType == first.Instance.GetType(),
                    "Activation did not attach the exact inspected runtime type."
                );

                Type activationEvidence = first.RuntimeLoad.Assembly.GetType(
                    "DSPPluginManager.RM09Consumer.MirrorActivationEvidence",
                    true,
                    false
                );
                TestAssert.Equal(
                    1,
                    ReadEvidence<int>(activationEvidence, "ActivationCount"),
                    "activation callback count"
                );
                TestAssert.Equal(
                    0,
                    ReadEvidence<int>(activationEvidence, "DeactivationCount"),
                    "deactivation callback count"
                );
                TestAssert.Equal(
                    true,
                    ReadEvidence<bool>(activationEvidence, "LoggerAvailable"),
                    "logger availability during activation"
                );
                TestAssert.Equal(
                    Path.Combine(writableParent, Identifier),
                    ReadEvidence<string>(activationEvidence, "WritableRoot"),
                    "writable root during activation"
                );
                TestAssert.Equal(
                    true,
                    ReadEvidence<bool>(activationEvidence, "InitiallyEnabled"),
                    "initial enabled state"
                );
                TestAssert.Equal(
                    true,
                    ReadEvidence<bool>(activationEvidence, "AttachedGameObject"),
                    "Unity attachment during activation"
                );
                TestAssert.True(
                    sink.Records.Exists(record =>
                        record.Source.Kind == LogSourceKind.Plugin &&
                        record.Source.Identifier == Identifier &&
                        record.Source.DisplayName == "DSP Mirror Blueprint" &&
                        record.Message == "RM-19 activation acknowledged."
                    ),
                    "Activation log lost its plugin attribution."
                );

                PluginActivationOutcome repeated =
                    coordinator.Activate(selected);
                TestAssert.True(
                    object.ReferenceEquals(first, repeated) &&
                    object.ReferenceEquals(first.Instance, repeated.Instance),
                    "Repeated activation did not reuse the retained outcome."
                );
                TestAssert.Equal(
                    1,
                    ReadEvidence<int>(activationEvidence, "ActivationCount"),
                    "repeated activation callback count"
                );

                object gameObject = first.Instance.GetType()
                    .GetProperty("gameObject")
                    .GetValue(first.Instance, null);
                Type facadeRuntime = Assembly.LoadFrom(
                    Path.GetFullPath(facadePath)
                ).GetType("UnityEngine.FacadeRuntime", true, false);
                int attached = (int)facadeRuntime.GetMethod(
                    "AttachedComponentCount"
                ).Invoke(null, new[] { gameObject });
                TestAssert.Equal(
                    1,
                    attached,
                    "selected plugin component count"
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
