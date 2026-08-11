using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPPluginManager.Discovery
{
    internal enum CandidateReconciliationState
    {
        Selected,
        Redundant,
        Superseded,
        Ambiguous,
        Rejected
    }

    internal enum CandidateReconciliationDiagnosticCode
    {
        RedundantCopy,
        SupersededVersion,
        AmbiguousIdentity,
        InspectionRejected
    }

    internal sealed class CandidateReconciliationDiagnostic
    {
        internal CandidateReconciliationDiagnostic(
            CandidateReconciliationDiagnosticCode code,
            string assemblyPath,
            string detail
        )
        {
            Code = code;
            AssemblyPath = assemblyPath ??
                throw new ArgumentNullException("assemblyPath");
            Detail = detail ?? throw new ArgumentNullException("detail");
        }

        internal CandidateReconciliationDiagnosticCode Code { get; }

        internal string AssemblyPath { get; }

        internal string Detail { get; }
    }

    internal sealed class CandidateReconciliationEntry
    {
        internal CandidateReconciliationEntry(
            CandidateReconciliationState state,
            RecognizedPluginCandidate candidate,
            PluginInspectionDiagnostic inspectionDiagnostic,
            CandidateReconciliationDiagnostic diagnostic
        )
        {
            bool rejected = state == CandidateReconciliationState.Rejected;
            bool selected = state == CandidateReconciliationState.Selected;
            if (rejected &&
                (candidate != null || inspectionDiagnostic == null ||
                 diagnostic == null))
            {
                throw new ArgumentException(
                    "A rejected entry requires inspection and reconciliation " +
                    "diagnostics but no candidate."
                );
            }
            if (selected &&
                (candidate == null || inspectionDiagnostic != null ||
                 diagnostic != null))
            {
                throw new ArgumentException(
                    "A selected entry requires only a candidate."
                );
            }
            if (!rejected && !selected &&
                (candidate == null || inspectionDiagnostic != null ||
                 diagnostic == null))
            {
                throw new ArgumentException(
                    "A classified non-selected entry requires a candidate " +
                    "and reconciliation diagnostic."
                );
            }
            State = state;
            Candidate = candidate;
            InspectionDiagnostic = inspectionDiagnostic;
            Diagnostic = diagnostic;
        }

        internal CandidateReconciliationState State { get; }

        internal RecognizedPluginCandidate Candidate { get; }

        internal PluginInspectionDiagnostic InspectionDiagnostic { get; }

        internal CandidateReconciliationDiagnostic Diagnostic { get; }
    }

    internal sealed class CandidateReconciliationResult
    {
        internal CandidateReconciliationResult(
            CandidateReconciliationEntry[] entries
        )
        {
            if (entries == null)
            {
                throw new ArgumentNullException("entries");
            }
            Entries = Array.AsReadOnly(
                (CandidateReconciliationEntry[])entries.Clone()
            );
            SelectedCandidates = Array.AsReadOnly(entries
                .Where(entry =>
                    entry.State == CandidateReconciliationState.Selected
                )
                .Select(entry => entry.Candidate)
                .ToArray());
            Diagnostics = Array.AsReadOnly(entries
                .Where(entry => entry.Diagnostic != null)
                .Select(entry => entry.Diagnostic)
                .ToArray());
        }

        internal IReadOnlyList<CandidateReconciliationEntry> Entries { get; }

        internal IReadOnlyList<RecognizedPluginCandidate> SelectedCandidates
        {
            get;
        }

        internal IReadOnlyList<CandidateReconciliationDiagnostic> Diagnostics
        {
            get;
        }
    }
}
