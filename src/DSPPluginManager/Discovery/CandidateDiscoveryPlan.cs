using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DSPPluginManager.Discovery
{
    internal sealed class CandidateDiscoveryPlan
    {
        internal CandidateDiscoveryPlan(
            int enumeratedCandidateCount,
            CandidateEnumerationDiagnostic[] enumerationDiagnostics,
            CandidateReconciliationResult reconciliation,
            string[] reportLines,
            int runtimeLoadedCandidateCount
        )
        {
            EnumeratedCandidateCount = enumeratedCandidateCount;
            EnumerationDiagnostics = Array.AsReadOnly(
                enumerationDiagnostics ??
                    throw new ArgumentNullException("enumerationDiagnostics")
            );
            Reconciliation = reconciliation ??
                throw new ArgumentNullException("reconciliation");
            ReportLines = Array.AsReadOnly(
                reportLines ?? throw new ArgumentNullException("reportLines")
            );
            RuntimeLoadedCandidateCount = runtimeLoadedCandidateCount;
        }

        internal int EnumeratedCandidateCount { get; }

        internal IReadOnlyList<CandidateEnumerationDiagnostic>
            EnumerationDiagnostics { get; }

        internal CandidateReconciliationResult Reconciliation { get; }

        internal IReadOnlyList<string> ReportLines { get; }

        internal int RuntimeLoadedCandidateCount { get; }
    }

    internal static class CandidateDiscoveryPlanner
    {
        internal static CandidateDiscoveryPlan Create(
            string pluginRoot,
            PluginInspectionReferences references
        )
        {
            if (string.IsNullOrWhiteSpace(pluginRoot))
            {
                throw new ArgumentException(
                    "The plugin root is required.",
                    "pluginRoot"
                );
            }
            string root = Path.GetFullPath(pluginRoot);
            CandidateEnumerationResult enumeration =
                new CandidateFileEnumerator(root).Enumerate();
            PluginMetadataReader reader = new PluginMetadataReader(
                references ?? throw new ArgumentNullException("references")
            );
            PluginInspectionResult[] inspections = enumeration.CandidatePaths
                .Select(reader.Inspect)
                .ToArray();
            CandidateReconciliationResult reconciliation =
                new CandidateReconciler().Reconcile(inspections);
            string[] reportLines = reconciliation.Entries
                .Select(entry => FormatEntry(root, entry))
                .ToArray();
            int loadedCount = CountRuntimeLoadedCandidates(
                enumeration.CandidatePaths
            );

            return new CandidateDiscoveryPlan(
                enumeration.CandidatePaths.Count,
                enumeration.Diagnostics.ToArray(),
                reconciliation,
                reportLines,
                loadedCount
            );
        }

        private static string FormatEntry(
            string pluginRoot,
            CandidateReconciliationEntry entry
        )
        {
            RecognizedPluginCandidate candidate = entry.Candidate;
            string path = candidate == null
                ? entry.InspectionDiagnostic.AssemblyPath
                : candidate.AssemblyPath;
            string identifier = candidate == null
                ? "-"
                : candidate.IdentifierComparisonKey;
            string version = candidate == null
                ? "-"
                : candidate.Version.ToString(3);
            string diagnostic = entry.Diagnostic == null
                ? "-"
                : entry.Diagnostic.Code.ToString();
            if (entry.InspectionDiagnostic != null)
            {
                diagnostic += "/" + entry.InspectionDiagnostic.Code;
            }

            return "state=" + entry.State +
                "|identifier=" + identifier +
                "|version=" + version +
                "|path=" + RelativePath(pluginRoot, path) +
                "|diagnostic=" + diagnostic;
        }

        private static string RelativePath(string root, string path)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
            string normalizedPath = Path.GetFullPath(path);
            string prefix = normalizedRoot + Path.DirectorySeparatorChar;
            if (!normalizedPath.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new InvalidOperationException(
                    "Candidate path escaped the configured plugin root: '" +
                    normalizedPath + "'."
                );
            }
            return normalizedPath.Substring(prefix.Length)
                .Replace(Path.DirectorySeparatorChar, '/');
        }

        private static int CountRuntimeLoadedCandidates(
            IReadOnlyList<string> candidatePaths
        )
        {
            HashSet<string> candidates = new HashSet<string>(
                candidatePaths.Select(Path.GetFullPath),
                StringComparer.OrdinalIgnoreCase
            );
            int count = 0;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string location;
                try
                {
                    location = assembly.IsDynamic ? null : assembly.Location;
                }
                catch (NotSupportedException)
                {
                    location = null;
                }
                if (!string.IsNullOrWhiteSpace(location) &&
                    candidates.Contains(Path.GetFullPath(location)))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
