using System;
using System.Reflection;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.Loading
{
    internal enum CandidateRuntimeLoadDiagnosticCode
    {
        CandidateNotSelected,
        CandidatePathInvalid,
        CandidateFileMissing,
        CandidateContentChanged,
        AssemblyIdentityMismatch,
        AssemblyAlreadyLoadedFromDifferentPath,
        AssemblyLocationMismatch,
        AssemblyLoadFailed,
        MissingDependency,
        PluginTypeNotFound,
        PluginTypeNotConcrete
    }

    internal sealed class CandidateRuntimeLoadDiagnostic
    {
        internal CandidateRuntimeLoadDiagnostic(
            CandidateRuntimeLoadDiagnosticCode code,
            RecognizedPluginCandidate candidate,
            string assemblyPath,
            string detail,
            Exception exception
        )
        {
            Code = code;
            Identifier = candidate == null
                ? "<unrecognized>"
                : candidate.Identifier;
            Version = candidate == null
                ? "<unavailable>"
                : candidate.Version.ToString(3);
            AssemblyPath = assemblyPath ?? "<unavailable>";
            TypeName = candidate == null
                ? "<unavailable>"
                : candidate.TypeName;
            Detail = detail ?? throw new ArgumentNullException("detail");
            Exception = exception;
        }

        internal CandidateRuntimeLoadDiagnosticCode Code { get; }

        internal string Identifier { get; }

        internal string Version { get; }

        internal string AssemblyPath { get; }

        internal string TypeName { get; }

        internal string Phase
        {
            get { return "runtime-load"; }
        }

        internal string Detail { get; }

        internal Exception Exception { get; }
    }

    internal sealed class CandidateRuntimeLoadResult
    {
        private CandidateRuntimeLoadResult(
            RecognizedPluginCandidate candidate,
            Assembly assembly,
            Type pluginType,
            CandidateRuntimeLoadDiagnostic diagnostic
        )
        {
            bool loaded = assembly != null && pluginType != null;
            if (loaded == (diagnostic != null))
            {
                throw new ArgumentException(
                    "A runtime-load result must contain either a loaded " +
                    "assembly and type or one diagnostic."
                );
            }
            Candidate = candidate;
            Assembly = assembly;
            PluginType = pluginType;
            Diagnostic = diagnostic;
        }

        internal bool IsLoaded
        {
            get { return Diagnostic == null; }
        }

        internal RecognizedPluginCandidate Candidate { get; }

        internal Assembly Assembly { get; }

        internal Type PluginType { get; }

        internal CandidateRuntimeLoadDiagnostic Diagnostic { get; }

        internal static CandidateRuntimeLoadResult Loaded(
            RecognizedPluginCandidate candidate,
            Assembly assembly,
            Type pluginType
        )
        {
            return new CandidateRuntimeLoadResult(
                candidate ?? throw new ArgumentNullException("candidate"),
                assembly ?? throw new ArgumentNullException("assembly"),
                pluginType ?? throw new ArgumentNullException("pluginType"),
                null
            );
        }

        internal static CandidateRuntimeLoadResult Failed(
            RecognizedPluginCandidate candidate,
            CandidateRuntimeLoadDiagnostic diagnostic
        )
        {
            return new CandidateRuntimeLoadResult(
                candidate,
                null,
                null,
                diagnostic ?? throw new ArgumentNullException("diagnostic")
            );
        }
    }
}
