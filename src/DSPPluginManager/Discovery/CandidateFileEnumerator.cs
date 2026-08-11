using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security;

namespace DSPPluginManager.Discovery
{
    internal sealed class CandidateFileEnumerator
    {
        private readonly string configuredRoot;
        private readonly IPluginFileSystem fileSystem;

        internal CandidateFileEnumerator(string configuredRoot)
            : this(configuredRoot, new WindowsPluginFileSystem())
        {
        }

        internal CandidateFileEnumerator(
            string configuredRoot,
            IPluginFileSystem fileSystem
        )
        {
            if (string.IsNullOrWhiteSpace(configuredRoot))
            {
                throw new ArgumentException(
                    "Configured plugin root is required.",
                    "configuredRoot"
                );
            }
            if (!Path.IsPathRooted(configuredRoot) ||
                IsDriveRelative(configuredRoot))
            {
                throw new ArgumentException(
                    "Configured plugin root must be absolute: '" +
                    configuredRoot + "'.",
                    "configuredRoot"
                );
            }

            this.configuredRoot = NormalizePath(configuredRoot);
            this.fileSystem = fileSystem ??
                throw new ArgumentNullException("fileSystem");
        }

        internal CandidateEnumerationResult Enumerate()
        {
            List<CandidateEnumerationDiagnostic> diagnostics =
                new List<CandidateEnumerationDiagnostic>();
            PluginFileSystemEntry root;
            try
            {
                root = fileSystem.Inspect(configuredRoot);
                if (!root.IsDirectory)
                {
                    throw new IOException("The plugin root is not a directory.");
                }
            }
            catch (Exception exception) when (IsFileSystemFailure(exception))
            {
                diagnostics.Add(Unreadable(configuredRoot, exception));
                return Result(
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    diagnostics
                );
            }

            string canonicalRoot = NormalizePath(root.CanonicalPath);
            SortedDictionary<string, PluginFileSystemEntry> pendingDirectories =
                new SortedDictionary<string, PluginFileSystemEntry>(
                StringComparer.Ordinal
            );
            HashSet<string> visitedDirectories = new HashSet<string>(
                StringComparer.Ordinal
            );
            Dictionary<string, string> candidates =
                new Dictionary<string, string>(StringComparer.Ordinal);
            pendingDirectories.Add(canonicalRoot, root);

            while (pendingDirectories.Count != 0)
            {
                KeyValuePair<string, PluginFileSystemEntry> pending =
                    pendingDirectories.First();
                string directory = pending.Key;
                pendingDirectories.Remove(directory);
                PluginFileSystemEntry directoryEntry = pending.Value;
                if (!visitedDirectories.Add(directoryEntry.Identity))
                {
                    continue;
                }

                string[] entries;
                try
                {
                    entries = fileSystem.GetEntries(directory);
                }
                catch (Exception exception) when (IsFileSystemFailure(exception))
                {
                    diagnostics.Add(Unreadable(directory, exception));
                    continue;
                }
                Array.Sort(entries, StringComparer.Ordinal);

                foreach (string entryPath in entries)
                {
                    string normalizedEntry = NormalizePath(entryPath);
                    PluginFileSystemEntry entry;
                    try
                    {
                        entry = fileSystem.Inspect(normalizedEntry);
                    }
                    catch (Exception exception)
                        when (IsFileSystemFailure(exception))
                    {
                        diagnostics.Add(Unreadable(normalizedEntry, exception));
                        continue;
                    }

                    string canonicalPath = NormalizePath(entry.CanonicalPath);
                    if (!IsWithinRoot(canonicalPath, canonicalRoot))
                    {
                        diagnostics.Add(new CandidateEnumerationDiagnostic(
                            CandidateEnumerationDiagnosticCode.OutsideRootLink,
                            normalizedEntry,
                            canonicalPath
                        ));
                        continue;
                    }
                    if (entry.IsDirectory)
                    {
                        if (!visitedDirectories.Contains(entry.Identity))
                        {
                            if (!pendingDirectories.ContainsKey(canonicalPath))
                            {
                                pendingDirectories.Add(canonicalPath, entry);
                            }
                        }
                        continue;
                    }
                    if (!string.Equals(
                            Path.GetExtension(canonicalPath),
                            ".dll",
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        continue;
                    }

                    string existing;
                    if (!candidates.TryGetValue(entry.Identity, out existing) ||
                        string.CompareOrdinal(canonicalPath, existing) < 0)
                    {
                        candidates[entry.Identity] = canonicalPath;
                    }
                }
            }

            return Result(candidates, diagnostics);
        }

        private static CandidateEnumerationResult Result(
            Dictionary<string, string> candidates,
            List<CandidateEnumerationDiagnostic> diagnostics
        )
        {
            string[] paths = candidates.Values
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            CandidateEnumerationDiagnostic[] orderedDiagnostics = diagnostics
                .OrderBy(diagnostic => diagnostic.Code)
                .ThenBy(diagnostic => diagnostic.Path, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Detail, StringComparer.Ordinal)
                .ToArray();
            return new CandidateEnumerationResult(paths, orderedDiagnostics);
        }

        private static CandidateEnumerationDiagnostic Unreadable(
            string path,
            Exception exception
        )
        {
            return new CandidateEnumerationDiagnostic(
                CandidateEnumerationDiagnosticCode.UnreadableEntry,
                path,
                exception.GetType().Name
            );
        }

        private static bool IsWithinRoot(string path, string root)
        {
            if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            string prefix = root.EndsWith(
                Path.DirectorySeparatorChar.ToString(),
                StringComparison.Ordinal
            ) ? root : root + Path.DirectorySeparatorChar;
            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            string normalized = Path.GetFullPath(path);
            string root = Path.GetPathRoot(normalized);
            return string.Equals(
                normalized,
                root,
                StringComparison.OrdinalIgnoreCase
            ) ? normalized : normalized.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar
            );
        }

        private static bool IsDriveRelative(string path)
        {
            return path.Length >= 2 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path.Length == 2 ||
                 (path[2] != Path.DirectorySeparatorChar &&
                  path[2] != Path.AltDirectorySeparatorChar));
        }

        private static bool IsFileSystemFailure(Exception exception)
        {
            return exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is SecurityException ||
                exception is Win32Exception ||
                exception is ArgumentException ||
                exception is NotSupportedException;
        }
    }
}
