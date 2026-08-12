using System;
using System.IO;
using System.Reflection;
using DSPPluginManager.Configuration;

namespace DSPPluginManager.Tests
{
    internal static class PluginConfigurationScopeTests
    {
        internal static void Run()
        {
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.RM25.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(sandbox);
            try
            {
                VerifyOwnedPathAndSourceStates(sandbox);
                VerifyBepInExSeparation(sandbox);
                VerifyInvalidInputs(sandbox);
                VerifyCollisionAndAccessFailures(sandbox);
                VerifyFailureIsolation(sandbox);
                VerifyImmutableShape();
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static void VerifyOwnedPathAndSourceStates(string sandbox)
        {
            string parent = Path.Combine(sandbox, "config", "child", "..");
            string firstWorking = Path.Combine(sandbox, "working-one");
            string secondWorking = Path.Combine(sandbox, "working-two");
            Directory.CreateDirectory(firstWorking);
            Directory.CreateDirectory(secondWorking);

            string originalWorking = Environment.CurrentDirectory;
            PluginConfigurationScope first;
            PluginConfigurationScope second;
            try
            {
                Environment.CurrentDirectory = firstWorking;
                first = PluginConfigurationScope.Create(
                    parent,
                    "Fixture.Plugin"
                );
                Environment.CurrentDirectory = secondWorking;
                second = PluginConfigurationScope.Create(
                    parent,
                    "fixture.plugin"
                );
            }
            finally
            {
                Environment.CurrentDirectory = originalWorking;
            }

            string expectedParent = Path.GetFullPath(Path.Combine(
                sandbox,
                "config"
            ));
            string expectedPath = Path.Combine(
                expectedParent,
                "fixture.plugin.cfg"
            );
            TestAssert.Equal("fixture.plugin", first.Identifier,
                "canonical configuration owner");
            TestAssert.Equal(expectedPath, first.FilePath,
                "owned configuration path");
            TestAssert.Equal(first.FilePath, second.FilePath,
                "working-directory-independent configuration path");
            AssertAvailable(
                first,
                PluginConfigurationSourceState.Missing,
                "missing source"
            );
            TestAssert.True(Directory.Exists(expectedParent),
                "Configuration parent was not created.");
            TestAssert.True(!File.Exists(expectedPath),
                "Scope creation unexpectedly created a configuration file.");

            File.WriteAllBytes(expectedPath, new byte[0]);
            AssertAvailable(
                PluginConfigurationScope.Create(parent, "fixture.plugin"),
                PluginConfigurationSourceState.Empty,
                "empty source"
            );
            File.WriteAllText(expectedPath, "[General]\r\nEnabled = true\r\n");
            AssertAvailable(
                PluginConfigurationScope.Create(parent, "fixture.plugin"),
                PluginConfigurationSourceState.Present,
                "present source"
            );
        }

        private static void VerifyBepInExSeparation(string sandbox)
        {
            string managerDirectory = Path.Combine(sandbox, "manager-config");
            string bepinexDirectory = Path.Combine(
                sandbox,
                "BepInEx",
                "config"
            );
            Directory.CreateDirectory(bepinexDirectory);
            string bepinexPath = Path.Combine(
                bepinexDirectory,
                "fixture.plugin.cfg"
            );
            byte[] original = { 0x42, 0x65, 0x70, 0x49, 0x6E, 0x45, 0x78 };
            File.WriteAllBytes(bepinexPath, original);
            DateTime originalWriteTime = File.GetLastWriteTimeUtc(bepinexPath);

            PluginConfigurationScope scope = PluginConfigurationScope.Create(
                managerDirectory,
                "fixture.plugin"
            );

            TestAssert.Equal(
                Path.Combine(managerDirectory, "fixture.plugin.cfg"),
                scope.FilePath,
                "separate manager configuration path"
            );
            AssertBytes(original, File.ReadAllBytes(bepinexPath),
                "BepInEx fixture contents");
            TestAssert.Equal(
                originalWriteTime,
                File.GetLastWriteTimeUtc(bepinexPath),
                "BepInEx fixture write time"
            );
            TestAssert.True(!File.Exists(scope.FilePath),
                "Manager scope imported the BepInEx fixture.");
        }

        private static void VerifyInvalidInputs(string sandbox)
        {
            foreach (string identifier in new[]
            {
                null, string.Empty, "../escape", "contains/slash",
                "contains space"
            })
            {
                string captured = identifier;
                TestAssert.Throws<ArgumentException>(
                    () => PluginConfigurationScope.Create(sandbox, captured),
                    "identifier",
                    "invalid"
                );
            }
            foreach (string directory in new[]
            {
                null, string.Empty, "relative", "C:drive-relative"
            })
            {
                string captured = directory;
                TestAssert.Throws<ArgumentException>(
                    () => PluginConfigurationScope.Create(
                        captured,
                        "fixture.plugin"
                    ),
                    "configurationDirectory"
                );
            }
        }

        private static void VerifyCollisionAndAccessFailures(string sandbox)
        {
            string parentFile = Path.Combine(sandbox, "parent-file");
            File.WriteAllText(parentFile, "source");
            AssertUnavailable(
                PluginConfigurationScope.Create(
                    parentFile,
                    "fixture.plugin"
                ),
                "parent is a file"
            );

            string directory = Path.Combine(sandbox, "directory-collision");
            string finalDirectory = Path.Combine(
                directory,
                "fixture.plugin.cfg"
            );
            Directory.CreateDirectory(finalDirectory);
            AssertUnavailable(
                PluginConfigurationScope.Create(directory, "fixture.plugin"),
                "configuration path is a directory"
            );

            string lockedDirectory = Path.Combine(sandbox, "locked-file");
            Directory.CreateDirectory(lockedDirectory);
            string lockedPath = Path.Combine(
                lockedDirectory,
                "fixture.plugin.cfg"
            );
            byte[] lockedContents = { 0x10, 0x20, 0x30 };
            File.WriteAllBytes(lockedPath, lockedContents);
            using (FileStream locked = new FileStream(
                lockedPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None
            ))
            {
                AssertUnavailable(
                    PluginConfigurationScope.Create(
                        lockedDirectory,
                        "fixture.plugin"
                    )
                );
                TestAssert.Equal(lockedContents.Length, locked.Length,
                    "locked source length");
            }
            AssertBytes(lockedContents, File.ReadAllBytes(lockedPath),
                "locked source contents");

            string fakeRoot = Path.GetFullPath(Path.Combine(
                sandbox,
                "fake-access"
            ));
            FakeConfigurationFileSystem createDenied =
                new FakeConfigurationFileSystem(
                    fakeRoot,
                    new UnauthorizedAccessException("create denied"),
                    null
                );
            AssertUnavailable(
                PluginConfigurationScope.Create(
                    fakeRoot,
                    "fixture.plugin",
                    createDenied
                ),
                "create denied"
            );

            FakeConfigurationFileSystem readDenied =
                new FakeConfigurationFileSystem(
                    fakeRoot,
                    null,
                    new UnauthorizedAccessException("read denied")
                );
            readDenied.ParentExists = true;
            readDenied.FileExists = true;
            AssertUnavailable(
                PluginConfigurationScope.Create(
                    fakeRoot,
                    "fixture.plugin",
                    readDenied
                ),
                "read denied"
            );
        }

        private static void VerifyFailureIsolation(string sandbox)
        {
            string parent = Path.Combine(sandbox, "isolated");
            Directory.CreateDirectory(Path.Combine(
                parent,
                "broken.plugin.cfg"
            ));
            PluginConfigurationScope broken =
                PluginConfigurationScope.Create(parent, "broken.plugin");
            PluginConfigurationScope healthy =
                PluginConfigurationScope.Create(parent, "healthy.plugin");

            AssertUnavailable(broken, "directory");
            AssertAvailable(
                healthy,
                PluginConfigurationSourceState.Missing,
                "unrelated plugin scope"
            );
            TestAssert.True(!File.Exists(healthy.FilePath),
                "Failure isolation unexpectedly created another plugin file.");
        }

        private static void VerifyImmutableShape()
        {
            foreach (PropertyInfo property in typeof(PluginConfigurationScope)
                .GetProperties(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                TestAssert.True(!property.CanWrite,
                    property.Name + " must not expose a setter.");
            }
            foreach (FieldInfo field in typeof(PluginConfigurationScope)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                TestAssert.True(field.IsInitOnly,
                    field.Name + " must be readonly.");
            }
        }

        private static void AssertAvailable(
            PluginConfigurationScope scope,
            PluginConfigurationSourceState state,
            string field
        )
        {
            TestAssert.True(scope.IsUsable, field + " must be usable.");
            TestAssert.Equal(state, scope.SourceState, field + " state");
            TestAssert.Equal<Exception>(null, scope.Failure,
                field + " failure");
        }

        private static void AssertUnavailable(
            PluginConfigurationScope scope,
            params string[] messageParts
        )
        {
            TestAssert.True(!scope.IsUsable,
                "Unavailable scope was reported usable.");
            TestAssert.Equal(
                PluginConfigurationSourceState.Unavailable,
                scope.SourceState,
                "unavailable source state"
            );
            TestAssert.True(scope.Failure != null,
                "Unavailable scope did not retain its failure.");
            foreach (string part in messageParts)
            {
                TestAssert.True(
                    scope.Failure.Message.IndexOf(
                        part,
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0,
                    "Failure did not contain '" + part + "': " +
                    scope.Failure.Message
                );
            }
        }

        private static void AssertBytes(
            byte[] expected,
            byte[] actual,
            string field
        )
        {
            TestAssert.Equal(expected.Length, actual.Length,
                field + " length");
            for (int index = 0; index < expected.Length; index++)
            {
                TestAssert.Equal(expected[index], actual[index],
                    field + " byte " + index);
            }
        }

        private sealed class FakeConfigurationFileSystem :
            IConfigurationFileSystem
        {
            private readonly string parent;
            private readonly Exception createFailure;
            private readonly Exception readFailure;

            internal FakeConfigurationFileSystem(
                string parent,
                Exception createFailure,
                Exception readFailure
            )
            {
                this.parent = parent;
                this.createFailure = createFailure;
                this.readFailure = readFailure;
            }

            internal bool ParentExists { get; set; }

            internal bool FileExists { get; set; }

            public ConfigurationPathKind GetPathKind(string path)
            {
                if (string.Equals(
                        path,
                        parent,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return ParentExists
                        ? ConfigurationPathKind.Directory
                        : ConfigurationPathKind.Missing;
                }
                return FileExists
                    ? ConfigurationPathKind.File
                    : ConfigurationPathKind.Missing;
            }

            public void CreateDirectory(string path)
            {
                if (createFailure != null)
                {
                    throw createFailure;
                }
                ParentExists = true;
            }

            public Stream OpenRead(string path)
            {
                if (readFailure != null)
                {
                    throw readFailure;
                }
                return new MemoryStream(new byte[] { 0x01 }, false);
            }
        }
    }
}
