using System;
using System.IO;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.Hosting
{
    internal static class PluginWritableRootPath
    {
        internal static string Create(
            string writableParent,
            string identifier
        )
        {
            if (!PluginContractRules.IsValidIdentifier(identifier))
            {
                throw new ArgumentException(
                    "Plugin identifier is invalid.",
                    "identifier"
                );
            }

            string parent = NormalizeAbsoluteDirectory(writableParent);
            if (File.Exists(parent))
            {
                throw new InvalidOperationException(
                    "Writable parent is a file but a directory is required: '" +
                    parent + "'."
                );
            }

            string segment = identifier.ToLowerInvariant();
            string root = NormalizeAbsoluteDirectory(
                Path.Combine(parent, segment)
            );
            if (!string.Equals(
                    Path.GetDirectoryName(root),
                    parent,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new InvalidOperationException(
                    "Plugin writable root is not a direct child of its parent."
                );
            }

            try
            {
                Directory.CreateDirectory(root);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Could not initialize plugin writable root '" + root +
                    "'.",
                    exception
                );
            }

            return root;
        }

        private static string NormalizeAbsoluteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Writable parent path is required.",
                    "writableParent"
                );
            }
            if (!Path.IsPathRooted(path) || IsDriveRelative(path))
            {
                throw new ArgumentException(
                    "Writable parent path must be absolute: '" + path + "'.",
                    "writableParent"
                );
            }

            string normalized;
            try
            {
                normalized = Path.GetFullPath(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is NotSupportedException ||
                      exception is PathTooLongException)
            {
                throw new ArgumentException(
                    "Writable parent path is invalid: '" + path + "'.",
                    "writableParent",
                    exception
                );
            }

            string volumeRoot = Path.GetPathRoot(normalized);
            if (!string.Equals(
                    normalized,
                    volumeRoot,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                normalized = normalized.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );
            }

            return normalized;
        }

        private static bool IsDriveRelative(string path)
        {
            return path.Length >= 2 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path.Length == 2 ||
                 path[2] != Path.DirectorySeparatorChar &&
                 path[2] != Path.AltDirectorySeparatorChar);
        }
    }
}
