using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DSPPluginManager.Configuration;
using DSPPluginManager.Contracts;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.UnityHost
{
    internal sealed class PluginConfigurationService
    {
        private readonly string identifier;
        private readonly string filePath;
        private readonly bool writeBlocked;
        private readonly PluginConfigurationDocument document;
        private readonly Action<string> warning;
        private readonly IPluginConfigurationPersistence persistence;
        private readonly Dictionary<ConfigurationDefinition, Binding> bindings;
        private readonly object sync = new object();
        private int mutationVersion;
        private int requestedPersistenceVersion;
        private int persistedVersion;

        internal PluginConfigurationService(
            PluginConfigurationScope scope,
            PluginConfigurationDocument document,
            Action<string> warning
        ) : this(
            scope,
            document,
            warning,
            new PluginConfigurationPersistence()
        )
        {
        }

        internal PluginConfigurationService(
            PluginConfigurationScope scope,
            PluginConfigurationDocument document,
            Action<string> warning,
            IPluginConfigurationPersistence persistence
        )
        {
            if (scope == null)
            {
                throw new ArgumentNullException("scope");
            }
            identifier = scope.Identifier;
            filePath = scope.FilePath;
            writeBlocked = !scope.IsUsable;
            this.document = document ?? throw new ArgumentNullException(
                "document"
            );
            this.warning = warning ?? throw new ArgumentNullException("warning");
            this.persistence = persistence ?? throw new ArgumentNullException(
                "persistence"
            );
            bindings = new Dictionary<ConfigurationDefinition, Binding>();
            Handle = new PluginConfiguration(
                BindBoolean,
                BindString,
                BindShortcut,
                Save
            );
        }

        internal PluginConfiguration Handle { get; }

        internal int MutationVersion
        {
            get
            {
                lock (sync)
                {
                    return mutationVersion;
                }
            }
        }

        internal int RequestedPersistenceVersion
        {
            get
            {
                lock (sync)
                {
                    return requestedPersistenceVersion;
                }
            }
        }

        internal int PersistedVersion
        {
            get
            {
                lock (sync)
                {
                    return persistedVersion;
                }
            }
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
                TryParseBoolean,
                SerializeBoolean
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
                TryParseString,
                SerializeString
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
                KeyboardShortcut.TryParse,
                value => value.ToPersistedString()
            );
        }

        private PluginConfigurationEntry<T> Bind<T>(
            string section,
            string key,
            T defaultValue,
            string description,
            string expectedType,
            TryParseValue<T> parse,
            Func<T, string> serialize
        )
        {
            lock (sync)
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
                    definition,
                    expectedType,
                    defaultValue,
                    description,
                    value,
                    serialize,
                    sync,
                    OnChanged
                );
                bindings.Add(definition, created);
                RequestPersistence("new binding");
                return created.Entry;
            }
        }

        private void OnChanged()
        {
            checked
            {
                mutationVersion++;
            }
            RequestPersistence("changed value");
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

        private void Save()
        {
            lock (sync)
            {
                RequestPersistence("explicit save");
            }
        }

        private void RequestPersistence(string reason)
        {
            checked
            {
                requestedPersistenceVersion++;
            }
            int requested = requestedPersistenceVersion;
            if (writeBlocked)
            {
                WarnPersistence(
                    reason,
                    requested,
                    "source read was unavailable; writes are blocked for " +
                    "this process",
                    null
                );
                return;
            }

            ConfigurationPersistenceResult result = persistence.Save(
                filePath,
                SerializeSnapshot()
            );
            if (result.Succeeded)
            {
                persistedVersion = requested;
                return;
            }

            WarnPersistence(
                reason,
                requested,
                result.FailureStage.ToString(),
                result.Failure
            );
        }

        private string SerializeSnapshot()
        {
            List<SnapshotEntry> entries = document.Entries
                .Select(entry => new SnapshotEntry(
                    entry.Section,
                    entry.Key,
                    null,
                    entry.SerializedValue
                ))
                .ToList();
            entries.AddRange(bindings.Values.Select(binding =>
                new SnapshotEntry(
                    binding.Definition.Section,
                    binding.Definition.Key,
                    binding.Description,
                    binding.SerializeValue()
                )
            ));

            StringBuilder result = new StringBuilder();
            string currentSection = null;
            foreach (SnapshotEntry entry in entries
                .OrderBy(value => value.Section, StringComparer.Ordinal)
                .ThenBy(value => value.Key, StringComparer.Ordinal))
            {
                if (!string.Equals(
                        currentSection,
                        entry.Section,
                        StringComparison.Ordinal
                    ))
                {
                    if (result.Length != 0)
                    {
                        result.AppendLine();
                    }
                    currentSection = entry.Section;
                    result.Append('[');
                    result.Append(currentSection);
                    result.AppendLine("]");
                }
                if (entry.Description != null)
                {
                    result.Append("# ");
                    result.AppendLine(entry.Description);
                }
                result.Append(entry.Key);
                result.Append(" = ");
                result.AppendLine(entry.SerializedValue);
            }
            return result.ToString();
        }

        private void WarnPersistence(
            string reason,
            int requested,
            string stage,
            Exception failure
        )
        {
            string message =
                "Plugin '" + identifier + "' configuration persistence " +
                "request " + requested + " (" + reason + ") failed at " +
                stage + "; persisted version remains " + persistedVersion +
                ". In-memory values remain usable.";
            if (failure != null)
            {
                message += " " + failure.GetType().FullName + ": " +
                    failure.Message;
            }
            try
            {
                warning(message);
            }
            catch
            {
            }
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
            protected Binding(
                ConfigurationDefinition definition,
                string expectedType,
                string description
            )
            {
                Definition = definition;
                ExpectedType = expectedType;
                Description = description;
            }

            internal ConfigurationDefinition Definition { get; }

            internal string ExpectedType { get; }

            internal string Description { get; }

            internal abstract string SerializeValue();
        }

        private sealed class TypedBinding<T> : Binding
        {
            private readonly Func<T, string> serialize;
            private readonly object sync;
            private readonly Action changed;
            private T value;

            internal TypedBinding(
                ConfigurationDefinition definition,
                string expectedType,
                T defaultValue,
                string description,
                T value,
                Func<T, string> serialize,
                object sync,
                Action changed
            ) : base(definition, expectedType, description)
            {
                DefaultValue = defaultValue;
                this.value = value;
                this.serialize = serialize;
                this.sync = sync;
                this.changed = changed;
                Entry = new PluginConfigurationEntry<T>(Read, Write);
            }

            internal T DefaultValue { get; }

            internal PluginConfigurationEntry<T> Entry { get; }

            internal override string SerializeValue()
            {
                lock (sync)
                {
                    return serialize(value);
                }
            }

            private T Read()
            {
                lock (sync)
                {
                    return value;
                }
            }

            private void Write(T next)
            {
                lock (sync)
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

        private sealed class SnapshotEntry
        {
            internal SnapshotEntry(
                string section,
                string key,
                string description,
                string serializedValue
            )
            {
                Section = section;
                Key = key;
                Description = description;
                SerializedValue = serializedValue;
            }

            internal string Section { get; }

            internal string Key { get; }

            internal string Description { get; }

            internal string SerializedValue { get; }
        }
    }
}
