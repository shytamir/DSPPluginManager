using System;
using System.Collections.Generic;
using System.Linq;

namespace DSPPluginManager.Discovery
{
    internal sealed class CandidateReconciler
    {
        internal CandidateReconciliationResult Reconcile(
            IEnumerable<PluginInspectionResult> inspectionResults
        )
        {
            if (inspectionResults == null)
            {
                throw new ArgumentNullException("inspectionResults");
            }
            PluginInspectionResult[] inputs = inspectionResults.ToArray();
            if (inputs.Any(result => result == null))
            {
                throw new ArgumentException(
                    "Inspection results cannot contain null entries.",
                    "inspectionResults"
                );
            }

            List<CandidateReconciliationEntry> entries =
                new List<CandidateReconciliationEntry>();
            IEnumerable<IGrouping<string, RecognizedPluginCandidate>> groups =
                inputs.Where(result => result.IsRecognized)
                    .Select(result => result.Candidate)
                    .GroupBy(
                        candidate => candidate.IdentifierComparisonKey,
                        StringComparer.OrdinalIgnoreCase
                    )
                    .OrderBy(group => group.Key, StringComparer.Ordinal);
            foreach (IGrouping<string, RecognizedPluginCandidate> group in groups)
            {
                ReconcileIdentity(group.ToArray(), entries);
            }

            foreach (PluginInspectionDiagnostic rejection in inputs
                .Where(result => !result.IsRecognized)
                .Select(result => result.Diagnostic)
                .OrderBy(diagnostic => diagnostic.AssemblyPath, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code)
                .ThenBy(diagnostic => diagnostic.Detail, StringComparer.Ordinal))
            {
                CandidateReconciliationDiagnostic diagnostic =
                    new CandidateReconciliationDiagnostic(
                        CandidateReconciliationDiagnosticCode.InspectionRejected,
                        rejection.AssemblyPath,
                        rejection.Code + ": " + rejection.Detail
                    );
                entries.Add(new CandidateReconciliationEntry(
                    CandidateReconciliationState.Rejected,
                    null,
                    rejection,
                    diagnostic
                ));
            }

            return new CandidateReconciliationResult(entries.ToArray());
        }

        private static void ReconcileIdentity(
            RecognizedPluginCandidate[] candidates,
            List<CandidateReconciliationEntry> entries
        )
        {
            RecognizedPluginCandidate[] ordered = candidates
                .OrderByDescending(candidate => candidate.Version)
                .ThenBy(candidate => candidate.AssemblyPath, StringComparer.Ordinal)
                .ToArray();
            IGrouping<Version, RecognizedPluginCandidate>[] versionGroups = ordered
                .GroupBy(candidate => candidate.Version)
                .OrderByDescending(group => group.Key)
                .ToArray();
            IGrouping<Version, RecognizedPluginCandidate> conflict =
                versionGroups.FirstOrDefault(group => group
                    .Select(Fingerprint)
                    .Distinct(StringComparer.Ordinal)
                    .Skip(1)
                    .Any());
            if (conflict != null)
            {
                string conflictPaths = string.Join(", ", conflict
                    .Select(candidate => candidate.AssemblyPath)
                    .OrderBy(path => path, StringComparer.Ordinal));
                string detail = "Version " + conflict.Key.ToString(3) +
                    " has conflicting content, type, or assembly identity at " +
                    conflictPaths + ". The entire identifier group is rejected.";
                foreach (RecognizedPluginCandidate candidate in ordered)
                {
                    Add(
                        entries,
                        CandidateReconciliationState.Ambiguous,
                        CandidateReconciliationDiagnosticCode.AmbiguousIdentity,
                        candidate,
                        detail
                    );
                }
                return;
            }

            Version highestVersion = versionGroups[0].Key;
            RecognizedPluginCandidate selected = versionGroups[0]
                .OrderBy(candidate => candidate.AssemblyPath, StringComparer.Ordinal)
                .First();
            foreach (IGrouping<Version, RecognizedPluginCandidate> versionGroup in
                versionGroups)
            {
                RecognizedPluginCandidate[] sameVersion = versionGroup
                    .OrderBy(
                        candidate => candidate.AssemblyPath,
                        StringComparer.Ordinal
                    )
                    .ToArray();
                RecognizedPluginCandidate retained = sameVersion[0];
                if (versionGroup.Key.Equals(highestVersion))
                {
                    entries.Add(new CandidateReconciliationEntry(
                        CandidateReconciliationState.Selected,
                        retained,
                        null,
                        null
                    ));
                }
                else
                {
                    Add(
                        entries,
                        CandidateReconciliationState.Superseded,
                        CandidateReconciliationDiagnosticCode.SupersededVersion,
                        retained,
                        "Version " + retained.Version.ToString(3) +
                            " is superseded by " + highestVersion.ToString(3) +
                            " at '" + selected.AssemblyPath + "'."
                    );
                }
                foreach (RecognizedPluginCandidate redundant in
                    sameVersion.Skip(1))
                {
                    Add(
                        entries,
                        CandidateReconciliationState.Redundant,
                        CandidateReconciliationDiagnosticCode.RedundantCopy,
                        redundant,
                        "Byte-identical version " +
                            versionGroup.Key.ToString(3) +
                            " is retained at ordinal path '" +
                            retained.AssemblyPath + "'."
                    );
                }
            }
        }

        private static void Add(
            List<CandidateReconciliationEntry> entries,
            CandidateReconciliationState state,
            CandidateReconciliationDiagnosticCode code,
            RecognizedPluginCandidate candidate,
            string detail
        )
        {
            CandidateReconciliationDiagnostic diagnostic =
                new CandidateReconciliationDiagnostic(
                    code,
                    candidate.AssemblyPath,
                    detail
                );
            entries.Add(new CandidateReconciliationEntry(
                state,
                candidate,
                null,
                diagnostic
            ));
        }

        private static string Fingerprint(RecognizedPluginCandidate candidate)
        {
            return candidate.ContentHash + "\0" + candidate.TypeName + "\0" +
                candidate.AssemblyIdentity;
        }
    }
}
