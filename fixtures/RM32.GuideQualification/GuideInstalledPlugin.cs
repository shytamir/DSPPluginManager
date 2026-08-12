using System;
using System.IO;
using System.Linq;
using System.Threading;
using DSPPluginManager.Contracts;
using UnityEngine;

namespace DSPPluginManager.RM32GuideQualification
{
    [Plugin(
        "fixture.rm34.guide",
        "RM-34 Guide Qualification",
        "1.0.0"
    )]
    public sealed class GuideInstalledPlugin : PluginBehaviour
    {
        private PluginConfigurationEntry<bool> showPanelSetting;
        private PluginConfigurationEntry<KeyboardShortcut> shortcutSetting;
        private PluginConfigurationEntry<string> currentSetting;
        private PluginConfigurationEntry<string> legacySetting;
        private int run;
        private int activateThread;
        private int pollThread;
        private int cleanupThread;
        private int pollCount;
        private int exitCountdown = -1;
        private bool showPanelAtActivation;
        private string shortcutBeforeMutation;
        private string currentBeforeMutation;
        private string legacyAtActivation;
        private bool bepInExLoadedAtActivation;

        public override void Activate()
        {
            run = int.Parse(File.ReadAllText(
                Path.Combine(WritableRoot, "RUN.txt")
            ));
            activateThread = Thread.CurrentThread.ManagedThreadId;
            showPanelSetting = Config.Bind(
                "General",
                "Show Panel",
                true,
                "Show the progression panel."
            );
            shortcutSetting = Config.Bind(
                "General",
                "Toggle Shortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Toggle the progression panel."
            );
            showPanelAtActivation = showPanelSetting.Value;
            shortcutBeforeMutation = shortcutSetting.Value.ToString();

            currentSetting = Config.Bind(
                "Phase Selection",
                "Current",
                string.Empty,
                "Current save selection."
            );
            legacySetting = Config.Bind(
                "Phase Selection",
                "Legacy",
                string.Empty,
                "Legacy save selection."
            );
            currentBeforeMutation = currentSetting.Value;
            legacyAtActivation = legacySetting.Value;
            bepInExLoadedAtActivation = IsBepInExLoaded();
            if (run == 1)
            {
                currentSetting.Value = "next phase";
                shortcutSetting.Value = new KeyboardShortcut(KeyCode.F8);
                Config.Save();
            }
            if (bepInExLoadedAtActivation)
            {
                throw new InvalidOperationException(
                    "RM-34 Guide observed a loaded BepInEx assembly."
                );
            }
            File.WriteAllText(
                Path.Combine(WritableRoot, "ACTIVATED-" + run + ".txt"),
                "ready"
            );
            Logger.Information(
                "RM-34 Guide activated for run " + run + "."
            );
        }

        private void Update()
        {
            if (pollCount == 0 && shortcutSetting.Value.IsDown())
            {
                pollCount = 1;
                pollThread = Thread.CurrentThread.ManagedThreadId;
                File.WriteAllText(
                    Path.Combine(WritableRoot, "POLL-" + run + ".txt"),
                    "observed"
                );
            }
            if (exitCountdown < 0 && pollCount == 1 && File.Exists(
                    Path.Combine(
                        Directory.GetParent(WritableRoot).FullName,
                        "fixture.rm34.mirror",
                        "POLL-" + run + ".txt"
                    )
                ))
            {
                exitCountdown = 12;
            }
            if (exitCountdown == 0)
            {
                Application.Quit();
                exitCountdown = -1;
            }
            else if (exitCountdown > 0)
            {
                exitCountdown--;
            }
        }

        public override void Deactivate()
        {
            cleanupThread = Thread.CurrentThread.ManagedThreadId;
            string[] lines =
            {
                "run=" + run,
                "activateThread=" + activateThread,
                "pollThread=" + pollThread,
                "cleanupThread=" + cleanupThread,
                "pollCount=" + pollCount,
                "showPanelAtActivation=" + showPanelAtActivation,
                "shortcutBeforeMutation=" + shortcutBeforeMutation,
                "shortcutAtCleanup=" + shortcutSetting.Value,
                "currentBeforeMutation=" + currentBeforeMutation,
                "currentAtCleanup=" + currentSetting.Value,
                "legacyAtActivation=" + legacyAtActivation,
                "bepInExLoadedAtActivation=" +
                    bepInExLoadedAtActivation,
                "bepInExLoadedAtCleanup=" + IsBepInExLoaded(),
                "loggerAvailable=" + (Logger != null),
                "configurationAvailable=" + (Config != null),
                "writableRootAvailable=" + Directory.Exists(WritableRoot),
                "componentAvailable=" + (enabled && gameObject != null),
                "contractAvailable=" +
                    (typeof(PluginBehaviour).Assembly != null),
                "unityAvailable=" + (typeof(GameObject).Assembly != null)
            };
            File.WriteAllLines(
                Path.Combine(
                    WritableRoot,
                    "RM34-GUIDE-" + run + ".log"
                ),
                lines
            );
            if (pollCount != 1)
            {
                throw new InvalidOperationException(
                    "RM-34 Guide cleanup evidence was invalid."
                );
            }
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
    }
}
