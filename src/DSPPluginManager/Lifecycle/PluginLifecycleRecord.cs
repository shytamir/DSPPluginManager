using System;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.Lifecycle
{
    internal sealed class PluginLifecycleRecord
    {
        private readonly object sync = new object();
        private PluginLifecycleState state;
        private PluginLifecycleFailure failure;

        internal PluginLifecycleRecord(CandidateReconciliationEntry selected)
        {
            if (selected == null)
            {
                throw new ArgumentNullException("selected");
            }
            if (selected.State != CandidateReconciliationState.Selected ||
                selected.Candidate == null)
            {
                throw new ArgumentException(
                    "A lifecycle record requires a selected candidate entry.",
                    "selected"
                );
            }
            Candidate = selected.Candidate;
            state = PluginLifecycleState.Selected;
        }

        internal RecognizedPluginCandidate Candidate { get; }

        internal PluginLifecycleState State
        {
            get
            {
                lock (sync)
                {
                    return state;
                }
            }
        }

        internal PluginLifecycleFailure Failure
        {
            get
            {
                lock (sync)
                {
                    return failure;
                }
            }
        }

        internal PluginLifecycleTransitionResult TransitionTo(
            PluginLifecycleState requestedState
        )
        {
            lock (sync)
            {
                if (requestedState == state)
                {
                    return Rejected(
                        requestedState,
                        PluginLifecycleTransitionDiagnosticCode
                            .DuplicateTransition,
                        "The plugin is already " + state + "."
                    );
                }
                if (IsFailureState(requestedState))
                {
                    return Rejected(
                        requestedState,
                        PluginLifecycleTransitionDiagnosticCode
                            .FailureContextRequired,
                        "Transition to " + requestedState +
                            " requires phase and exception context."
                    );
                }
                if (!CanTransition(state, requestedState))
                {
                    return Rejected(
                        requestedState,
                        PluginLifecycleTransitionDiagnosticCode
                            .InvalidTransition,
                        "Transition from " + state + " to " +
                            requestedState + " is not allowed."
                    );
                }

                return Accepted(requestedState, null);
            }
        }

        internal PluginLifecycleTransitionResult TransitionToFailure(
            PluginLifecycleState requestedState,
            string phase,
            Exception exception
        )
        {
            lock (sync)
            {
                if (requestedState == state)
                {
                    return Rejected(
                        requestedState,
                        PluginLifecycleTransitionDiagnosticCode
                            .DuplicateTransition,
                        "The plugin is already " + state + "."
                    );
                }
                if (!IsFailureState(requestedState))
                {
                    return Rejected(
                        requestedState,
                        PluginLifecycleTransitionDiagnosticCode
                            .FailureContextNotAllowed,
                        "Failure context can be attached only to Failed or " +
                            "StopFailed."
                    );
                }
                if (string.IsNullOrWhiteSpace(phase) || exception == null)
                {
                    return Rejected(
                        requestedState,
                        PluginLifecycleTransitionDiagnosticCode
                            .FailureContextRequired,
                        "Transition to " + requestedState +
                            " requires a non-empty phase and exception."
                    );
                }
                if (!CanTransition(state, requestedState))
                {
                    return Rejected(
                        requestedState,
                        PluginLifecycleTransitionDiagnosticCode
                            .InvalidTransition,
                        "Transition from " + state + " to " +
                            requestedState + " is not allowed."
                    );
                }

                PluginLifecycleFailure transitionFailure =
                    new PluginLifecycleFailure(Candidate, phase, exception);
                return Accepted(requestedState, transitionFailure);
            }
        }

        private PluginLifecycleTransitionResult Accepted(
            PluginLifecycleState requestedState,
            PluginLifecycleFailure transitionFailure
        )
        {
            PluginLifecycleState previousState = state;
            state = requestedState;
            failure = transitionFailure;
            return new PluginLifecycleTransitionResult(
                true,
                previousState,
                requestedState,
                state,
                null
            );
        }

        private PluginLifecycleTransitionResult Rejected(
            PluginLifecycleState requestedState,
            PluginLifecycleTransitionDiagnosticCode code,
            string detail
        )
        {
            return new PluginLifecycleTransitionResult(
                false,
                state,
                requestedState,
                state,
                new PluginLifecycleTransitionDiagnostic(code, detail)
            );
        }

        private static bool IsFailureState(PluginLifecycleState candidate)
        {
            return candidate == PluginLifecycleState.Failed ||
                candidate == PluginLifecycleState.StopFailed;
        }

        private static bool CanTransition(
            PluginLifecycleState current,
            PluginLifecycleState requested
        )
        {
            switch (current)
            {
                case PluginLifecycleState.Selected:
                    return requested == PluginLifecycleState.Activating ||
                        requested == PluginLifecycleState.Failed;
                case PluginLifecycleState.Activating:
                    return requested == PluginLifecycleState.Active ||
                        requested == PluginLifecycleState.Failed;
                case PluginLifecycleState.Active:
                    return requested == PluginLifecycleState.Stopping;
                case PluginLifecycleState.Stopping:
                    return requested == PluginLifecycleState.Stopped ||
                        requested == PluginLifecycleState.StopFailed;
                default:
                    return false;
            }
        }
    }
}
