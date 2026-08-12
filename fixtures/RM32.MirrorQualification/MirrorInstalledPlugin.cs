using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using DSPPluginManager.Contracts;
using HarmonyLib;

namespace DSPPluginManager.RM32MirrorQualification
{
    [Plugin(
        "fixture.rm34.mirror",
        "RM-34 Mirror Qualification",
        "1.0.0"
    )]
    public sealed class MirrorInstalledPlugin : PluginBehaviour
    {
        private const string PatchId = "fixture.rm34.mirror";

        private PluginConfigurationEntry<bool> enabledSetting;
        private PluginConfigurationEntry<bool> verboseSetting;
        private PluginConfigurationEntry<KeyboardShortcut> shortcutSetting;
        private Harmony harmony;
        private MethodInfo patchTarget;
        private int run;
        private int activateThread;
        private int pollThread;
        private int cleanupThread;
        private int pollCount;
        private bool enabledAtActivation;
        private bool verboseAtActivation;
        private string shortcutAtActivation;
        private int patchedResult;
        private int cleanupResult;
        private bool bepInExLoadedAtActivation;

        public override void Activate()
        {
            run = ReadRunNumber();
            activateThread = Thread.CurrentThread.ManagedThreadId;
            enabledSetting = Config.Bind(
                "Diagnostics",
                "Enabled",
                false,
                "Enable diagnostics."
            );
            verboseSetting = Config.Bind(
                "Diagnostics",
                "Verbose",
                false,
                "Enable verbose diagnostics."
            );
            shortcutSetting = Config.Bind(
                "Diagnostics",
                "Shortcut",
                new KeyboardShortcut(UnityEngine.KeyCode.F9),
                "Diagnostic shortcut."
            );
            enabledAtActivation = enabledSetting.Value;
            verboseAtActivation = verboseSetting.Value;
            shortcutAtActivation = shortcutSetting.Value.ToString();
            bepInExLoadedAtActivation = IsBepInExLoaded();

            patchTarget = AccessTools.Method(
                typeof(MirrorInstalledPatchTarget),
                "Compute"
            );
            harmony = new Harmony(PatchId);
            harmony.Patch(
                patchTarget,
                postfix: new HarmonyMethod(AccessTools.Method(
                    typeof(MirrorInstalledPlugin),
                    "Postfix"
                ))
            );
            patchedResult = MirrorInstalledPatchTarget.Compute(1);
            if (patchedResult != 12 || bepInExLoadedAtActivation)
            {
                throw new InvalidOperationException(
                    "RM-34 Mirror dependency boundary was not satisfied."
                );
            }
            if (run == 1)
            {
                verboseSetting.Value = true;
            }
            File.WriteAllText(
                Path.Combine(WritableRoot, "ACTIVATED-" + run + ".txt"),
                "ready"
            );
            Logger.Information(
                "RM-34 Mirror activated for run " + run + "."
            );
        }

        private void Update()
        {
            if (pollCount != 0 || !shortcutSetting.Value.IsDown())
            {
                return;
            }
            pollCount = 1;
            pollThread = Thread.CurrentThread.ManagedThreadId;
            File.WriteAllText(
                Path.Combine(WritableRoot, "POLL-" + run + ".txt"),
                "observed"
            );
        }

        public override void Deactivate()
        {
            cleanupThread = Thread.CurrentThread.ManagedThreadId;
            harmony.UnpatchSelf();
            cleanupResult = MirrorInstalledPatchTarget.Compute(1);
            string[] lines =
            {
                "run=" + run,
                "activateThread=" + activateThread,
                "pollThread=" + pollThread,
                "cleanupThread=" + cleanupThread,
                "pollCount=" + pollCount,
                "enabledAtActivation=" + enabledAtActivation,
                "verboseAtActivation=" + verboseAtActivation,
                "shortcutAtActivation=" + shortcutAtActivation,
                "verboseAtCleanup=" + verboseSetting.Value,
                "patchedResult=" + patchedResult,
                "cleanupResult=" + cleanupResult,
                "bepInExLoadedAtActivation=" +
                    bepInExLoadedAtActivation,
                "bepInExLoadedAtCleanup=" + IsBepInExLoaded(),
                "loggerAvailable=" + (Logger != null),
                "configurationAvailable=" + (Config != null),
                "writableRootAvailable=" + Directory.Exists(WritableRoot),
                "componentAvailable=" + (enabled && gameObject != null),
                "contractAvailable=" +
                    (typeof(PluginBehaviour).Assembly != null),
                "unityAvailable=" +
                    (typeof(UnityEngine.GameObject).Assembly != null)
            };
            File.WriteAllLines(
                Path.Combine(
                    WritableRoot,
                    "RM34-MIRROR-" + run + ".log"
                ),
                lines
            );
            if (pollCount != 1 || cleanupResult != 2)
            {
                throw new InvalidOperationException(
                    "RM-34 Mirror cleanup evidence was invalid."
                );
            }
        }

        private int ReadRunNumber()
        {
            return int.Parse(File.ReadAllText(
                Path.Combine(WritableRoot, "RUN.txt")
            ));
        }

        private static bool IsBepInExLoaded()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                assembly.GetName().Name.StartsWith(
                    "BepInEx",
                    StringComparison.Ordinal
                )
            );
        }

        private static void Postfix(ref int __result)
        {
            __result += 10;
        }
    }

    internal static class MirrorInstalledPatchTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int Compute(int value)
        {
            return value + 1;
        }
    }
}
