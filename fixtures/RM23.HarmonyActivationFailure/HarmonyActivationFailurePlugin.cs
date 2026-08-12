using DSPPluginManager.Contracts;
using HarmonyLib;

namespace DSPPluginManager.RM23HarmonyActivationFailure
{
    [Plugin(
        "fixture.rm23.a-harmony-activation-failure",
        "RM-23 Harmony Activation Failure",
        "1.0.0"
    )]
    public sealed class HarmonyActivationFailurePlugin : PluginBehaviour
    {
        public override void Activate()
        {
            Logger.Information(
                "RM-23 intentional Harmony patch failure entered."
            );
            Harmony harmony = new Harmony(
                "fixture.rm23.a-harmony-activation-failure"
            );
            harmony.Patch(
                null,
                postfix: new HarmonyMethod(
                    AccessTools.Method(
                        typeof(HarmonyActivationFailurePlugin),
                        "Postfix"
                    )
                )
            );
        }

        public override void Deactivate()
        {
        }

        private static void Postfix()
        {
        }
    }
}
