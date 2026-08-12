using System;

namespace DSPPluginManager.Contracts
{
    public sealed class PluginConfiguration
    {
        private readonly Func<string, string, bool, string,
            PluginConfigurationEntry<bool>> bindBoolean;
        private readonly Func<string, string, string, string,
            PluginConfigurationEntry<string>> bindString;
        private readonly Func<string, string, KeyboardShortcut, string,
            PluginConfigurationEntry<KeyboardShortcut>> bindShortcut;
        private readonly Action save;

        internal PluginConfiguration(
            Func<string, string, bool, string,
                PluginConfigurationEntry<bool>> bindBoolean,
            Func<string, string, string, string,
                PluginConfigurationEntry<string>> bindString,
            Func<string, string, KeyboardShortcut, string,
                PluginConfigurationEntry<KeyboardShortcut>> bindShortcut,
            Action save
        )
        {
            this.bindBoolean = bindBoolean ??
                throw new ArgumentNullException("bindBoolean");
            this.bindString = bindString ??
                throw new ArgumentNullException("bindString");
            this.bindShortcut = bindShortcut ??
                throw new ArgumentNullException("bindShortcut");
            this.save = save ?? throw new ArgumentNullException("save");
        }

        public PluginConfigurationEntry<bool> Bind(
            string section,
            string key,
            bool defaultValue,
            string description
        )
        {
            ValidateDefinition(section, key, description);
            return RequireEntry(bindBoolean(
                section,
                key,
                defaultValue,
                description
            ));
        }

        public PluginConfigurationEntry<string> Bind(
            string section,
            string key,
            string defaultValue,
            string description
        )
        {
            ValidateDefinition(section, key, description);
            if (defaultValue == null)
            {
                throw new ArgumentNullException("defaultValue");
            }
            return RequireEntry(bindString(
                section,
                key,
                defaultValue,
                description
            ));
        }

        public PluginConfigurationEntry<KeyboardShortcut> Bind(
            string section,
            string key,
            KeyboardShortcut defaultValue,
            string description
        )
        {
            ValidateDefinition(section, key, description);
            return RequireEntry(bindShortcut(
                section,
                key,
                defaultValue,
                description
            ));
        }

        public void Save()
        {
            save();
        }

        private static PluginConfigurationEntry<T> RequireEntry<T>(
            PluginConfigurationEntry<T> entry
        )
        {
            if (entry == null)
            {
                throw new InvalidOperationException(
                    "The host did not return a configuration entry."
                );
            }
            return entry;
        }

        private static void ValidateDefinition(
            string section,
            string key,
            string description
        )
        {
            ValidateDefinitionPart(section, "section", false, '[', ']');
            ValidateDefinitionPart(key, "key", true, '=', '[', ']');
            if (description == null)
            {
                throw new ArgumentNullException("description");
            }
            if (description.IndexOf('\r') >= 0 ||
                description.IndexOf('\n') >= 0)
            {
                throw new ArgumentException(
                    "The description must occupy one line.",
                    "description"
                );
            }
        }

        private static void ValidateDefinitionPart(
            string value,
            string parameterName,
            bool forbidCommentStart,
            params char[] forbidden
        )
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }
            if (value.Length == 0 ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                ContainsControlCharacter(value) ||
                (forbidCommentStart && value[0] == '#') ||
                value.IndexOfAny(forbidden) >= 0)
            {
                throw new ArgumentException(
                    "The configuration " + parameterName + " is invalid.",
                    parameterName
                );
            }
        }

        private static bool ContainsControlCharacter(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
