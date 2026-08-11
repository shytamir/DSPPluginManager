using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.Loading
{
    internal delegate Assembly RuntimeAssemblyLoad(string assemblyPath);

    internal sealed class SelectedCandidateLoader
    {
        private static readonly Dictionary<string, CandidateRuntimeLoadResult>
            Outcomes = new Dictionary<string, CandidateRuntimeLoadResult>(
                StringComparer.OrdinalIgnoreCase
            );
        private static readonly object Sync = new object();
        private readonly RuntimeAssemblyLoad runtimeLoad;

        internal SelectedCandidateLoader()
            : this(Assembly.LoadFrom)
        {
        }

        internal SelectedCandidateLoader(RuntimeAssemblyLoad runtimeLoad)
        {
            this.runtimeLoad = runtimeLoad ??
                throw new ArgumentNullException("runtimeLoad");
        }

        internal CandidateRuntimeLoadResult Load(
            CandidateReconciliationEntry entry
        )
        {
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }
            if (entry.State != CandidateReconciliationState.Selected ||
                entry.Candidate == null)
            {
                string rejectedPath = EntryPath(entry);
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.CandidateNotSelected,
                    entry.Candidate,
                    rejectedPath,
                    "The reconciliation entry is " + entry.State +
                        "; only Selected entries may cross the runtime-load " +
                        "boundary.",
                    null
                );
            }

            RecognizedPluginCandidate candidate = entry.Candidate;
            string path;
            try
            {
                if (string.IsNullOrWhiteSpace(candidate.AssemblyPath) ||
                    !Path.IsPathRooted(candidate.AssemblyPath))
                {
                    throw new ArgumentException(
                        "The candidate assembly path must be absolute."
                    );
                }
                path = Path.GetFullPath(candidate.AssemblyPath);
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is NotSupportedException ||
                      exception is PathTooLongException)
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.CandidatePathInvalid,
                    candidate,
                    candidate.AssemblyPath,
                    "The selected candidate path is invalid.",
                    exception
                );
            }

            lock (Sync)
            {
                CandidateRuntimeLoadResult prior;
                if (Outcomes.TryGetValue(path, out prior))
                {
                    return prior;
                }

                CandidateRuntimeLoadResult result = LoadFirst(candidate, path);
                Outcomes.Add(path, result);
                return result;
            }
        }

        private CandidateRuntimeLoadResult LoadFirst(
            RecognizedPluginCandidate candidate,
            string path
        )
        {
            if (!File.Exists(path))
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.CandidateFileMissing,
                    candidate,
                    path,
                    "The selected candidate file no longer exists.",
                    null
                );
            }

            string currentHash;
            try
            {
                currentHash = ComputeSha256(path);
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.AssemblyLoadFailed,
                    candidate,
                    path,
                    "The selected candidate could not be integrity checked.",
                    exception
                );
            }
            if (!string.Equals(
                    currentHash,
                    candidate.ContentHash,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.CandidateContentChanged,
                    candidate,
                    path,
                    "The selected candidate changed after static inspection; " +
                        "expected SHA-256 " + candidate.ContentHash +
                        ", found " + currentHash + ".",
                    null
                );
            }

            AssemblyName expectedIdentity;
            AssemblyName fileIdentity;
            try
            {
                expectedIdentity = new AssemblyName(candidate.AssemblyIdentity);
                fileIdentity = AssemblyName.GetAssemblyName(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is BadImageFormatException ||
                      exception is FileLoadException ||
                      exception is IOException ||
                      exception is SecurityException)
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.AssemblyLoadFailed,
                    candidate,
                    path,
                    "The selected candidate assembly identity could not be read.",
                    exception
                );
            }
            if (!IdentitiesEqual(expectedIdentity, fileIdentity))
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.AssemblyIdentityMismatch,
                    candidate,
                    path,
                    "The runtime file identity is '" + fileIdentity.FullName +
                        "'; static inspection recorded '" +
                        candidate.AssemblyIdentity + "'.",
                    null
                );
            }

            Assembly assembly = FindLoadedAtPath(path);
            if (assembly == null)
            {
                Assembly conflict = FindLoadedIdentityAtAnotherPath(
                    expectedIdentity,
                    path
                );
                if (conflict != null)
                {
                    return Failed(
                        CandidateRuntimeLoadDiagnosticCode
                            .AssemblyAlreadyLoadedFromDifferentPath,
                        candidate,
                        path,
                        "Assembly identity '" + expectedIdentity.FullName +
                            "' is already loaded from '" +
                            SafeLocation(conflict) + "'.",
                        null
                    );
                }

                try
                {
                    assembly = runtimeLoad(path);
                }
                catch (Exception exception)
                {
                    CandidateRuntimeLoadDiagnosticCode code =
                        IsMissingDependency(exception)
                            ? CandidateRuntimeLoadDiagnosticCode.MissingDependency
                            : CandidateRuntimeLoadDiagnosticCode.AssemblyLoadFailed;
                    return Failed(
                        code,
                        candidate,
                        path,
                        code == CandidateRuntimeLoadDiagnosticCode.MissingDependency
                            ? MissingDependencyDetail(exception)
                            : "The selected candidate assembly could not be loaded.",
                        exception
                    );
                }
            }

            if (assembly == null)
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.AssemblyLoadFailed,
                    candidate,
                    path,
                    "The runtime loader returned no assembly.",
                    null
                );
            }
            if (!IdentitiesEqual(expectedIdentity, assembly.GetName()))
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.AssemblyIdentityMismatch,
                    candidate,
                    path,
                    "The loaded assembly identity is '" +
                        assembly.GetName().FullName + "'; static inspection " +
                        "recorded '" + candidate.AssemblyIdentity + "'.",
                    null
                );
            }
            string loadedLocation = SafeLocation(assembly);
            if (string.IsNullOrWhiteSpace(loadedLocation) ||
                !string.Equals(
                    Path.GetFullPath(loadedLocation),
                    path,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.AssemblyLocationMismatch,
                    candidate,
                    path,
                    "The selected path loaded from unexpected location '" +
                        loadedLocation + "'.",
                    null
                );
            }

            Type pluginType;
            try
            {
                pluginType = assembly.GetType(
                    candidate.TypeName,
                    false,
                    false
                );
                if (pluginType == null)
                {
                    foreach (Type availableType in assembly.GetTypes())
                    {
                        ForceTypeShapeResolution(availableType);
                    }
                }
                else
                {
                    ForceTypeShapeResolution(pluginType);
                }
            }
            catch (Exception exception)
            {
                CandidateRuntimeLoadDiagnosticCode code =
                    IsMissingDependency(exception)
                        ? CandidateRuntimeLoadDiagnosticCode.MissingDependency
                        : CandidateRuntimeLoadDiagnosticCode.PluginTypeNotFound;
                return Failed(
                    code,
                    candidate,
                    path,
                    code == CandidateRuntimeLoadDiagnosticCode.MissingDependency
                        ? MissingDependencyDetail(exception)
                        : "The exact statically inspected plugin type could " +
                            "not be resolved.",
                    exception
                );
            }
            if (pluginType == null ||
                !string.Equals(
                    pluginType.FullName,
                    candidate.TypeName,
                    StringComparison.Ordinal
                ) || !ReferenceEquals(pluginType.Assembly, assembly))
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.PluginTypeNotFound,
                    candidate,
                    path,
                    "The exact statically inspected plugin type '" +
                        candidate.TypeName + "' is absent from the loaded " +
                        "assembly.",
                    null
                );
            }
            if (!pluginType.IsClass || pluginType.IsAbstract)
            {
                return Failed(
                    CandidateRuntimeLoadDiagnosticCode.PluginTypeNotConcrete,
                    candidate,
                    path,
                    "The resolved plugin type is not a concrete class.",
                    null
                );
            }

            return CandidateRuntimeLoadResult.Loaded(
                candidate,
                assembly,
                pluginType
            );
        }

        private static CandidateRuntimeLoadResult Failed(
            CandidateRuntimeLoadDiagnosticCode code,
            RecognizedPluginCandidate candidate,
            string path,
            string detail,
            Exception exception
        )
        {
            return CandidateRuntimeLoadResult.Failed(
                candidate,
                new CandidateRuntimeLoadDiagnostic(
                    code,
                    candidate,
                    path,
                    detail,
                    exception
                )
            );
        }

        private static string EntryPath(CandidateReconciliationEntry entry)
        {
            if (entry.Candidate != null)
            {
                return entry.Candidate.AssemblyPath;
            }
            return entry.InspectionDiagnostic == null
                ? "<unavailable>"
                : entry.InspectionDiagnostic.AssemblyPath;
        }

        private static Assembly FindLoadedAtPath(string path)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .FirstOrDefault(assembly => string.Equals(
                    SafeLocation(assembly),
                    path,
                    StringComparison.OrdinalIgnoreCase
                ));
        }

        private static Assembly FindLoadedIdentityAtAnotherPath(
            AssemblyName expected,
            string selectedPath
        )
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .FirstOrDefault(assembly =>
                    IdentitiesEqual(expected, assembly.GetName()) &&
                    !string.Equals(
                        SafeLocation(assembly),
                        selectedPath,
                        StringComparison.OrdinalIgnoreCase
                    )
                );
        }

        private static void ForceTypeShapeResolution(Type type)
        {
            Type current = type;
            while (current != null)
            {
                current.GetInterfaces();
                current = current.BaseType;
            }
        }

        private static bool IsMissingDependency(Exception exception)
        {
            Exception current = exception;
            while (current != null)
            {
                ReflectionTypeLoadException reflectionFailure =
                    current as ReflectionTypeLoadException;
                if (reflectionFailure != null &&
                    reflectionFailure.LoaderExceptions != null &&
                    reflectionFailure.LoaderExceptions.Any(IsMissingDependency))
                {
                    return true;
                }
                if (current is FileNotFoundException ||
                    current is TypeLoadException)
                {
                    return true;
                }
                if (current is FileLoadException &&
                    current.Message != null &&
                    current.Message.IndexOf(
                        "Could not load",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0)
                {
                    return true;
                }
                current = current.InnerException;
            }
            return false;
        }

        private static string MissingDependencyDetail(Exception exception)
        {
            ReflectionTypeLoadException reflectionFailure =
                exception as ReflectionTypeLoadException;
            IEnumerable<Exception> failures = reflectionFailure == null ||
                reflectionFailure.LoaderExceptions == null
                ? new[] { exception }
                : reflectionFailure.LoaderExceptions.Where(item => item != null);
            return "A runtime dependency required by the selected candidate " +
                "could not be resolved: " + string.Join(
                    " | ",
                    failures.Select(item => item.ToString())
                );
        }

        private static bool IdentitiesEqual(
            AssemblyName expected,
            AssemblyName actual
        )
        {
            if (expected == null || actual == null ||
                !string.Equals(expected.Name, actual.Name, StringComparison.Ordinal) ||
                !Equals(expected.Version, actual.Version) ||
                !string.Equals(
                    NormalizeCulture(expected.CultureName),
                    NormalizeCulture(actual.CultureName),
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return false;
            }
            byte[] expectedToken = expected.GetPublicKeyToken() ?? new byte[0];
            byte[] actualToken = actual.GetPublicKeyToken() ?? new byte[0];
            return expectedToken.SequenceEqual(actualToken);
        }

        private static string NormalizeCulture(string culture)
        {
            return string.IsNullOrWhiteSpace(culture) ||
                string.Equals(
                    culture,
                    "neutral",
                    StringComparison.OrdinalIgnoreCase
                )
                ? string.Empty
                : culture;
        }

        private static string SafeLocation(Assembly assembly)
        {
            try
            {
                return assembly == null || assembly.IsDynamic
                    ? string.Empty
                    : assembly.Location;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            ))
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }
    }
}
