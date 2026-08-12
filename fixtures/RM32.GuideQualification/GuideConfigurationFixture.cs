using DSPPluginManager.Contracts;
using UnityEngine;

namespace DSPPluginManager.RM32GuideQualification
{
    internal sealed class GuideConfigurationFixture
    {
        private readonly PluginConfiguration configuration;
        private readonly PluginConfigurationEntry<bool> showPanel;
        private readonly PluginConfigurationEntry<KeyboardShortcut> shortcut;
        private PluginConfigurationEntry<string> selection;

        internal GuideConfigurationFixture(PluginConfiguration configuration)
        {
            this.configuration = configuration;
            showPanel = configuration.Bind(
                "General",
                "Show Panel",
                true,
                "Show the progression panel."
            );
            shortcut = configuration.Bind(
                "General",
                "Toggle Shortcut",
                new KeyboardShortcut(KeyCode.F8),
                "Toggle the progression panel."
            );
        }

        internal bool ShowPanel => showPanel.Value;

        internal string DisplayShortcut()
        {
            return shortcut.Value.ToString();
        }

        internal bool IsShortcutDown()
        {
            return shortcut.Value.IsDown();
        }

        internal string Selection => selection == null
            ? null
            : selection.Value;

        internal void SelectSave(string saveKey, string value)
        {
            selection = configuration.Bind(
                "Phase Selection",
                saveKey,
                string.Empty,
                "Selected phase for this save."
            );
            selection.Value = value;
            configuration.Save();
        }

        internal void SetShortcut(KeyboardShortcut value)
        {
            shortcut.Value = value;
        }
    }
}
