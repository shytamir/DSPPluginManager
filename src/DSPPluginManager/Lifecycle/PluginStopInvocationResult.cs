using System;

namespace DSPPluginManager.Lifecycle
{
    internal sealed class PluginStopInvocationResult
    {
        private PluginStopInvocationResult(
            bool acknowledged,
            string failurePhase,
            Exception exception
        )
        {
            bool failurePhaseMissing = string.IsNullOrWhiteSpace(failurePhase);
            if ((acknowledged && (exception != null || !failurePhaseMissing)) ||
                (!acknowledged && (exception == null || failurePhaseMissing)))
            {
                throw new ArgumentException(
                    "A stop invocation must be acknowledged or retain one " +
                    "attributable failure."
                );
            }
            Acknowledged = acknowledged;
            FailurePhase = failurePhase;
            Exception = exception;
        }

        internal bool Acknowledged { get; }

        internal string FailurePhase { get; }

        internal Exception Exception { get; }

        internal static PluginStopInvocationResult Stopped()
        {
            return new PluginStopInvocationResult(true, null, null);
        }

        internal static PluginStopInvocationResult Failed(
            string failurePhase,
            Exception exception
        )
        {
            return new PluginStopInvocationResult(
                false,
                failurePhase,
                exception ?? throw new ArgumentNullException("exception")
            );
        }
    }
}
