using System;

namespace DSPPluginManager.Discovery
{
    internal enum PluginInspectionDiagnosticCode
    {
        NonManagedFile,
        MalformedAssembly,
        MissingReference,
        NoPluginType,
        UnsupportedPluginType,
        AbstractPluginType,
        InvalidMetadata,
        MultiplePluginTypes
    }

    internal sealed class PluginInspectionDiagnostic
    {
        internal PluginInspectionDiagnostic(
            PluginInspectionDiagnosticCode code,
            string assemblyPath,
            string detail
        )
        {
            Code = code;
            AssemblyPath = assemblyPath ??
                throw new ArgumentNullException("assemblyPath");
            Detail = detail ?? throw new ArgumentNullException("detail");
        }

        internal PluginInspectionDiagnosticCode Code { get; }

        internal string AssemblyPath { get; }

        internal string Detail { get; }
    }

    internal sealed class RecognizedPluginCandidate
    {
        internal RecognizedPluginCandidate(
            string identifier,
            string identifierComparisonKey,
            string displayName,
            Version version,
            string assemblyIdentity,
            string assemblyPath,
            string typeName,
            string contentHash
        )
        {
            Identifier = identifier ?? throw new ArgumentNullException("identifier");
            IdentifierComparisonKey = identifierComparisonKey ??
                throw new ArgumentNullException("identifierComparisonKey");
            DisplayName = displayName ?? throw new ArgumentNullException("displayName");
            Version = version ?? throw new ArgumentNullException("version");
            AssemblyIdentity = assemblyIdentity ??
                throw new ArgumentNullException("assemblyIdentity");
            AssemblyPath = assemblyPath ??
                throw new ArgumentNullException("assemblyPath");
            TypeName = typeName ?? throw new ArgumentNullException("typeName");
            ContentHash = contentHash ??
                throw new ArgumentNullException("contentHash");
        }

        internal string Identifier { get; }

        internal string IdentifierComparisonKey { get; }

        internal string DisplayName { get; }

        internal Version Version { get; }

        internal string AssemblyIdentity { get; }

        internal string AssemblyPath { get; }

        internal string TypeName { get; }

        internal string ContentHash { get; }
    }

    internal sealed class PluginInspectionResult
    {
        private PluginInspectionResult(
            RecognizedPluginCandidate candidate,
            PluginInspectionDiagnostic diagnostic
        )
        {
            Candidate = candidate;
            Diagnostic = diagnostic;
        }

        internal bool IsRecognized
        {
            get { return Candidate != null; }
        }

        internal RecognizedPluginCandidate Candidate { get; }

        internal PluginInspectionDiagnostic Diagnostic { get; }

        internal static PluginInspectionResult Recognized(
            RecognizedPluginCandidate candidate
        )
        {
            return new PluginInspectionResult(
                candidate ?? throw new ArgumentNullException("candidate"),
                null
            );
        }

        internal static PluginInspectionResult Rejected(
            PluginInspectionDiagnostic diagnostic
        )
        {
            return new PluginInspectionResult(
                null,
                diagnostic ?? throw new ArgumentNullException("diagnostic")
            );
        }
    }
}
