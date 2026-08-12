using System;
using System.IO;
using System.Reflection;
using DSPPluginManager.Hosting;

namespace DSPPluginManager.Tests
{
    internal static class HostEnvironmentPathsTests
    {
        private static readonly string[] Roles =
        {
            "executable",
            "managed",
            "host root",
            "plugin",
            "configuration",
            "log",
            "dependency",
            "writable-output"
        };

        internal static void Run()
        {
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(sandbox);
            try
            {
                string[] inputs = CreateInputs(sandbox);
                VerifyNormalizationAndWorkingDirectoryIndependence(
                    sandbox,
                    inputs
                );
                VerifyEmptyAndRelativeInputs(inputs);
                VerifyHostRootContainment(inputs);
                VerifyFileAndDirectoryConflicts(sandbox, inputs);
                VerifyMissingBootstrapInput(sandbox, inputs);
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static string[] CreateInputs(string sandbox)
        {
            string gameDirectory = Path.Combine(sandbox, "game");
            string executable = Path.Combine(gameDirectory, "DSPGAME.exe");
            string managed = Path.Combine(
                gameDirectory,
                "DSPGAME_Data",
                "Managed"
            );
            Directory.CreateDirectory(managed);
            File.WriteAllText(executable, string.Empty);

            string host = Path.Combine(sandbox, "explicit-host");
            return new[]
            {
                executable,
                managed,
                host,
                Path.Combine(host, "custom-plugins", "."),
                Path.Combine(host, "custom-config", "child", ".."),
                Path.Combine(host, "custom-logs"),
                Path.Combine(host, "custom-dependencies"),
                Path.Combine(host, "custom-writable", ".")
            };
        }

        private static void VerifyNormalizationAndWorkingDirectoryIndependence(
            string sandbox,
            string[] inputs
        )
        {
            string originalDirectory = Environment.CurrentDirectory;
            string firstWorkingDirectory = Path.Combine(sandbox, "working-a");
            string secondWorkingDirectory = Path.Combine(sandbox, "working-b");
            Directory.CreateDirectory(firstWorkingDirectory);
            Directory.CreateDirectory(secondWorkingDirectory);

            HostEnvironmentPaths first;
            HostEnvironmentPaths second;
            try
            {
                Environment.CurrentDirectory = firstWorkingDirectory;
                first = Create(inputs);
                Environment.CurrentDirectory = secondWorkingDirectory;
                second = Create(inputs);
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
            }

            string[] firstValues = Values(first);
            string[] secondValues = Values(second);
            for (int index = 0; index < firstValues.Length; index++)
            {
                string expected = Normalize(inputs[index], index != 0);
                TestAssert.Equal(expected, firstValues[index], Roles[index]);
                TestAssert.Equal(
                    firstValues[index],
                    secondValues[index],
                    Roles[index] + " working-directory independence"
                );
                if (index >= 2)
                {
                    TestAssert.True(
                        Directory.Exists(firstValues[index]),
                        Roles[index] + " directory was not initialized."
                    );
                }
            }

            foreach (PropertyInfo property in typeof(HostEnvironmentPaths)
                .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                TestAssert.True(
                    !property.CanWrite,
                    property.Name + " must not expose a setter."
                );
            }
            foreach (FieldInfo field in typeof(HostEnvironmentPaths)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                TestAssert.True(
                    field.IsInitOnly,
                    field.Name + " must be readonly after construction."
                );
            }

            string pluginRoot = first.CreatePluginWritableRoot("fixture.plugin");
            TestAssert.Equal(
                Path.Combine(first.WritableOutputDirectory, "fixture.plugin"),
                pluginRoot,
                "host-derived plugin writable root"
            );
            TestAssert.True(Directory.Exists(pluginRoot),
                "Host-derived plugin writable root was not initialized.");
        }

        private static void VerifyEmptyAndRelativeInputs(string[] validInputs)
        {
            for (int index = 0; index < validInputs.Length; index++)
            {
                int capturedIndex = index;
                string[] empty = Clone(validInputs);
                empty[capturedIndex] = " ";
                TestAssert.Throws<ArgumentException>(
                    () => Create(empty),
                    Roles[capturedIndex],
                    "required"
                );

                string[] relative = Clone(validInputs);
                relative[capturedIndex] = "relative-path";
                TestAssert.Throws<ArgumentException>(
                    () => Create(relative),
                    Roles[capturedIndex],
                    "absolute",
                    "relative-path"
                );
            }

            string[] driveRelative = Clone(validInputs);
            driveRelative[3] = "C:relative-path";
            TestAssert.Throws<ArgumentException>(
                () => Create(driveRelative),
                "plugin",
                "absolute",
                "C:relative-path"
            );
        }

        private static void VerifyHostRootContainment(string[] validInputs)
        {
            for (int index = 3; index < validInputs.Length; index++)
            {
                int capturedIndex = index;
                string[] outside = Clone(validInputs);
                outside[capturedIndex] = Path.Combine(
                    validInputs[2],
                    "..",
                    "outside-" + capturedIndex
                );
                TestAssert.Throws<ArgumentException>(
                    () => Create(outside),
                    Roles[capturedIndex],
                    "must be a child",
                    "outside-" + capturedIndex
                );
            }
        }

        private static void VerifyFileAndDirectoryConflicts(
            string sandbox,
            string[] validInputs
        )
        {
            string executableDirectory = Path.Combine(
                sandbox,
                "executable-is-directory"
            );
            Directory.CreateDirectory(executableDirectory);
            string[] badExecutable = Clone(validInputs);
            badExecutable[0] = executableDirectory;
            TestAssert.Throws<InvalidOperationException>(
                () => Create(badExecutable),
                "executable",
                "directory",
                "file is required",
                executableDirectory
            );

            string managedFile = Path.Combine(sandbox, "managed-is-file");
            File.WriteAllText(managedFile, string.Empty);
            string[] badManaged = Clone(validInputs);
            badManaged[1] = managedFile;
            TestAssert.Throws<InvalidOperationException>(
                () => Create(badManaged),
                "managed",
                "file",
                "directory is required",
                managedFile
            );

            string hostFile = Path.Combine(sandbox, "host-is-file");
            File.WriteAllText(hostFile, string.Empty);
            string[] badHost = Clone(validInputs);
            badHost[2] = hostFile;
            for (int index = 3; index < badHost.Length; index++)
            {
                badHost[index] = Path.Combine(hostFile, "child-" + index);
            }
            TestAssert.Throws<InvalidOperationException>(
                () => Create(badHost),
                "host root",
                "file",
                "directory is required",
                hostFile
            );

            for (int index = 3; index < validInputs.Length; index++)
            {
                int capturedIndex = index;
                string conflictPath = Path.Combine(
                    validInputs[2],
                    "file-conflict-" + capturedIndex
                );
                File.WriteAllText(conflictPath, string.Empty);
                string[] conflict = Clone(validInputs);
                conflict[capturedIndex] = conflictPath;
                TestAssert.Throws<InvalidOperationException>(
                    () => Create(conflict),
                    Roles[capturedIndex],
                    "file",
                    "directory is required",
                    conflictPath
                );
            }
        }

        private static void VerifyMissingBootstrapInput(
            string sandbox,
            string[] validInputs
        )
        {
            string missingManaged = Path.Combine(sandbox, "missing-managed");
            string[] missing = Clone(validInputs);
            missing[1] = missingManaged;
            TestAssert.Throws<InvalidOperationException>(
                () => Create(missing),
                "managed",
                "does not exist",
                missingManaged
            );
        }

        private static HostEnvironmentPaths Create(string[] values)
        {
            return HostEnvironmentPaths.Create(
                values[0],
                values[1],
                values[2],
                values[3],
                values[4],
                values[5],
                values[6],
                values[7]
            );
        }

        private static string[] Values(HostEnvironmentPaths paths)
        {
            return new[]
            {
                paths.ExecutablePath,
                paths.ManagedDirectory,
                paths.HostRoot,
                paths.PluginDirectory,
                paths.ConfigurationDirectory,
                paths.LogDirectory,
                paths.DependencyDirectory,
                paths.WritableOutputDirectory
            };
        }

        private static string[] Clone(string[] values)
        {
            return (string[])values.Clone();
        }

        private static string Normalize(string path, bool directory)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath);
            if (directory && !string.Equals(
                    fullPath,
                    root,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );
            }

            return fullPath;
        }
    }
}
