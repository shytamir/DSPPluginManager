using System;
using System.IO;
using System.Reflection;
using DSPPluginManager.Hosting;

namespace DSPPluginManager.Bootstrap
{
    internal sealed class BootstrapEnvironment
    {
        private BootstrapEnvironment(
            string targetAssemblyPath,
            HostEnvironmentPaths paths
        )
        {
            TargetAssemblyPath = targetAssemblyPath;
            Paths = paths;
        }

        internal string TargetAssemblyPath { get; }

        internal HostEnvironmentPaths Paths { get; }

        internal static BootstrapEnvironment Create(
            string executablePath,
            string managedDirectory,
            string targetAssemblyPath,
            string executingAssemblyPath
        )
        {
            string executable = RequireAbsoluteFile(
                executablePath,
                "Doorstop process"
            );
            if (!string.Equals(
                    Path.GetFileName(executable),
                    "DSPGAME.exe",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new InvalidOperationException(
                    "The Doorstop process must be DSPGAME.exe: '" +
                    executable + "'."
                );
            }

            string gameRoot = Path.GetDirectoryName(executable);
            string managed = RequireAbsoluteDirectory(
                managedDirectory,
                "Doorstop managed"
            );
            string target = NormalizeTarget(
                targetAssemblyPath,
                gameRoot,
                "Doorstop target"
            );
            string executing = RequireAbsoluteFile(
                executingAssemblyPath,
                "executing assembly"
            );
            if (!string.Equals(
                    target,
                    executing,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new InvalidOperationException(
                    "Doorstop targeted '" + target +
                    "' but loaded manager entry assembly '" + executing + "'."
                );
            }

            string hostRoot = Path.GetDirectoryName(executing);
            HostEnvironmentPaths paths = HostEnvironmentPaths.Create(
                executable,
                managed,
                hostRoot,
                Path.Combine(hostRoot, "plugins"),
                Path.Combine(hostRoot, "config"),
                Path.Combine(hostRoot, "logs"),
                Path.Combine(hostRoot, "dependencies"),
                Path.Combine(hostRoot, "writable")
            );
            return new BootstrapEnvironment(target, paths);
        }

        private static string NormalizeTarget(
            string path,
            string gameRoot,
            string role
        )
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(role + " path is required.");
            }

            string candidate = Path.IsPathRooted(path)
                ? path
                : Path.Combine(gameRoot, path);
            return RequireAbsoluteFile(Path.GetFullPath(candidate), role);
        }

        private static string RequireAbsoluteFile(string path, string role)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                throw new InvalidOperationException(
                    role + " path must be absolute."
                );
            }

            string normalized = Path.GetFullPath(path);
            if (!File.Exists(normalized))
            {
                throw new FileNotFoundException(
                    role + " file was not found.",
                    normalized
                );
            }
            return normalized;
        }

        private static string RequireAbsoluteDirectory(string path, string role)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                throw new InvalidOperationException(
                    role + " directory must be absolute."
                );
            }

            string normalized = Path.GetFullPath(path);
            if (!Directory.Exists(normalized))
            {
                throw new DirectoryNotFoundException(
                    role + " directory was not found: '" + normalized + "'."
                );
            }
            return normalized;
        }
    }
}
