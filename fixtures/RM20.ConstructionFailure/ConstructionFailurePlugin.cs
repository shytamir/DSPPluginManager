using System;
using DSPPluginManager.Contracts;

namespace DSPPluginManager.RM20ConstructionFailure
{
    [Plugin(
        "fixture.rm20.construction-failure",
        "RM-20 Construction Failure",
        "1.0.0"
    )]
    public sealed class ConstructionFailurePlugin : PluginBehaviour
    {
        public ConstructionFailurePlugin()
        {
            throw new InvalidOperationException(
                "RM-20 intentional construction failure."
            );
        }

        public override void Activate()
        {
        }

        public override void Deactivate()
        {
        }
    }
}
