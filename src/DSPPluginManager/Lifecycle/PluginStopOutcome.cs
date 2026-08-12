using System;

namespace DSPPluginManager.Lifecycle
{
    internal sealed class PluginStopOutcome
    {
        internal PluginStopOutcome(
            PluginLifecycleRecord lifecycle,
            PluginStopInvocationResult invocation
        )
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(
                "lifecycle"
            );
            Invocation = invocation;
        }

        internal PluginLifecycleRecord Lifecycle { get; }

        internal PluginStopInvocationResult Invocation { get; }

        internal bool IsStopped
        {
            get { return Lifecycle.State == PluginLifecycleState.Stopped; }
        }
    }
}
