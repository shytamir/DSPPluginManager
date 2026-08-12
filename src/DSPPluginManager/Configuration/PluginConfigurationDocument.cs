using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DSPPluginManager.Configuration
{
    internal sealed class PluginConfigurationDocument
    {
        private readonly Dictionary<ConfigurationDefinition, StoredValue>
            values;
        private readonly ReadOnlyCollection<ConfigurationDocumentDiagnostic>
            diagnostics;

        private PluginConfigurationDocument(
            Dictionary<ConfigurationDefinition, StoredValue> values,
            ConfigurationDocumentDiagnostic[] diagnostics
        )
        {
            this.values = values;
            this.diagnostics = Array.AsReadOnly(diagnostics);
        }

        internal int Count
        {
            get { return values.Count; }
        }

        internal IReadOnlyList<ConfigurationDocumentDiagnostic> Diagnostics
        {
            get { return diagnostics; }
        }

        internal IReadOnlyList<ConfigurationSerializedEntry> Entries
        {
            get
            {
                return values
                    .OrderBy(pair => pair.Key.Section, StringComparer.Ordinal)
                    .ThenBy(pair => pair.Key.Key, StringComparer.Ordinal)
                    .Select(pair => new ConfigurationSerializedEntry(
                        pair.Key.Section,
                        pair.Key.Key,
                        pair.Value.SerializedValue
                    ))
                    .ToArray();
            }
        }

        internal bool TryGetSerializedValue(
            string section,
            string key,
            out string serializedValue
        )
        {
            ConfigurationDefinition definition =
                new ConfigurationDefinition(section, key);
            StoredValue stored;
            if (values.TryGetValue(definition, out stored))
            {
                serializedValue = stored.SerializedValue;
                return true;
            }

            serializedValue = null;
            return false;
        }

        internal bool TryClaimSerializedValue(
            string section,
            string key,
            out string serializedValue
        )
        {
            ConfigurationDefinition definition =
                new ConfigurationDefinition(section, key);
            StoredValue stored;
            if (values.TryGetValue(definition, out stored))
            {
                values.Remove(definition);
                serializedValue = stored.SerializedValue;
                return true;
            }

            serializedValue = null;
            return false;
        }

        internal static PluginConfigurationDocument Parse(string contents)
        {
            if (contents == null)
            {
                throw new ArgumentNullException("contents");
            }
            using (StringReader reader = new StringReader(contents))
            {
                return Parse(reader);
            }
        }

        internal static PluginConfigurationDocument Parse(TextReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }

            Dictionary<ConfigurationDefinition, StoredValue> values =
                new Dictionary<ConfigurationDefinition, StoredValue>();
            List<ConfigurationDocumentDiagnostic> diagnostics =
                new List<ConfigurationDocumentDiagnostic>();
            string currentSection = null;
            string line;
            int lineNumber = 0;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    continue;
                }

                if (trimmed[0] == '[')
                {
                    currentSection = ParseSection(
                        trimmed,
                        lineNumber,
                        line,
                        diagnostics
                    );
                    continue;
                }

                int equals = trimmed.IndexOf('=');
                if (equals < 0)
                {
                    diagnostics.Add(Diagnostic(
                        ConfigurationDocumentDiagnosticCode.MalformedEntry,
                        lineNumber,
                        line,
                        "The entry does not contain an assignment."
                    ));
                    continue;
                }
                if (currentSection == null)
                {
                    diagnostics.Add(Diagnostic(
                        ConfigurationDocumentDiagnosticCode.EntryWithoutSection,
                        lineNumber,
                        line,
                        "The entry has no valid section."
                    ));
                    continue;
                }

                string key = trimmed.Substring(0, equals).Trim();
                string serializedValue = trimmed.Substring(equals + 1).Trim();
                if (!ConfigurationDefinition.IsValidKey(key))
                {
                    diagnostics.Add(Diagnostic(
                        ConfigurationDocumentDiagnosticCode.InvalidDefinition,
                        lineNumber,
                        line,
                        "The entry key is invalid."
                    ));
                    continue;
                }

                ConfigurationDefinition definition =
                    new ConfigurationDefinition(currentSection, key);
                StoredValue previous;
                if (values.TryGetValue(definition, out previous))
                {
                    diagnostics.Add(Diagnostic(
                        ConfigurationDocumentDiagnosticCode.DuplicateDefinition,
                        lineNumber,
                        line,
                        "The definition replaces its value from line " +
                        previous.LineNumber + "."
                    ));
                }
                values[definition] = new StoredValue(
                    serializedValue,
                    lineNumber
                );
            }

            return new PluginConfigurationDocument(
                values,
                diagnostics.ToArray()
            );
        }

        private static string ParseSection(
            string trimmed,
            int lineNumber,
            string line,
            List<ConfigurationDocumentDiagnostic> diagnostics
        )
        {
            if (trimmed.Length < 2 ||
                trimmed[trimmed.Length - 1] != ']')
            {
                diagnostics.Add(Diagnostic(
                    ConfigurationDocumentDiagnosticCode.MalformedSection,
                    lineNumber,
                    line,
                    "The section header is not closed."
                ));
                return null;
            }

            string section = trimmed.Substring(1, trimmed.Length - 2);
            if (!ConfigurationDefinition.IsValidSection(section))
            {
                diagnostics.Add(Diagnostic(
                    ConfigurationDocumentDiagnosticCode.InvalidDefinition,
                    lineNumber,
                    line,
                    "The section name is invalid."
                ));
                return null;
            }
            return section;
        }

        private static ConfigurationDocumentDiagnostic Diagnostic(
            ConfigurationDocumentDiagnosticCode code,
            int lineNumber,
            string line,
            string detail
        )
        {
            return new ConfigurationDocumentDiagnostic(
                code,
                lineNumber,
                line,
                detail
            );
        }

        private sealed class StoredValue
        {
            internal StoredValue(string serializedValue, int lineNumber)
            {
                SerializedValue = serializedValue;
                LineNumber = lineNumber;
            }

            internal string SerializedValue { get; }

            internal int LineNumber { get; }
        }
    }

    internal sealed class ConfigurationSerializedEntry
    {
        internal ConfigurationSerializedEntry(
            string section,
            string key,
            string serializedValue
        )
        {
            Section = section ?? throw new ArgumentNullException("section");
            Key = key ?? throw new ArgumentNullException("key");
            SerializedValue = serializedValue ??
                throw new ArgumentNullException("serializedValue");
        }

        internal string Section { get; }

        internal string Key { get; }

        internal string SerializedValue { get; }
    }
}
