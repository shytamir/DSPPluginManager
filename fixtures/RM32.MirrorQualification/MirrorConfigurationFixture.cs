using DSPPluginManager.Contracts;
using HarmonyLib;
using UnityEngine;

namespace DSPPluginManager.RM32MirrorQualification
{
    internal sealed class MirrorConfigurationFixture
    {
        private readonly PluginConfigurationEntry<bool> enabled;
        private readonly PluginConfigurationEntry<bool> verbose;
        private readonly PluginConfigurationEntry<KeyboardShortcut> shortcut;

        internal MirrorConfigurationFixture(PluginConfiguration configuration)
        {
            enabled = configuration.Bind(
                "Diagnostics",
                "Enabled",
                false,
                "Enable diagnostics."
            );
            verbose = configuration.Bind(
                "Diagnostics",
                "Verbose",
                false,
                "Enable verbose diagnostics."
            );
            shortcut = configuration.Bind(
                "Diagnostics",
                "Shortcut",
                new KeyboardShortcut(KeyCode.F9),
                "Diagnostic shortcut."
            );
        }

        internal bool Enabled => enabled.Value;

        internal bool Verbose => verbose.Value;

        internal string HarmonyContract => typeof(Harmony).FullName;

        internal string DisplayShortcut()
        {
            return shortcut.Value.ToString();
        }

        internal bool IsShortcutDown()
        {
            return shortcut.Value.IsDown();
        }

        internal void SetVerbose(bool value)
        {
            verbose.Value = value;
        }

        internal void SetShortcut(KeyboardShortcut value)
        {
            shortcut.Value = value;
        }
    }
}
