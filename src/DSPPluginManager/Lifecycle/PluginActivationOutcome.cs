using System;
using DSPPluginManager.Loading;

namespace DSPPluginManager.Lifecycle
{
    internal sealed class PluginActivationOutcome
    {
        internal PluginActivationOutcome(
            PluginLifecycleRecord lifecycle,
            CandidateRuntimeLoadResult runtimeLoad,
            PluginActivationInvocationResult invocation
        )
        {
            Lifecycle = lifecycle ?? throw new ArgumentNullException(
                "lifecycle"
            );
            RuntimeLoad = runtimeLoad;
            Invocation = invocation;
        }

        internal PluginLifecycleRecord Lifecycle { get; }

        internal CandidateRuntimeLoadResult RuntimeLoad { get; }

        internal PluginActivationInvocationResult Invocation { get; }

        internal object Instance
        {
            get { return Invocation == null ? null : Invocation.Instance; }
        }

        internal bool IsActive
        {
            get { return Lifecycle.State == PluginLifecycleState.Active; }
        }
    }
}
