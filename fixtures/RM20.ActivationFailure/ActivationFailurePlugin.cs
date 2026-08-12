using System;
using DSPPluginManager.Contracts;

namespace DSPPluginManager.RM20ActivationFailure
{
    [Plugin(
        "fixture.rm20.activation-failure",
        "RM-20 Activation Failure",
        "1.0.0"
    )]
    public sealed class ActivationFailurePlugin : PluginBehaviour
    {
        public override void Activate()
        {
            Logger.Information(
                "RM-20 activation failure fixture reached explicit startup."
            );
            throw new InvalidOperationException(
                "RM-20 intentional activation failure."
            );
        }

        public override void Deactivate()
        {
        }
    }
}
