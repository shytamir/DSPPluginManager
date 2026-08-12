using System;
using System.Collections.Generic;
using DSPPluginManager.Configuration;
using DSPPluginManager.Discovery;
using DSPPluginManager.Hosting;
using DSPPluginManager.Loading;
using DSPPluginManager.Logging;

namespace DSPPluginManager.Lifecycle
{
    internal sealed class PluginActivationCoordinator
    {
        private readonly object sync = new object();
        private readonly SelectedCandidateLoader loader;
        private readonly LogDispatcher dispatcher;
        private readonly string writableParent;
        private readonly string configurationDirectory;
        private readonly UnityHostBridge unityHost;
        private readonly Dictionary<string, PluginLifecycleRecord> records;
        private readonly Dictionary<string, PluginActivationOutcome> outcomes;
        private readonly Dictionary<string, PluginStopOutcome> stopOutcomes;
        private readonly List<string> activationOrder;

        internal PluginActivationCoordinator(
            SelectedCandidateLoader loader,
            LogDispatcher dispatcher,
            string writableParent,
            string configurationDirectory,
            UnityHostBridge unityHost
        )
        {
            this.loader = loader ?? throw new ArgumentNullException("loader");
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(
                "dispatcher"
            );
            if (string.IsNullOrWhiteSpace(writableParent))
            {
                throw new ArgumentException(
                    "The writable parent is required.",
                    "writableParent"
                );
            }
            this.writableParent = writableParent;
            if (string.IsNullOrWhiteSpace(configurationDirectory))
            {
                throw new ArgumentException(
                    "The configuration directory is required.",
                    "configurationDirectory"
                );
            }
            this.configurationDirectory = configurationDirectory;
            this.unityHost = unityHost ?? throw new ArgumentNullException(
                "unityHost"
            );
            records = new Dictionary<string, PluginLifecycleRecord>(
                PluginContractRules.IdentifierComparer
            );
            outcomes = new Dictionary<string, PluginActivationOutcome>(
                PluginContractRules.IdentifierComparer
            );
            stopOutcomes = new Dictionary<string, PluginStopOutcome>(
                PluginContractRules.IdentifierComparer
            );
            activationOrder = new List<string>();
        }

        internal PluginActivationOutcome Activate(
            CandidateReconciliationEntry selected
        )
        {
            if (selected == null)
            {
                throw new ArgumentNullException("selected");
            }
            if (selected.State != CandidateReconciliationState.Selected ||
                selected.Candidate == null)
            {
                throw new ArgumentException(
                    "Only a selected candidate can be activated.",
                    "selected"
                );
            }

            string identifier = selected.Candidate.Identifier;
            lock (sync)
            {
                PluginActivationOutcome retained;
                if (outcomes.TryGetValue(identifier, out retained))
                {
                    return retained;
                }

                PluginLifecycleRecord lifecycle =
                    new PluginLifecycleRecord(selected);
                records.Add(identifier, lifecycle);
                RequireAccepted(
                    lifecycle.TransitionTo(PluginLifecycleState.Activating)
                );

                CandidateRuntimeLoadResult runtimeLoad = null;
                PluginActivationInvocationResult invocation = null;
                string phase = "runtime-load";
                try
                {
                    runtimeLoad = loader.Load(selected);
                    if (!runtimeLoad.IsLoaded)
                    {
                        throw RuntimeLoadException(runtimeLoad.Diagnostic);
                    }

                    phase = "service-preparation";
                    string writableRoot = PluginWritableRootPath.Create(
                        writableParent,
                        selected.Candidate.Identifier
                    );
                    SourceLogger logger = dispatcher.CreateLogger(
                        new LogSourceContext(
                            LogSourceKind.Plugin,
                            selected.Candidate.Identifier,
                            selected.Candidate.DisplayName
                        )
                    );
                    PluginConfigurationScope configurationScope =
                        PluginConfigurationScope.Create(
                            configurationDirectory,
                            selected.Candidate.Identifier
                        );
                    PluginConfigurationDocument configurationDocument =
                        LoadConfiguration(
                            configurationScope,
                            logger
                        );
                    PluginActivationRequest request =
                        new PluginActivationRequest(
                            selected.Candidate,
                            runtimeLoad.PluginType,
                            logger,
                            writableRoot,
                            configurationScope,
                            configurationDocument
                        );

                    phase = "unity-activation";
                    invocation = unityHost.ActivateSelected(request);
                    if (!invocation.Acknowledged)
                    {
                        RequireAccepted(lifecycle.TransitionToFailure(
                            PluginLifecycleState.Failed,
                            invocation.FailurePhase,
                            invocation.Exception
                        ));
                    }
                    else
                    {
                        RequireAccepted(lifecycle.TransitionTo(
                            PluginLifecycleState.Active
                        ));
                    }
                }
                catch (Exception exception)
                {
                    if (lifecycle.State == PluginLifecycleState.Activating)
                    {
                        RequireAccepted(lifecycle.TransitionToFailure(
                            PluginLifecycleState.Failed,
                            phase,
                            exception
                        ));
                    }
                }

                PluginActivationOutcome outcome =
                    new PluginActivationOutcome(
                        lifecycle,
                        runtimeLoad,
                        invocation
                    );
                outcomes.Add(identifier, outcome);
                activationOrder.Add(identifier);
                return outcome;
            }
        }

        internal PluginStopOutcome Stop(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
            {
                throw new ArgumentException(
                    "The plugin identifier is required.",
                    "identifier"
                );
            }

            lock (sync)
            {
                PluginStopOutcome retained;
                if (stopOutcomes.TryGetValue(identifier, out retained))
                {
                    return retained;
                }

                PluginLifecycleRecord lifecycle;
                if (!records.TryGetValue(identifier, out lifecycle) ||
                    lifecycle.State != PluginLifecycleState.Active)
                {
                    throw new InvalidOperationException(
                        "Only an active plugin can be stopped."
                    );
                }

                RequireAccepted(
                    lifecycle.TransitionTo(PluginLifecycleState.Stopping)
                );
                PluginStopInvocationResult invocation = null;
                try
                {
                    invocation = unityHost.StopPlugin(identifier);
                    if (invocation.Acknowledged)
                    {
                        RequireAccepted(lifecycle.TransitionTo(
                            PluginLifecycleState.Stopped
                        ));
                    }
                    else
                    {
                        RequireAccepted(lifecycle.TransitionToFailure(
                            PluginLifecycleState.StopFailed,
                            invocation.FailurePhase,
                            invocation.Exception
                        ));
                    }
                }
                catch (Exception exception)
                {
                    if (lifecycle.State == PluginLifecycleState.Stopping)
                    {
                        RequireAccepted(lifecycle.TransitionToFailure(
                            PluginLifecycleState.StopFailed,
                            "unity-stop",
                            exception
                        ));
                    }
                }

                PluginStopOutcome outcome = new PluginStopOutcome(
                    lifecycle,
                    invocation
                );
                stopOutcomes.Add(identifier, outcome);
                return outcome;
            }
        }

        internal IReadOnlyList<PluginStopOutcome> StopAll()
        {
            lock (sync)
            {
                List<PluginStopOutcome> stopped =
                    new List<PluginStopOutcome>();
                foreach (string identifier in activationOrder)
                {
                    PluginLifecycleRecord lifecycle = records[identifier];
                    if (lifecycle.State == PluginLifecycleState.Active ||
                        stopOutcomes.ContainsKey(identifier))
                    {
                        stopped.Add(Stop(identifier));
                    }
                }
                return stopped.AsReadOnly();
            }
        }

        internal IReadOnlyList<PluginActivationOutcome> ActivateSelected(
            CandidateReconciliationResult reconciliation
        )
        {
            if (reconciliation == null)
            {
                throw new ArgumentNullException("reconciliation");
            }

            List<PluginActivationOutcome> selectedOutcomes =
                new List<PluginActivationOutcome>();
            foreach (CandidateReconciliationEntry entry in
                reconciliation.Entries)
            {
                if (entry.State == CandidateReconciliationState.Selected)
                {
                    selectedOutcomes.Add(Activate(entry));
                }
            }
            return selectedOutcomes.AsReadOnly();
        }

        internal PluginLifecycleRecord GetLifecycleRecord(string identifier)
        {
            lock (sync)
            {
                PluginLifecycleRecord record;
                return records.TryGetValue(identifier, out record)
                    ? record
                    : null;
            }
        }

        private static Exception RuntimeLoadException(
            CandidateRuntimeLoadDiagnostic diagnostic
        )
        {
            return new InvalidOperationException(
                "Selected candidate runtime load failed with " +
                diagnostic.Code + ": " + diagnostic.Detail,
                diagnostic.Exception
            );
        }

        private static PluginConfigurationDocument LoadConfiguration(
            PluginConfigurationScope scope,
            SourceLogger logger
        )
        {
            if (!scope.IsUsable)
            {
                logger.Warning(
                    "Plugin '" + scope.Identifier + "' configuration source '" +
                    scope.FilePath + "' was unavailable; defaults remain " +
                    "usable and writes are blocked for this process. " +
                    scope.Failure
                );
                return PluginConfigurationDocument.Parse(string.Empty);
            }

            PluginConfigurationDocument document =
                PluginConfigurationDocument.Parse(scope.Contents);
            foreach (ConfigurationDocumentDiagnostic diagnostic in
                document.Diagnostics)
            {
                logger.Warning(
                    "Plugin '" + scope.Identifier + "' configuration parse " +
                    "diagnostic " + diagnostic.Code + " at line " +
                    diagnostic.LineNumber + ": " + diagnostic.Detail +
                    " Source='" + diagnostic.LineText + "'."
                );
            }
            return document;
        }

        private static void RequireAccepted(
            PluginLifecycleTransitionResult transition
        )
        {
            if (!transition.Accepted)
            {
                throw new InvalidOperationException(
                    "Lifecycle transition was rejected: " +
                    transition.Diagnostic.Detail
                );
            }
        }
    }
}
