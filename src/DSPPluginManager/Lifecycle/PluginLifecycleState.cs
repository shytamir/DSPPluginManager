using System;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.Lifecycle
{
    internal enum PluginLifecycleState
    {
        Selected,
        Activating,
        Active,
        Failed,
        Stopping,
        Stopped,
        StopFailed
    }

    internal enum PluginLifecycleTransitionDiagnosticCode
    {
        DuplicateTransition,
        InvalidTransition,
        FailureContextRequired,
        FailureContextNotAllowed
    }

    internal sealed class PluginLifecycleTransitionDiagnostic
    {
        internal PluginLifecycleTransitionDiagnostic(
            PluginLifecycleTransitionDiagnosticCode code,
            string detail
        )
        {
            Code = code;
            Detail = detail ?? throw new ArgumentNullException("detail");
        }

        internal PluginLifecycleTransitionDiagnosticCode Code { get; }

        internal string Detail { get; }
    }

    internal sealed class PluginLifecycleTransitionResult
    {
        internal PluginLifecycleTransitionResult(
            bool accepted,
            PluginLifecycleState previousState,
            PluginLifecycleState requestedState,
            PluginLifecycleState currentState,
            PluginLifecycleTransitionDiagnostic diagnostic
        )
        {
            if (accepted == (diagnostic != null))
            {
                throw new ArgumentException(
                    "A lifecycle transition must be accepted or carry one " +
                    "rejection diagnostic."
                );
            }
            Accepted = accepted;
            PreviousState = previousState;
            RequestedState = requestedState;
            CurrentState = currentState;
            Diagnostic = diagnostic;
        }

        internal bool Accepted { get; }

        internal PluginLifecycleState PreviousState { get; }

        internal PluginLifecycleState RequestedState { get; }

        internal PluginLifecycleState CurrentState { get; }

        internal PluginLifecycleTransitionDiagnostic Diagnostic { get; }
    }

    internal sealed class PluginLifecycleFailure
    {
        internal PluginLifecycleFailure(
            RecognizedPluginCandidate candidate,
            string phase,
            Exception exception
        )
        {
            if (candidate == null)
            {
                throw new ArgumentNullException("candidate");
            }
            if (string.IsNullOrWhiteSpace(phase))
            {
                throw new ArgumentException(
                    "The lifecycle failure phase is required.",
                    "phase"
                );
            }
            Identifier = candidate.Identifier;
            Version = candidate.Version;
            AssemblyPath = candidate.AssemblyPath;
            TypeName = candidate.TypeName;
            Phase = phase;
            Exception = exception ?? throw new ArgumentNullException("exception");
            ExceptionText = exception.ToString();
        }

        internal string Identifier { get; }

        internal Version Version { get; }

        internal string AssemblyPath { get; }

        internal string TypeName { get; }

        internal string Phase { get; }

        internal Exception Exception { get; }

        internal string ExceptionText { get; }
    }
}
