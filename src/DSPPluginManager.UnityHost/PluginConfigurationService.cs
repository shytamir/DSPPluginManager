using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DSPPluginManager.Configuration;
using DSPPluginManager.Contracts;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.UnityHost
{
    internal sealed class PluginConfigurationService
    {
        private readonly string identifier;
        private readonly PluginConfigurationDocument document;
        private readonly Action<string> warning;
        private readonly Dictionary<ConfigurationDefinition, Binding> bindings;
        private int mutationVersion;

        internal PluginConfigurationService(
            string identifier,
            PluginConfigurationDocument document,
            Action<string> warning
        )
        {
            if (!PluginContractRules.IsValidIdentifier(identifier))
            {
                throw new ArgumentException(
                    "Plugin identifier is invalid.",
                    "identifier"
                );
            }
            this.identifier = identifier.ToLowerInvariant();
            this.document = document ?? throw new ArgumentNullException(
                "document"
            );
            this.warning = warning ?? throw new ArgumentNullException("warning");
            bindings = new Dictionary<ConfigurationDefinition, Binding>();
            Handle = new PluginConfiguration(
                BindBoolean,
                BindString,
                BindShortcut,
                SaveBeforePersistenceExists
            );
        }

        internal PluginConfiguration Handle { get; }

        internal int MutationVersion
        {
            get { return mutationVersion; }
        }

        private PluginConfigurationEntry<bool> BindBoolean(
            string section,
            string key,
            bool defaultValue,
            string description
        )
        {
            return Bind(
                section,
                key,
                defaultValue,
                description,
                "Boolean",
                TryParseBoolean
            );
        }

        private PluginConfigurationEntry<string> BindString(
            string section,
            string key,
            string defaultValue,
            string description
        )
        {
            return Bind(
                section,
                key,
                defaultValue,
                description,
                "String",
                TryParseString
            );
        }

        private PluginConfigurationEntry<KeyboardShortcut> BindShortcut(
            string section,
            string key,
            KeyboardShortcut defaultValue,
            string description
        )
        {
            return Bind(
                section,
                key,
                defaultValue,
                description,
                "KeyboardShortcut",
                KeyboardShortcut.TryParse
            );
        }

        private PluginConfigurationEntry<T> Bind<T>(
            string section,
            string key,
            T defaultValue,
            string description,
            string expectedType,
            TryParseValue<T> parse
        )
        {
            ConfigurationDefinition definition =
                new ConfigurationDefinition(section, key);
            Binding existing;
            if (bindings.TryGetValue(definition, out existing))
            {
                TypedBinding<T> matching = existing as TypedBinding<T>;
                if (matching == null)
                {
                    throw new InvalidOperationException(
                        "Plugin '" + identifier + "' configuration [" +
                        section + "] " + key + " was first bound as " +
                        existing.ExpectedType + " and cannot be rebound as " +
                        expectedType + "."
                    );
                }
                return matching.Entry;
            }

            T value = defaultValue;
            string serialized;
            if (document.TryClaimSerializedValue(
                    section,
                    key,
                    out serialized
                ))
            {
                T parsed;
                if (parse(serialized, out parsed))
                {
                    value = parsed;
                }
                else
                {
                    Warn(
                        section,
                        key,
                        expectedType,
                        "stored value '" + serialized + "' is malformed"
                    );
                }
            }

            TypedBinding<T> created = new TypedBinding<T>(
                expectedType,
                defaultValue,
                description,
                value,
                OnChanged
            );
            bindings.Add(definition, created);
            return created.Entry;
        }

        private void OnChanged()
        {
            checked
            {
                mutationVersion++;
            }
        }

        private void Warn(
            string section,
            string key,
            string expectedType,
            string reason
        )
        {
            try
            {
                warning(
                    "Plugin '" + identifier + "' configuration [" + section +
                    "] " + key + " expected " + expectedType + ": " + reason +
                    ". The supplied default remains active."
                );
            }
            catch
            {
            }
        }

        private static void SaveBeforePersistenceExists()
        {
        }

        private static bool TryParseBoolean(string value, out bool parsed)
        {
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
            {
                parsed = true;
                return true;
            }
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
            {
                parsed = false;
                return true;
            }
            parsed = false;
            return false;
        }

        private static bool TryParseString(string value, out string parsed)
        {
            StringBuilder result = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (current != '\\')
                {
                    result.Append(current);
                    continue;
                }
                if (++index >= value.Length)
                {
                    parsed = null;
                    return false;
                }
                switch (value[index])
                {
                    case '\\': result.Append('\\'); break;
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case 'u':
                        if (index + 4 >= value.Length)
                        {
                            parsed = null;
                            return false;
                        }
                        int code;
                        if (!int.TryParse(
                                value.Substring(index + 1, 4),
                                NumberStyles.AllowHexSpecifier,
                                CultureInfo.InvariantCulture,
                                out code
                            ))
                        {
                            parsed = null;
                            return false;
                        }
                        result.Append((char)code);
                        index += 4;
                        break;
                    default:
                        parsed = null;
                        return false;
                }
            }
            parsed = result.ToString();
            return true;
        }

        internal static string SerializeBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        internal static string SerializeString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            StringBuilder result = new StringBuilder(value.Length);
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                switch (current)
                {
                    case '\\': result.Append("\\\\"); break;
                    case '\n': result.Append("\\n"); break;
                    case '\r': result.Append("\\r"); break;
                    case '\t': result.Append("\\t"); break;
                    default:
                        if (char.IsControl(current) ||
                            char.IsSurrogate(current) ||
                            (char.IsWhiteSpace(current) &&
                                (index == 0 || index == value.Length - 1)))
                        {
                            result.Append("\\u");
                            result.Append(((int)current).ToString(
                                "X4",
                                CultureInfo.InvariantCulture
                            ));
                        }
                        else
                        {
                            result.Append(current);
                        }
                        break;
                }
            }
            return result.ToString();
        }

        private delegate bool TryParseValue<T>(string value, out T parsed);

        private abstract class Binding
        {
            protected Binding(string expectedType)
            {
                ExpectedType = expectedType;
            }

            internal string ExpectedType { get; }
        }

        private sealed class TypedBinding<T> : Binding
        {
            private readonly Action changed;
            private T value;

            internal TypedBinding(
                string expectedType,
                T defaultValue,
                string description,
                T value,
                Action changed
            ) : base(expectedType)
            {
                DefaultValue = defaultValue;
                Description = description;
                this.value = value;
                this.changed = changed;
                Entry = new PluginConfigurationEntry<T>(Read, Write);
            }

            internal T DefaultValue { get; }

            internal string Description { get; }

            internal PluginConfigurationEntry<T> Entry { get; }

            private T Read()
            {
                return value;
            }

            private void Write(T next)
            {
                if (EqualityComparer<T>.Default.Equals(value, next))
                {
                    return;
                }
                value = next;
                changed();
            }
        }
    }
}
