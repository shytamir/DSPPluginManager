using System;
using System.Collections.Generic;
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
        private readonly UnityHostBridge unityHost;
        private readonly Dictionary<string, PluginLifecycleRecord> records;
        private readonly Dictionary<string, PluginActivationOutcome> outcomes;

        internal PluginActivationCoordinator(
            SelectedCandidateLoader loader,
            LogDispatcher dispatcher,
            string writableParent,
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
            this.unityHost = unityHost ?? throw new ArgumentNullException(
                "unityHost"
            );
            records = new Dictionary<string, PluginLifecycleRecord>(
                PluginContractRules.IdentifierComparer
            );
            outcomes = new Dictionary<string, PluginActivationOutcome>(
                PluginContractRules.IdentifierComparer
            );
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
                    PluginActivationRequest request =
                        new PluginActivationRequest(
                            selected.Candidate,
                            runtimeLoad.PluginType,
                            logger,
                            writableRoot
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
                return outcome;
            }
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
