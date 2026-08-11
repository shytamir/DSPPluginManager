using System;
using System.IO;

namespace DSPPluginManager.Discovery
{
    internal sealed class PluginInspectionReferences
    {
        internal PluginInspectionReferences(
            string contractAssemblyPath,
            string dependencyDirectory,
            string gameManagedDirectory
        )
        {
            ContractAssemblyPath = RequireFile(
                contractAssemblyPath,
                "contractAssemblyPath"
            );
            DependencyDirectory = RequireDirectory(
                dependencyDirectory,
                "dependencyDirectory"
            );
            GameManagedDirectory = RequireDirectory(
                gameManagedDirectory,
                "gameManagedDirectory"
            );
        }

        internal string ContractAssemblyPath { get; }

        internal string DependencyDirectory { get; }

        internal string GameManagedDirectory { get; }

        private static string RequireFile(string path, string parameter)
        {
            string normalized = NormalizeAbsolute(path, parameter);
            if (!File.Exists(normalized))
            {
                throw new ArgumentException(
                    "Required inspection reference does not exist: '" +
                    normalized + "'.",
                    parameter
                );
            }
            return normalized;
        }

        private static string RequireDirectory(string path, string parameter)
        {
            string normalized = NormalizeAbsolute(path, parameter);
            if (!Directory.Exists(normalized))
            {
                throw new ArgumentException(
                    "Required inspection directory does not exist: '" +
                    normalized + "'.",
                    parameter
                );
            }
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

        private static string NormalizeAbsolute(string path, string parameter)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !Path.IsPathRooted(path) || IsDriveRelative(path))
            {
                throw new ArgumentException(
                    "Inspection path must be absolute.",
                    parameter
                );
            }
            return Path.GetFullPath(path);
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
    }
}
