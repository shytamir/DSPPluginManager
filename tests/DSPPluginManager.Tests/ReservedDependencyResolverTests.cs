using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DSPPluginManager.Dependencies;

namespace DSPPluginManager.Tests
{
    internal static class ReservedDependencyResolverTests
    {
        private static readonly AssemblyName HarmonyRequest = new AssemblyName(
            "0Harmony, Version=2.5.5.0, Culture=neutral, PublicKeyToken=null"
        );

        private static readonly string[] RuntimeFiles =
        {
            "0Harmony.dll",
            "MonoMod.RuntimeDetour.dll",
            "MonoMod.Utils.dll",
            "Mono.Cecil.dll"
        };

        internal static void Run(string validatedRuntimeDirectory)
        {
            string source = Path.GetFullPath(validatedRuntimeDirectory);
            TestAssert.True(
                Directory.Exists(source),
                "The validated managed dependency directory is missing."
            );

            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(sandbox);
            try
            {
                VerifyUnrelatedRequest(source, sandbox);
                VerifyMissingFile(source, sandbox);
                VerifyCorruptFile(source, sandbox);
                VerifyContentMismatch(source, sandbox);
                VerifyWrongHostIdentity(source, sandbox);
                VerifyWrongRequestedVersion(source, sandbox);
                VerifyPluginLocalDuplicate(source, sandbox);
                VerifyAlreadyLoadedConflict(source, sandbox);
                VerifySuccessfulResolution(source, sandbox);
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static void VerifyUnrelatedRequest(
            string source,
            string sandbox
        )
        {
            Fixture fixture = CreateFixture(source, sandbox, "unrelated");
            using (ReservedDependencyResolver resolver = fixture.CreateResolver())
            {
                Assembly result = resolver.ResolveForTest(
                    new AssemblyName(
                        "Unrelated.Library, Version=1.0.0.0, Culture=neutral"
                    ),
                    Assembly.GetExecutingAssembly()
                );
                TestAssert.Equal(
                    null,
                    result,
                    "unrelated assembly resolution result"
                );
            }
        }

        private static void VerifyMissingFile(string source, string sandbox)
        {
            Fixture fixture = CreateFixture(source, sandbox, "missing");
            string missingPath = Path.Combine(
                fixture.DependencyDirectory,
                "0Harmony.dll"
            );
            File.Delete(missingPath);

            using (ReservedDependencyResolver resolver = fixture.CreateResolver())
            {
                ExpectFailure(
                    resolver,
                    HarmonyRequest,
                    "required host-owned file is missing",
                    missingPath
                );
            }
        }

        private static void VerifyCorruptFile(string source, string sandbox)
        {
            Fixture fixture = CreateFixture(source, sandbox, "corrupt");
            string corruptPath = Path.Combine(
                fixture.DependencyDirectory,
                "0Harmony.dll"
            );
            File.WriteAllText(corruptPath, "not a managed assembly");

            using (ReservedDependencyResolver resolver = fixture.CreateResolver())
            {
                ExpectFailure(
                    resolver,
                    HarmonyRequest,
                    "not a readable managed assembly",
                    corruptPath
                );
            }
        }

        private static void VerifyContentMismatch(string source, string sandbox)
        {
            Fixture fixture = CreateFixture(
                source,
                sandbox,
                "content-mismatch"
            );
            string changedPath = Path.Combine(
                fixture.DependencyDirectory,
                "0Harmony.dll"
            );
            using (FileStream stream = new FileStream(
                changedPath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.None
            ))
            {
                stream.WriteByte(0);
            }

            using (ReservedDependencyResolver resolver = fixture.CreateResolver())
            {
                ExpectFailure(
                    resolver,
                    HarmonyRequest,
                    "failed SHA-256 integrity validation",
                    changedPath
                );
            }
        }

        private static void VerifyWrongHostIdentity(
            string source,
            string sandbox
        )
        {
            Fixture fixture = CreateFixture(
                source,
                sandbox,
                "wrong-host-identity"
            );
            string selectedPath = Path.Combine(
                fixture.DependencyDirectory,
                "0Harmony.dll"
            );
            File.Copy(
                Path.Combine(source, "MonoMod.Utils.dll"),
                selectedPath,
                true
            );

            using (ReservedDependencyResolver resolver = fixture.CreateResolver())
            {
                ExpectFailure(
                    resolver,
                    HarmonyRequest,
                    "has identity",
                    "MonoMod.Utils",
                    "expected '0Harmony, Version=2.5.5.0"
                );
            }
        }

        private static void VerifyWrongRequestedVersion(
            string source,
            string sandbox
        )
        {
            Fixture fixture = CreateFixture(
                source,
                sandbox,
                "wrong-request-version"
            );
            AssemblyName wrongVersion = new AssemblyName(
                "0Harmony, Version=9.0.0.0, Culture=neutral, PublicKeyToken=null"
            );

            using (ReservedDependencyResolver resolver = fixture.CreateResolver())
            {
                ExpectFailure(
                    resolver,
                    wrongVersion,
                    "requested identity is not approved",
                    "version 2.5.5.0"
                );
            }
        }

        private static void VerifyPluginLocalDuplicate(
            string source,
            string sandbox
        )
        {
            Fixture fixture = CreateFixture(
                source,
                sandbox,
                "plugin-duplicate"
            );
            string nested = Path.Combine(
                fixture.PluginDirectory,
                "ExamplePlugin",
                "lib"
            );
            Directory.CreateDirectory(nested);
            string duplicatePath = Path.Combine(nested, "renamed-copy.dll");
            File.Copy(
                Path.Combine(source, "0Harmony.dll"),
                duplicatePath
            );

            using (ReservedDependencyResolver resolver = fixture.CreateResolver())
            {
                ExpectFailure(
                    resolver,
                    HarmonyRequest,
                    "plugin-local reserved dependency duplicate",
                    duplicatePath
                );
            }
        }

        private static void VerifyAlreadyLoadedConflict(
            string source,
            string sandbox
        )
        {
            Fixture fixture = CreateFixture(
                source,
                sandbox,
                "loaded-conflict"
            );
            string conflictingPath = Path.Combine(
                fixture.PluginDirectory,
                "0Harmony.dll"
            );
            LoadedDependencyAssembly conflict = new LoadedDependencyAssembly(
                Assembly.GetExecutingAssembly(),
                new AssemblyName(
                    "0Harmony, Version=9.0.0.0, Culture=neutral, PublicKeyToken=null"
                ),
                conflictingPath
            );
            LoadedAssemblyProvider provider = () =>
                new[] { conflict };

            using (ReservedDependencyResolver resolver =
                new ReservedDependencyResolver(
                    fixture.DependencyDirectory,
                    fixture.PluginDirectory,
                    provider
                ))
            {
                ExpectFailure(
                    resolver,
                    HarmonyRequest,
                    "conflicting reserved assembly is already loaded",
                    "Version=9.0.0.0",
                    conflictingPath
                );
            }
        }

        private static void VerifySuccessfulResolution(
            string source,
            string sandbox
        )
        {
            string pluginDirectory = Path.Combine(sandbox, "success-plugins");
            Directory.CreateDirectory(pluginDirectory);
            Fixture fixture = new Fixture(source, pluginDirectory);
            Dictionary<string, string> selectedVersions =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "0Harmony", "2.5.5.0" },
                    { "MonoMod.RuntimeDetour", "21.9.19.1" },
                    { "MonoMod.Utils", "21.9.19.1" },
                    { "Mono.Cecil", "0.10.4.0" }
                };
            AssemblyName[] approvedRequests =
            {
                HarmonyRequest,
                new AssemblyName(
                    "MonoMod.RuntimeDetour, Version=21.8.19.1, " +
                    "Culture=neutral, PublicKeyToken=null"
                ),
                new AssemblyName(
                    "MonoMod.Utils, Version=21.8.19.1, Culture=neutral, " +
                    "PublicKeyToken=null"
                ),
                new AssemblyName(
                    "Mono.Cecil, Version=0.10.4.0, Culture=neutral, " +
                    "PublicKeyToken=50cebf1cceb9d05e"
                )
            };

            using (ReservedDependencyResolver resolver = fixture.CreateResolver())
            {
                resolver.Install();
                foreach (AssemblyName request in approvedRequests)
                {
                    Assembly resolved = Assembly.Load(request);
                    TestAssert.Equal(
                        request.Name,
                        resolved.GetName().Name,
                        request.Name + " selected name"
                    );
                    TestAssert.Equal(
                        selectedVersions[request.Name],
                        resolved.GetName().Version.ToString(),
                        request.Name + " selected version"
                    );
                    TestAssert.Equal(
                        Path.Combine(
                            fixture.DependencyDirectory,
                            request.Name + ".dll"
                        ),
                        Path.GetFullPath(resolved.Location),
                        request.Name + " selected path"
                    );

                    Assembly loadedResult = resolver.ResolveForTest(
                        request,
                        Assembly.GetExecutingAssembly()
                    );
                    TestAssert.True(
                        object.ReferenceEquals(resolved, loadedResult),
                        request.Name +
                        " did not reuse the validated loaded assembly."
                    );
                }

                VerifySelectedMonoModRequest(
                    resolver,
                    "MonoMod.RuntimeDetour"
                );
                VerifySelectedMonoModRequest(resolver, "MonoMod.Utils");
            }
        }

        private static void VerifySelectedMonoModRequest(
            ReservedDependencyResolver resolver,
            string name
        )
        {
            AssemblyName request = new AssemblyName(
                name + ", Version=21.9.19.1, Culture=neutral, " +
                "PublicKeyToken=null"
            );
            Assembly resolved = resolver.ResolveForTest(
                request,
                Assembly.GetExecutingAssembly()
            );
            TestAssert.Equal(
                "21.9.19.1",
                resolved.GetName().Version.ToString(),
                name + " direct selected-version request"
            );
        }

        private static void ExpectFailure(
            ReservedDependencyResolver resolver,
            AssemblyName request,
            params string[] details
        )
        {
            List<string> expected = new List<string>
            {
                request.Name,
                "DSPPluginManager.Tests"
            };
            expected.AddRange(details);
            TestAssert.Throws<InvalidOperationException>(
                () => resolver.ResolveForTest(
                    request,
                    Assembly.GetExecutingAssembly()
                ),
                expected.ToArray()
            );
        }

        private static Fixture CreateFixture(
            string source,
            string sandbox,
            string name
        )
        {
            string root = Path.Combine(sandbox, name);
            string dependencies = Path.Combine(root, "managed-dependencies");
            string plugins = Path.Combine(root, "plugins");
            Directory.CreateDirectory(dependencies);
            Directory.CreateDirectory(plugins);
            foreach (string fileName in RuntimeFiles)
            {
                File.Copy(
                    Path.Combine(source, fileName),
                    Path.Combine(dependencies, fileName)
                );
            }
            return new Fixture(dependencies, plugins);
        }

        private sealed class Fixture
        {
            internal Fixture(
                string dependencyDirectory,
                string pluginDirectory
            )
            {
                DependencyDirectory = dependencyDirectory;
                PluginDirectory = pluginDirectory;
            }

            internal string DependencyDirectory { get; }

            internal string PluginDirectory { get; }

            internal ReservedDependencyResolver CreateResolver()
            {
                return new ReservedDependencyResolver(
                    DependencyDirectory,
                    PluginDirectory
                );
            }
        }
    }
}
