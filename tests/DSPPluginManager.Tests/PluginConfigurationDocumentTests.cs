using System;
using System.Linq;
using DSPPluginManager.Configuration;

namespace DSPPluginManager.Tests
{
    internal static class PluginConfigurationDocumentTests
    {
        internal static void Run()
        {
            VerifyDefinitionIdentityAndValidation();
            VerifyConsumerShapedLateEntriesRemainUnclaimed();
            VerifyMalformedAndDuplicateDiagnostics();
            VerifyDeterministicConstruction();
        }

        private static void VerifyDefinitionIdentityAndValidation()
        {
            ConfigurationDefinition upper = new ConfigurationDefinition(
                "General",
                "Enabled"
            );
            ConfigurationDefinition same = new ConfigurationDefinition(
                "General",
                "Enabled"
            );
            ConfigurationDefinition sectionCase = new ConfigurationDefinition(
                "general",
                "Enabled"
            );
            ConfigurationDefinition keyCase = new ConfigurationDefinition(
                "General",
                "enabled"
            );
            TestAssert.True(upper.Equals(same),
                "Equal configuration definitions did not compare equal.");
            TestAssert.Equal(upper.GetHashCode(), same.GetHashCode(),
                "equal definition hash");
            TestAssert.True(!upper.Equals(sectionCase),
                "Section identity became case-insensitive.");
            TestAssert.True(!upper.Equals(keyCase),
                "Key identity became case-insensitive.");

            new ConfigurationDefinition(
                "Phase Selection",
                "save2-0123456789abcdef"
            );
            foreach (string section in new[]
            {
                null, string.Empty, " General", "General ",
                "General\r\nInjected", "[General]", "General\tInjected"
            })
            {
                string captured = section;
                TestAssert.Throws<ArgumentException>(
                    () => new ConfigurationDefinition(captured, "Key"),
                    "section",
                    "invalid"
                );
            }
            foreach (string key in new[]
            {
                null, string.Empty, " Key", "Key ", "Key=Injected",
                "[Injected]", "# Comment", "Key\r\nInjected",
                "Key\tInjected"
            })
            {
                string captured = key;
                TestAssert.Throws<ArgumentException>(
                    () => new ConfigurationDefinition("General", captured),
                    "key",
                    "invalid"
                );
            }
        }

        private static void VerifyConsumerShapedLateEntriesRemainUnclaimed()
        {
            string contents =
                "# Fixture configuration\r\n" +
                "\r\n" +
                "[General]\r\n" +
                "Enabled = true\r\n" +
                "Shortcut = F8\r\n" +
                "\r\n" +
                "[Phase Selection]\r\n" +
                "save2-alpha = nav2;phase=3;seed=123=456\r\n" +
                "save-alpha = legacy-value\r\n" +
                "[general]\r\n" +
                "Enabled = false\r\n";

            PluginConfigurationDocument document =
                PluginConfigurationDocument.Parse(contents);
            TestAssert.Equal(5, document.Count, "consumer-shaped entry count");
            TestAssert.Equal(0, document.Diagnostics.Count,
                "consumer-shaped diagnostics");

            AssertValue(document, "General", "Enabled", "true");
            AssertValue(document, "General", "Shortcut", "F8");
            AssertValue(document, "general", "Enabled", "false");
            TestAssert.Equal(5, document.Count,
                "fixed observation claimed unrelated definitions");

            AssertValue(
                document,
                "Phase Selection",
                "save2-alpha",
                "nav2;phase=3;seed=123=456"
            );
            AssertValue(
                document,
                "Phase Selection",
                "save-alpha",
                "legacy-value"
            );
            TestAssert.Equal(5, document.Count,
                "late observation removed a definition");
        }

        private static void VerifyMalformedAndDuplicateDiagnostics()
        {
            string contents =
                "orphan = ignored\n" +
                "[Broken\n" +
                "after-broken = ignored\n" +
                "[]\n" +
                "after-empty = ignored\n" +
                "[Valid]\n" +
                "missing assignment\n" +
                "bad[key = ignored\n" +
                "Duplicate = first\n" +
                "Duplicate = second\n" +
                "Survivor = retained\n";

            PluginConfigurationDocument document =
                PluginConfigurationDocument.Parse(contents);
            TestAssert.Equal(2, document.Count, "malformed fixture entries");
            AssertValue(document, "Valid", "Duplicate", "second");
            AssertValue(document, "Valid", "Survivor", "retained");

            ConfigurationDocumentDiagnosticCode[] expectedCodes =
            {
                ConfigurationDocumentDiagnosticCode.EntryWithoutSection,
                ConfigurationDocumentDiagnosticCode.MalformedSection,
                ConfigurationDocumentDiagnosticCode.EntryWithoutSection,
                ConfigurationDocumentDiagnosticCode.InvalidDefinition,
                ConfigurationDocumentDiagnosticCode.EntryWithoutSection,
                ConfigurationDocumentDiagnosticCode.MalformedEntry,
                ConfigurationDocumentDiagnosticCode.InvalidDefinition,
                ConfigurationDocumentDiagnosticCode.DuplicateDefinition
            };
            TestAssert.Equal(expectedCodes.Length, document.Diagnostics.Count,
                "malformed diagnostic count");
            for (int index = 0; index < expectedCodes.Length; index++)
            {
                ConfigurationDocumentDiagnostic diagnostic =
                    document.Diagnostics[index];
                TestAssert.Equal(expectedCodes[index], diagnostic.Code,
                    "diagnostic code " + index);
                TestAssert.True(diagnostic.LineNumber > 0,
                    "Diagnostic omitted its line number.");
                TestAssert.True(diagnostic.LineText.Length > 0,
                    "Diagnostic omitted its line text.");
                TestAssert.True(diagnostic.Detail.Length > 0,
                    "Diagnostic omitted its detail.");
            }
            ConfigurationDocumentDiagnostic duplicate =
                document.Diagnostics.Last();
            TestAssert.Equal(10, duplicate.LineNumber,
                "duplicate diagnostic line");
            TestAssert.Equal("Duplicate = second", duplicate.LineText,
                "duplicate diagnostic context");
            TestAssert.True(
                duplicate.Detail.IndexOf("line 9", StringComparison.Ordinal) >= 0,
                "Duplicate diagnostic omitted the replaced line."
            );
        }

        private static void VerifyDeterministicConstruction()
        {
            string contents =
                "[z]\n" +
                "b = 2\n" +
                "a = 1\n" +
                "[a]\n" +
                "z = 3\n" +
                "bad line\n" +
                "z = 4\n";
            PluginConfigurationDocument first =
                PluginConfigurationDocument.Parse(contents);
            PluginConfigurationDocument second =
                PluginConfigurationDocument.Parse(contents);

            TestAssert.Equal(
                SerializeEntries(first),
                SerializeEntries(second),
                "repeated document state"
            );
            TestAssert.Equal(
                "a|z|4\nz|a|1\nz|b|2",
                SerializeEntries(first),
                "ordinal document state"
            );
            TestAssert.Equal(
                SerializeDiagnostics(first),
                SerializeDiagnostics(second),
                "repeated diagnostics"
            );
        }

        private static void AssertValue(
            PluginConfigurationDocument document,
            string section,
            string key,
            string expected
        )
        {
            string actual;
            TestAssert.True(
                document.TryGetSerializedValue(section, key, out actual),
                "Definition was not retained: " + section + "/" + key
            );
            TestAssert.Equal(expected, actual,
                "serialized value " + section + "/" + key);
        }

        private static string SerializeEntries(
            PluginConfigurationDocument document
        )
        {
            return string.Join("\n", document.Entries.Select(entry =>
                entry.Section + "|" + entry.Key + "|" +
                entry.SerializedValue
            ));
        }

        private static string SerializeDiagnostics(
            PluginConfigurationDocument document
        )
        {
            return string.Join("\n", document.Diagnostics.Select(diagnostic =>
                diagnostic.Code + "|" + diagnostic.LineNumber + "|" +
                diagnostic.LineText + "|" + diagnostic.Detail
            ));
        }
    }
}
