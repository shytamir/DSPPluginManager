using System;
using System.IO;
using UnityEngine;

namespace DSPPluginManager.Contracts
{
    public abstract class PluginBehaviour : MonoBehaviour
    {
        private PluginLogger logger;
        private string writableRoot;

        public PluginLogger Logger
        {
            get
            {
                if (logger == null)
                {
                    throw new InvalidOperationException(
                        "The host has not prepared the plugin logger."
                    );
                }

                return logger;
            }
        }

        public string WritableRoot
        {
            get
            {
                if (writableRoot == null)
                {
                    throw new InvalidOperationException(
                        "The host has not prepared the plugin writable root."
                    );
                }

                return writableRoot;
            }
        }

        public abstract void Activate();

        public abstract void Deactivate();

        internal void InitializeLogger(PluginLogger value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (logger != null)
            {
                throw new InvalidOperationException(
                    "The plugin logger has already been prepared."
                );
            }

            logger = value;
        }

        internal void InitializeWritableRoot(string value)
        {
            if (writableRoot != null)
            {
                throw new InvalidOperationException(
                    "The plugin writable root has already been prepared."
                );
            }
            if (string.IsNullOrWhiteSpace(value) ||
                !Path.IsPathRooted(value) ||
                IsDriveRelative(value))
            {
                throw new ArgumentException(
                    "The plugin writable root must be an absolute path.",
                    "value"
                );
            }

            string normalized = Path.GetFullPath(value);
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
            if (!Directory.Exists(normalized))
            {
                throw new InvalidOperationException(
                    "The plugin writable root does not exist: '" +
                    normalized + "'."
                );
            }

            writableRoot = normalized;
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
