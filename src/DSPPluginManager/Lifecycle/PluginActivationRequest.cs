using System;
using DSPPluginManager.Discovery;
using DSPPluginManager.Logging;

namespace DSPPluginManager.Lifecycle
{
    internal sealed class PluginActivationRequest
    {
        internal PluginActivationRequest(
            RecognizedPluginCandidate candidate,
            Type pluginType,
            SourceLogger logger,
            string writableRoot
        )
        {
            Candidate = candidate ?? throw new ArgumentNullException(
                "candidate"
            );
            PluginType = pluginType ?? throw new ArgumentNullException(
                "pluginType"
            );
            Logger = logger ?? throw new ArgumentNullException("logger");
            WritableRoot = writableRoot ?? throw new ArgumentNullException(
                "writableRoot"
            );
        }

        internal RecognizedPluginCandidate Candidate { get; }

        internal Type PluginType { get; }

        internal SourceLogger Logger { get; }

        internal string WritableRoot { get; }
    }

    internal sealed class PluginActivationInvocationResult
    {
        private PluginActivationInvocationResult(
            bool acknowledged,
            object instance,
            string failurePhase,
            Exception exception
        )
        {
            bool failurePhaseMissing = string.IsNullOrWhiteSpace(failurePhase);
            if ((acknowledged &&
                    (instance == null || exception != null ||
                     !failurePhaseMissing)) ||
                (!acknowledged &&
                    (exception == null || failurePhaseMissing)))
            {
                throw new ArgumentException(
                    "An activation invocation must be acknowledged or retain " +
                    "one attributable failure."
                );
            }
            Acknowledged = acknowledged;
            Instance = instance;
            FailurePhase = failurePhase;
            Exception = exception;
        }

        internal bool Acknowledged { get; }

        internal object Instance { get; }

        internal string FailurePhase { get; }

        internal Exception Exception { get; }

        internal static PluginActivationInvocationResult Active(object instance)
        {
            return new PluginActivationInvocationResult(
                true,
                instance,
                null,
                null
            );
        }

        internal static PluginActivationInvocationResult Failed(
            object instance,
            string failurePhase,
            Exception exception
        )
        {
            return new PluginActivationInvocationResult(
                false,
                instance,
                failurePhase,
                exception ?? throw new ArgumentNullException("exception")
            );
        }
    }
}
