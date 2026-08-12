using System;

namespace DSPPluginManager.Configuration
{
    internal readonly struct ConfigurationDefinition :
        IEquatable<ConfigurationDefinition>
    {
        internal ConfigurationDefinition(string section, string key)
        {
            if (!IsValidSection(section))
            {
                throw new ArgumentException(
                    "Configuration section is invalid.",
                    "section"
                );
            }
            if (!IsValidKey(key))
            {
                throw new ArgumentException(
                    "Configuration key is invalid.",
                    "key"
                );
            }

            Section = section;
            Key = key;
        }

        internal string Section { get; }

        internal string Key { get; }

        public bool Equals(ConfigurationDefinition other)
        {
            return string.Equals(
                    Section,
                    other.Section,
                    StringComparison.Ordinal
                ) && string.Equals(
                    Key,
                    other.Key,
                    StringComparison.Ordinal
                );
        }

        public override bool Equals(object obj)
        {
            return obj is ConfigurationDefinition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Section == null
                    ? 0
                    : StringComparer.Ordinal.GetHashCode(Section)) * 397) ^
                    (Key == null
                        ? 0
                        : StringComparer.Ordinal.GetHashCode(Key));
            }
        }

        internal static bool IsValidSection(string value)
        {
            return IsValidPart(value) &&
                value.IndexOf('[') < 0 &&
                value.IndexOf(']') < 0;
        }

        internal static bool IsValidKey(string value)
        {
            return IsValidPart(value) &&
                value.IndexOf('=') < 0 &&
                value.IndexOf('[') < 0 &&
                value.IndexOf(']') < 0 &&
                value[0] != '#';
        }

        private static bool IsValidPart(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                return false;
            }
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsControl(value[index]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
