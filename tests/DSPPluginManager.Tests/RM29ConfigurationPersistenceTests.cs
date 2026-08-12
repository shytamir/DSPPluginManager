using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DSPPluginManager.Configuration;

namespace DSPPluginManager.Tests
{
    internal static class RM29ConfigurationPersistenceTests
    {
        internal static void Run(string unityHostPath, string contractPath)
        {
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.RM29.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(sandbox);
            try
            {
                Assembly unityHost = FindLoadedAssembly(unityHostPath);
                Assembly contract = FindLoadedAssembly(contractPath);
                Type serviceType = unityHost.GetType(
                    "DSPPluginManager.UnityHost.PluginConfigurationService",
                    true
                );
                Type configurationType = contract.GetType(
                    "DSPPluginManager.Contracts.PluginConfiguration",
                    true
                );
                Type shortcutType = contract.GetType(
                    "DSPPluginManager.Contracts.KeyboardShortcut",
                    true
                );
                Type keyCodeType = shortcutType.GetConstructors().Single()
                    .GetParameters()[0].ParameterType;

                VerifyAutosaveAndReload(
                    sandbox,
                    serviceType,
                    configurationType,
                    shortcutType,
                    keyCodeType
                );
                VerifyAtomicFailureStages();
                VerifyFailureDiagnosticsAndWriteBlock(
                    sandbox,
                    serviceType,
                    configurationType
                );
                VerifyPerPluginSerialization(
                    sandbox,
                    serviceType,
                    configurationType
                );
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static void VerifyAutosaveAndReload(
            string sandbox,
            Type serviceType,
            Type configurationType,
            Type shortcutType,
            Type keyCodeType
        )
        {
            string directory = Path.Combine(sandbox, "round-trip");
            PluginConfigurationScope scope = PluginConfigurationScope.Create(
                directory,
                "Fixture.Plugin"
            );
            PluginConfigurationDocument document =
                PluginConfigurationDocument.Parse(
                    "[Zulu]\nTail = keep\n" +
                    "[Guide]\nLegacySave = legacy\nCurrentSave = current\n"
                );
            List<string> warnings = new List<string>();
            object service = CreateService(
                serviceType,
                scope,
                document,
                warnings,
                null
            );
            object configuration = GetProperty(service, "Handle");

            object enabled = Bind(configurationType, typeof(bool)).Invoke(
                configuration,
                new object[]
                {
                    "General", "Enabled", true, "enable the fixture"
                }
            );
            TestAssert.True(File.Exists(scope.FilePath),
                "A new binding did not autosave before returning.");

            object text = Bind(configurationType, typeof(string)).Invoke(
                configuration,
                new object[]
                {
                    "Alpha", "Text", " Ω;phase=3=value\n ",
                    "text value"
                }
            );
            object shortcutValue = CreateShortcut(
                shortcutType,
                keyCodeType,
                "F9",
                "LeftShift"
            );
            Bind(configurationType, shortcutType).Invoke(
                configuration,
                new[]
                {
                    "General", "Shortcut", shortcutValue,
                    "activation shortcut"
                }
            );

            byte[] bytes = File.ReadAllBytes(scope.FilePath);
            TestAssert.True(
                bytes.Length < 3 ||
                bytes[0] != 0xEF || bytes[1] != 0xBB || bytes[2] != 0xBF,
                "Configuration snapshot unexpectedly contains a UTF-8 BOM."
            );
            string snapshot = new UTF8Encoding(false, true).GetString(bytes);
            AssertOrdered(snapshot, "[Alpha]", "[General]", "[Guide]", "[Zulu]");
            AssertOrdered(snapshot, "CurrentSave = current", "LegacySave = legacy");
            AssertContains(snapshot, "# enable the fixture");
            AssertContains(snapshot, "# text value");
            AssertContains(snapshot, "# activation shortcut");
            AssertContains(snapshot, "Text = \\u0020Ω;phase=3=value\\n\\u0020");
            AssertContains(snapshot, "Shortcut = F9 + LeftShift");
            TestAssert.Equal(3, GetInt(service, "RequestedPersistenceVersion"),
                "new-bind requested version");
            TestAssert.Equal(3, GetInt(service, "PersistedVersion"),
                "new-bind persisted version");

            SetRawValue(enabled, false);
            TestAssert.Equal(4, GetInt(service, "RequestedPersistenceVersion"),
                "changed-value requested version");
            TestAssert.Equal(4, GetInt(service, "PersistedVersion"),
                "changed-value persisted version");
            AssertContains(File.ReadAllText(scope.FilePath), "Enabled = false");
            TestAssert.Equal(" Ω;phase=3=value\n ", GetRawValue(text),
                "persisted string changed in memory");

            File.Delete(scope.FilePath);
            configurationType.GetMethod("Save").Invoke(configuration, null);
            TestAssert.True(File.Exists(scope.FilePath),
                "Explicit save did not publish a complete snapshot.");
            TestAssert.Equal(5, GetInt(service, "RequestedPersistenceVersion"),
                "explicit requested version");
            TestAssert.Equal(5, GetInt(service, "PersistedVersion"),
                "explicit persisted version");
            AssertContains(File.ReadAllText(scope.FilePath), "LegacySave = legacy");

            PluginConfigurationDocument reloaded =
                PluginConfigurationDocument.Parse(
                    File.ReadAllText(scope.FilePath, Encoding.UTF8)
                );
            PluginConfigurationScope reloadScope =
                PluginConfigurationScope.Create(directory, "fixture.plugin");
            object reloadService = CreateService(
                serviceType,
                reloadScope,
                reloaded,
                new List<string>(),
                null
            );
            object reloadConfiguration = GetProperty(reloadService, "Handle");
            object current = Bind(configurationType, typeof(string)).Invoke(
                reloadConfiguration,
                new object[]
                {
                    "Guide", "CurrentSave", "fallback", "current save"
                }
            );
            TestAssert.Equal("current", GetRawValue(current),
                "late current-save binding");
            configurationType.GetMethod("Save").Invoke(
                reloadConfiguration,
                null
            );
            PluginConfigurationDocument twiceReloaded =
                PluginConfigurationDocument.Parse(
                    File.ReadAllText(scope.FilePath, Encoding.UTF8)
                );
            string legacy;
            TestAssert.True(
                twiceReloaded.TryGetSerializedValue(
                    "Guide",
                    "LegacySave",
                    out legacy
                ),
                "Late autosave discarded the unbound legacy key."
            );
            TestAssert.Equal("legacy", legacy, "retained legacy value");
            TestAssert.Equal(0, warnings.Count, "successful persistence warnings");
        }

        private static void VerifyAtomicFailureStages()
        {
            foreach (ConfigurationPersistenceFailureStage stage in
                Enum.GetValues(typeof(ConfigurationPersistenceFailureStage)))
            {
                bool existing = stage != ConfigurationPersistenceFailureStage.Move;
                FakePersistenceFileSystem fileSystem =
                    new FakePersistenceFileSystem(stage, existing);
                PluginConfigurationPersistence persistence =
                    new PluginConfigurationPersistence(fileSystem);
                ConfigurationPersistenceResult result = persistence.Save(
                    @"C:\config\fixture.plugin.cfg",
                    "[General]\r\nEnabled = true\r\n"
                );
                TestAssert.True(!result.Succeeded,
                    stage + " unexpectedly succeeded.");
                TestAssert.Equal<ConfigurationPersistenceFailureStage?>(
                    stage,
                    result.FailureStage,
                    stage + " failure stage"
                );
                TestAssert.Equal("original", fileSystem.FinalContents,
                    stage + " final contents");
                TestAssert.Equal(0, fileSystem.TemporaryCount,
                    stage + " temporary cleanup");
                TestAssert.True(!fileSystem.Published,
                    stage + " unexpectedly published.");
            }

            FakePersistenceFileSystem replace =
                new FakePersistenceFileSystem(null, true);
            ConfigurationPersistenceResult replaced =
                new PluginConfigurationPersistence(replace).Save(
                    @"C:\config\fixture.plugin.cfg",
                    "replacement"
                );
            TestAssert.True(replaced.Succeeded, "replace success");
            TestAssert.Equal("replacement", replace.FinalContents,
                "replaced contents");
            TestAssert.True(replace.PublishedAfterCloseAndFlush,
                "Replace occurred before close and durable flush.");

            FakePersistenceFileSystem move =
                new FakePersistenceFileSystem(null, false);
            ConfigurationPersistenceResult moved =
                new PluginConfigurationPersistence(move).Save(
                    @"C:\config\fixture.plugin.cfg",
                    "first"
                );
            TestAssert.True(moved.Succeeded, "move success");
            TestAssert.Equal("first", move.FinalContents, "moved contents");
            TestAssert.True(move.PublishedAfterCloseAndFlush,
                "Move occurred before close and durable flush.");
        }

        private static void VerifyFailureDiagnosticsAndWriteBlock(
            string sandbox,
            Type serviceType,
            Type configurationType
        )
        {
            string directory = Path.Combine(sandbox, "failed-save");
            PluginConfigurationScope scope = PluginConfigurationScope.Create(
                directory,
                "Failed.Plugin"
            );
            List<string> warnings = new List<string>();
            RecordingPersistence failure = new RecordingPersistence(false);
            object service = CreateService(
                serviceType,
                scope,
                PluginConfigurationDocument.Parse(string.Empty),
                warnings,
                failure
            );
            object configuration = GetProperty(service, "Handle");
            object entry = Bind(configurationType, typeof(bool)).Invoke(
                configuration,
                new object[] { "General", "Enabled", true, "description" }
            );
            SetRawValue(entry, false);
            configurationType.GetMethod("Save").Invoke(configuration, null);
            TestAssert.Equal(false, GetRawValue(entry),
                "failed persistence in-memory value");
            TestAssert.Equal(3, GetInt(service, "RequestedPersistenceVersion"),
                "failed requested version");
            TestAssert.Equal(0, GetInt(service, "PersistedVersion"),
                "failed persisted version");
            TestAssert.Equal(3, warnings.Count, "failed persistence warnings");
            foreach (string warning in warnings)
            {
                AssertContains(warning, "failed.plugin");
                AssertContains(warning, "persisted version remains 0");
                AssertContains(warning, "Replace");
                AssertContains(warning, "In-memory values remain usable");
            }

            string blockedDirectory = Path.Combine(sandbox, "read-blocked");
            string blockedPath = Path.Combine(
                blockedDirectory,
                "blocked.plugin.cfg"
            );
            Directory.CreateDirectory(blockedPath);
            PluginConfigurationScope blockedScope =
                PluginConfigurationScope.Create(
                    blockedDirectory,
                    "blocked.plugin"
                );
            RecordingPersistence shouldNotRun =
                new RecordingPersistence(true);
            List<string> blockedWarnings = new List<string>();
            object blocked = CreateService(
                serviceType,
                blockedScope,
                PluginConfigurationDocument.Parse(string.Empty),
                blockedWarnings,
                shouldNotRun
            );
            object blockedConfiguration = GetProperty(blocked, "Handle");
            object blockedEntry = Bind(configurationType, typeof(string)).Invoke(
                blockedConfiguration,
                new object[] { "General", "Text", "usable", "description" }
            );
            configurationType.GetMethod("Save").Invoke(
                blockedConfiguration,
                null
            );
            TestAssert.Equal("usable", GetRawValue(blockedEntry),
                "write-blocked in-memory value");
            TestAssert.Equal(0, shouldNotRun.CallCount,
                "write-blocked persistence calls");
            TestAssert.Equal(2, blockedWarnings.Count,
                "write-blocked warnings");
            foreach (string warning in blockedWarnings)
            {
                AssertContains(warning, "writes are blocked for this process");
                AssertContains(warning, "persisted version remains 0");
            }
            TestAssert.True(Directory.Exists(blockedPath),
                "Write-blocked final path was touched.");
        }

        private static void VerifyPerPluginSerialization(
            string sandbox,
            Type serviceType,
            Type configurationType
        )
        {
            RecordingPersistence persistence = new RecordingPersistence(true);
            persistence.DelayMilliseconds = 10;
            object service = CreateService(
                serviceType,
                PluginConfigurationScope.Create(
                    Path.Combine(sandbox, "serialized"),
                    "serialized.plugin"
                ),
                PluginConfigurationDocument.Parse(string.Empty),
                new List<string>(),
                persistence
            );
            object configuration = GetProperty(service, "Handle");
            object entry = Bind(configurationType, typeof(string)).Invoke(
                configuration,
                new object[] { "General", "Text", "initial", "description" }
            );
            Task[] assignments = Enumerable.Range(0, 8)
                .Select(index => Task.Run(() =>
                    SetRawValue(entry, "value-" + index)
                ))
                .ToArray();
            Task.WaitAll(assignments);
            TestAssert.Equal(1, persistence.MaximumConcurrentCalls,
                "per-plugin persistence concurrency");
        }

        private static object CreateService(
            Type serviceType,
            PluginConfigurationScope scope,
            PluginConfigurationDocument document,
            List<string> warnings,
            IPluginConfigurationPersistence persistence
        )
        {
            object[] arguments = persistence == null
                ? new object[]
                {
                    scope,
                    document,
                    new Action<string>(warnings.Add)
                }
                : new object[]
                {
                    scope,
                    document,
                    new Action<string>(warnings.Add),
                    persistence
                };
            return Activator.CreateInstance(
                serviceType,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                arguments,
                null
            );
        }

        private static MethodInfo Bind(Type configurationType, Type valueType)
        {
            return configurationType.GetMethods().Single(method =>
                method.Name == "Bind" &&
                method.GetParameters()[2].ParameterType == valueType
            );
        }

        private static object CreateShortcut(
            Type shortcutType,
            Type keyCodeType,
            string main,
            params string[] held
        )
        {
            Array heldKeys = Array.CreateInstance(keyCodeType, held.Length);
            for (int index = 0; index < held.Length; index++)
            {
                heldKeys.SetValue(Enum.Parse(keyCodeType, held[index]), index);
            }
            return shortcutType.GetConstructors().Single().Invoke(new object[]
            {
                Enum.Parse(keyCodeType, main),
                heldKeys
            });
        }

        private static object GetProperty(object target, string name)
        {
            return target.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(target, null);
        }

        private static int GetInt(object target, string name)
        {
            return (int)GetProperty(target, name);
        }

        private static object GetRawValue(object entry)
        {
            return entry.GetType().GetProperty("Value").GetValue(entry, null);
        }

        private static void SetRawValue(object entry, object value)
        {
            entry.GetType().GetProperty("Value").SetValue(entry, value, null);
        }

        private static void AssertContains(string actual, string expected)
        {
            TestAssert.True(
                actual.IndexOf(expected, StringComparison.Ordinal) >= 0,
                "Expected text '" + expected + "' was absent from: " + actual
            );
        }

        private static void AssertOrdered(
            string contents,
            params string[] expected
        )
        {
            int previous = -1;
            foreach (string value in expected)
            {
                int current = contents.IndexOf(value, StringComparison.Ordinal);
                TestAssert.True(current > previous,
                    "Expected deterministic order for '" + value + "'.");
                previous = current;
            }
        }

        private static Assembly FindLoadedAssembly(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return AppDomain.CurrentDomain.GetAssemblies().Single(assembly =>
                !assembly.IsDynamic &&
                string.Equals(
                    Path.GetFullPath(assembly.Location),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        private sealed class RecordingPersistence :
            IPluginConfigurationPersistence
        {
            private readonly bool succeed;
            private int activeCalls;
            private int callCount;
            private int maximumConcurrentCalls;

            internal RecordingPersistence(bool succeed)
            {
                this.succeed = succeed;
            }

            internal int DelayMilliseconds { get; set; }

            internal int CallCount
            {
                get { return callCount; }
            }

            internal int MaximumConcurrentCalls
            {
                get { return maximumConcurrentCalls; }
            }

            public ConfigurationPersistenceResult Save(
                string finalPath,
                string contents
            )
            {
                Interlocked.Increment(ref callCount);
                int active = Interlocked.Increment(ref activeCalls);
                int observed;
                do
                {
                    observed = maximumConcurrentCalls;
                    if (active <= observed)
                    {
                        break;
                    }
                }
                while (Interlocked.CompareExchange(
                    ref maximumConcurrentCalls,
                    active,
                    observed
                ) != observed);
                try
                {
                    if (DelayMilliseconds != 0)
                    {
                        Thread.Sleep(DelayMilliseconds);
                    }
                    return succeed
                        ? ConfigurationPersistenceResult.Success()
                        : ConfigurationPersistenceResult.Failed(
                            ConfigurationPersistenceFailureStage.Replace,
                            new IOException("simulated replace failure")
                        );
                }
                finally
                {
                    Interlocked.Decrement(ref activeCalls);
                }
            }
        }

        private sealed class FakePersistenceFileSystem :
            IConfigurationPersistenceFileSystem
        {
            private readonly ConfigurationPersistenceFailureStage? failure;
            private readonly Dictionary<string, TrackingStream> temporary =
                new Dictionary<string, TrackingStream>();
            private readonly bool existing;

            internal FakePersistenceFileSystem(
                ConfigurationPersistenceFailureStage? failure,
                bool existing
            )
            {
                this.failure = failure;
                this.existing = existing;
                FinalContents = "original";
            }

            internal string FinalContents { get; private set; }

            internal int TemporaryCount
            {
                get { return temporary.Count; }
            }

            internal bool Published { get; private set; }

            internal bool PublishedAfterCloseAndFlush { get; private set; }

            public Stream CreateTemporaryFile(string path)
            {
                TrackingStream stream = new TrackingStream(
                    failure == ConfigurationPersistenceFailureStage.TemporaryWrite
                );
                temporary.Add(path, stream);
                return stream;
            }

            public void FlushToDisk(Stream stream)
            {
                if (failure == ConfigurationPersistenceFailureStage.Flush)
                {
                    throw new IOException("simulated flush failure");
                }
                ((TrackingStream)stream).DurablyFlushed = true;
            }

            public ConfigurationPathKind GetPathKind(string path)
            {
                if (failure == ConfigurationPersistenceFailureStage.FinalPath)
                {
                    throw new IOException("simulated final-path failure");
                }
                return existing
                    ? ConfigurationPathKind.File
                    : ConfigurationPathKind.Missing;
            }

            public void Replace(string sourcePath, string destinationPath)
            {
                if (failure == ConfigurationPersistenceFailureStage.Replace)
                {
                    throw new IOException("simulated replace failure");
                }
                Publish(sourcePath);
            }

            public void Move(string sourcePath, string destinationPath)
            {
                if (failure == ConfigurationPersistenceFailureStage.Move)
                {
                    throw new IOException("simulated move failure");
                }
                Publish(sourcePath);
            }

            public void Delete(string path)
            {
                temporary.Remove(path);
            }

            private void Publish(string path)
            {
                TrackingStream stream = temporary[path];
                PublishedAfterCloseAndFlush =
                    stream.Disposed && stream.DurablyFlushed;
                FinalContents = Encoding.UTF8.GetString(stream.ToArray());
                temporary.Remove(path);
                Published = true;
            }
        }

        private sealed class TrackingStream : MemoryStream
        {
            private readonly bool failWrite;

            internal TrackingStream(bool failWrite)
            {
                this.failWrite = failWrite;
            }

            internal bool Disposed { get; private set; }

            internal bool DurablyFlushed { get; set; }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (failWrite)
                {
                    throw new IOException("simulated temporary-write failure");
                }
                base.Write(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
