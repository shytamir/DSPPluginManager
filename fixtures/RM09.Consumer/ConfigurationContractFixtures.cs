using DSPPluginManager.Contracts;
using UnityEngine;

namespace DSPPluginManager.RM09Consumer
{
    internal sealed class MirrorConfigurationShape
    {
        private readonly PluginConfigurationEntry<bool> enabled;
        private readonly PluginConfigurationEntry<bool> verbose;
        private readonly PluginConfigurationEntry<KeyboardShortcut> shortcut;

        internal MirrorConfigurationShape(PluginConfiguration configuration)
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

        internal bool IsEnabled => enabled.Value && verbose.Value;

        internal bool IsShortcutDown()
        {
            return shortcut.Value.IsDown();
        }

        internal string DisplayShortcut()
        {
            return shortcut.Value.ToString();
        }
    }

    internal sealed class GuideConfigurationShape
    {
        private readonly PluginConfiguration configuration;

        internal GuideConfigurationShape(PluginConfiguration configuration)
        {
            this.configuration = configuration;
            configuration.Bind(
                "General",
                "Show Panel",
                true,
                "Show the progression panel."
            );
            configuration.Bind(
                "General",
                "Toggle Shortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Toggle the progression panel."
            );
        }

        internal void SelectSave(string saveKey, string value)
        {
            PluginConfigurationEntry<string> selection = configuration.Bind(
                "Phase Selection",
                saveKey,
                string.Empty,
                "Selected phase for this save."
            );
            selection.Value = value;
            configuration.Save();
        }
    }
}
