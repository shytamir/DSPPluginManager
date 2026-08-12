using System;
using System.IO;

namespace DSPPluginManager.Hosting
{
    internal sealed class HostEnvironmentPaths
    {
        private HostEnvironmentPaths(
            string executablePath,
            string managedDirectory,
            string hostRoot,
            string pluginDirectory,
            string configurationDirectory,
            string logDirectory,
            string dependencyDirectory,
            string writableOutputDirectory
        )
        {
            ExecutablePath = executablePath;
            ManagedDirectory = managedDirectory;
            HostRoot = hostRoot;
            PluginDirectory = pluginDirectory;
            ConfigurationDirectory = configurationDirectory;
            LogDirectory = logDirectory;
            DependencyDirectory = dependencyDirectory;
            WritableOutputDirectory = writableOutputDirectory;
        }

        internal string ExecutablePath { get; }

        internal string ManagedDirectory { get; }

        internal string HostRoot { get; }

        internal string PluginDirectory { get; }

        internal string ConfigurationDirectory { get; }

        internal string LogDirectory { get; }

        internal string DependencyDirectory { get; }

        internal string WritableOutputDirectory { get; }

        internal string CreatePluginWritableRoot(string identifier)
        {
            return PluginWritableRootPath.Create(
                WritableOutputDirectory,
                identifier
            );
        }

        internal static HostEnvironmentPaths Create(
            string executablePath,
            string managedDirectory,
            string hostRoot,
            string pluginDirectory,
            string configurationDirectory,
            string logDirectory,
            string dependencyDirectory,
            string writableOutputDirectory
        )
        {
            string executable = NormalizeAbsolutePath(
                executablePath,
                "executable"
            );
            string managed = NormalizeAbsoluteDirectory(
                managedDirectory,
                "managed"
            );
            string host = NormalizeAbsoluteDirectory(hostRoot, "host root");
            string plugins = NormalizeHostChild(
                pluginDirectory,
                "plugin",
                host
            );
            string configuration = NormalizeHostChild(
                configurationDirectory,
                "configuration",
                host
            );
            string logs = NormalizeHostChild(logDirectory, "log", host);
            string dependencies = NormalizeHostChild(
                dependencyDirectory,
                "dependency",
                host
            );
            string writableOutput = NormalizeHostChild(
                writableOutputDirectory,
                "writable-output",
                host
            );

            RequireExistingFile(executable, "executable");
            RequireExistingDirectory(managed, "managed");
            RequireDirectoryPath(host, "host root");
            RequireDirectoryPath(plugins, "plugin");
            RequireDirectoryPath(configuration, "configuration");
            RequireDirectoryPath(logs, "log");
            RequireDirectoryPath(dependencies, "dependency");
            RequireDirectoryPath(writableOutput, "writable-output");

            EnsureDirectory(host, "host root");
            EnsureDirectory(plugins, "plugin");
            EnsureDirectory(configuration, "configuration");
            EnsureDirectory(logs, "log");
            EnsureDirectory(dependencies, "dependency");
            EnsureDirectory(writableOutput, "writable-output");

            return new HostEnvironmentPaths(
                executable,
                managed,
                host,
                plugins,
                configuration,
                logs,
                dependencies,
                writableOutput
            );
        }

        private static string NormalizeHostChild(
            string path,
            string role,
            string hostRoot
        )
        {
            string normalized = NormalizeAbsoluteDirectory(path, role);
            string prefix = AppendDirectorySeparator(hostRoot);
            if (!normalized.StartsWith(
                    prefix,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new ArgumentException(
                    role + " directory '" + normalized +
                    "' must be a child of host root '" + hostRoot + "'.",
                    role
                );
            }

            return normalized;
        }

        private static string NormalizeAbsoluteDirectory(string path, string role)
        {
            string normalized = NormalizeAbsolutePath(path, role);
            string root = Path.GetPathRoot(normalized);
            if (!string.Equals(
                    normalized,
                    root,
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

        private static string NormalizeAbsolutePath(string path, string role)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(role + " path is required.", role);
            }
            if (!Path.IsPathRooted(path) || IsDriveRelative(path))
            {
                throw new ArgumentException(
                    role + " path must be absolute: '" + path + "'.",
                    role
                );
            }

            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is NotSupportedException ||
                      exception is PathTooLongException)
            {
                throw new ArgumentException(
                    role + " path is invalid: '" + path + "'.",
                    role,
                    exception
                );
            }
        }

        private static bool IsDriveRelative(string path)
        {
            return path.Length >= 2 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path.Length == 2 || !IsDirectorySeparator(path[2]));
        }

        private static bool IsDirectorySeparator(char value)
        {
            return value == Path.DirectorySeparatorChar ||
                value == Path.AltDirectorySeparatorChar;
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (IsDirectorySeparator(path[path.Length - 1]))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }

        private static void RequireExistingFile(string path, string role)
        {
            if (File.Exists(path))
            {
                return;
            }
            if (Directory.Exists(path))
            {
                throw new InvalidOperationException(
                    role + " path is a directory but a file is required: '" +
                    path + "'."
                );
            }

            throw new InvalidOperationException(
                role + " file does not exist: '" + path + "'."
            );
        }

        private static void RequireExistingDirectory(string path, string role)
        {
            if (Directory.Exists(path))
            {
                return;
            }
            if (File.Exists(path))
            {
                throw new InvalidOperationException(
                    role + " path is a file but a directory is required: '" +
                    path + "'."
                );
            }

            throw new InvalidOperationException(
                role + " directory does not exist: '" + path + "'."
            );
        }

        private static void RequireDirectoryPath(string path, string role)
        {
            if (File.Exists(path))
            {
                throw new InvalidOperationException(
                    role + " path is a file but a directory is required: '" +
                    path + "'."
                );
            }
        }

        private static void EnsureDirectory(string path, string role)
        {
            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Could not initialize " + role + " directory '" + path +
                    "'.",
                    exception
                );
            }
        }
    }
}
