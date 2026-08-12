using System;

namespace DSPPluginManager.Configuration
{
    internal enum ConfigurationDocumentDiagnosticCode
    {
        MalformedSection,
        MalformedEntry,
        EntryWithoutSection,
        InvalidDefinition,
        DuplicateDefinition
    }

    internal sealed class ConfigurationDocumentDiagnostic
    {
        internal ConfigurationDocumentDiagnostic(
            ConfigurationDocumentDiagnosticCode code,
            int lineNumber,
            string lineText,
            string detail
        )
        {
            if (lineNumber < 1)
            {
                throw new ArgumentOutOfRangeException("lineNumber");
            }

            Code = code;
            LineNumber = lineNumber;
            LineText = lineText ?? throw new ArgumentNullException("lineText");
            Detail = detail ?? throw new ArgumentNullException("detail");
        }

        internal ConfigurationDocumentDiagnosticCode Code { get; }

        internal int LineNumber { get; }

        internal string LineText { get; }

        internal string Detail { get; }
    }
}
