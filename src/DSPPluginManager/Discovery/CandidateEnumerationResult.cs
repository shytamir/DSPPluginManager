using System;
using System.Collections.Generic;

namespace DSPPluginManager.Discovery
{
    internal sealed class CandidateEnumerationResult
    {
        internal CandidateEnumerationResult(
            string[] candidatePaths,
            CandidateEnumerationDiagnostic[] diagnostics
        )
        {
            if (candidatePaths == null)
            {
                throw new ArgumentNullException("candidatePaths");
            }
            if (diagnostics == null)
            {
                throw new ArgumentNullException("diagnostics");
            }

            CandidatePaths = Array.AsReadOnly(
                (string[])candidatePaths.Clone()
            );
            Diagnostics = Array.AsReadOnly(
                (CandidateEnumerationDiagnostic[])diagnostics.Clone()
            );
        }

        internal IReadOnlyList<string> CandidatePaths { get; }

        internal IReadOnlyList<CandidateEnumerationDiagnostic> Diagnostics
        {
            get;
        }
    }
}
