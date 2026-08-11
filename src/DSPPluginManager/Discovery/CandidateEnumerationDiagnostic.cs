using System;

namespace DSPPluginManager.Discovery
{
    internal enum CandidateEnumerationDiagnosticCode
    {
        UnreadableEntry,
        OutsideRootLink
    }

    internal sealed class CandidateEnumerationDiagnostic
    {
        internal CandidateEnumerationDiagnostic(
            CandidateEnumerationDiagnosticCode code,
            string path,
            string detail
        )
        {
            Code = code;
            Path = path ?? throw new ArgumentNullException("path");
            Detail = detail ?? throw new ArgumentNullException("detail");
        }

        internal CandidateEnumerationDiagnosticCode Code { get; }

        internal string Path { get; }

        internal string Detail { get; }
    }
}
