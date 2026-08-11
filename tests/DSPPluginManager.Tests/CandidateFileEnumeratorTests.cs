using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.Tests
{
    internal static class CandidateFileEnumeratorTests
    {
        internal static void Run()
        {
            VerifyCreationOrderDoesNotAffectRealEnumeration();
            VerifyAliasesBoundariesAndFailuresAreDeterministic();
            VerifyUnavailableRootIsLocalFailure();
            TestAssert.Throws<ArgumentException>(
                () => new CandidateFileEnumerator("relative"),
                "absolute"
            );
            TestAssert.Throws<ArgumentException>(
                () => new CandidateFileEnumerator("C:relative"),
                "absolute"
            );
        }

        private static void VerifyCreationOrderDoesNotAffectRealEnumeration()
        {
            string root = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(root);
            try
            {
                CreateRealFixture(root, false);
                CandidateEnumerationResult first =
                    new CandidateFileEnumerator(root).Enumerate();

                Directory.Delete(root, true);
                Directory.CreateDirectory(root);
                CreateRealFixture(root, true);
                CandidateEnumerationResult second =
                    new CandidateFileEnumerator(root).Enumerate();

                string[] expected =
                {
                    Path.GetFullPath(Path.Combine(root, "a", "second.DLL")),
                    Path.GetFullPath(Path.Combine(root, "middle.dll")),
                    Path.GetFullPath(Path.Combine(root, "z.dll"))
                };
                AssertSequence(expected, first.CandidatePaths, "real candidates");
                AssertSequence(
                    first.CandidatePaths,
                    second.CandidatePaths,
                    "creation-order candidates"
                );
                TestAssert.Equal(0, first.Diagnostics.Count, "first diagnostics");
                TestAssert.Equal(0, second.Diagnostics.Count, "second diagnostics");
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void CreateRealFixture(string root, bool reverse)
        {
            string[] paths =
            {
                Path.Combine(root, "z.dll"),
                Path.Combine(root, "ignored.txt"),
                Path.Combine(root, "a", "second.DLL"),
                Path.Combine(root, "a", "native.so"),
                Path.Combine(root, "middle.dll")
            };
            IEnumerable<string> creationOrder = reverse ?
                paths.Reverse() : paths;
            foreach (string path in creationOrder)
            {
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, new byte[] { 0x00 });
            }
        }

        private static void VerifyAliasesBoundariesAndFailuresAreDeterministic()
        {
            string root = Path.GetFullPath(@"C:\rm10-plugins");
            FakePluginFileSystem firstFileSystem = CreateFakeFixture(root, false);
            FakePluginFileSystem secondFileSystem = CreateFakeFixture(root, true);

            CandidateEnumerationResult first = new CandidateFileEnumerator(
                root,
                firstFileSystem
            ).Enumerate();
            CandidateEnumerationResult second = new CandidateFileEnumerator(
                root,
                secondFileSystem
            ).Enumerate();

            string[] expected =
            {
                Path.Combine(root, "real.dll"),
                Path.Combine(root, "sub", "child.DLL"),
                Path.Combine(root, "z.dll")
            };
            AssertSequence(expected, first.CandidatePaths, "bounded candidates");
            TestAssert.Equal(
                1,
                firstFileSystem.EnumerationCount(Path.Combine(root, "sub")),
                "canonical directory traversal count"
            );
            AssertSequence(
                Serialize(first.Diagnostics),
                Serialize(second.Diagnostics),
                "deterministic diagnostics"
            );
            TestAssert.Equal(3, first.Diagnostics.Count, "diagnostic count");
            TestAssert.Equal(
                1,
                first.Diagnostics.Count(diagnostic =>
                    diagnostic.Code ==
                        CandidateEnumerationDiagnosticCode.OutsideRootLink
                ),
                "outside-link diagnostic count"
            );
            TestAssert.Equal(
                2,
                first.Diagnostics.Count(diagnostic =>
                    diagnostic.Code ==
                        CandidateEnumerationDiagnosticCode.UnreadableEntry
                ),
                "unreadable diagnostic count"
            );
        }

        private static FakePluginFileSystem CreateFakeFixture(
            string root,
            bool reverse
        )
        {
            string sub = Path.Combine(root, "sub");
            string bad = Path.Combine(root, "bad");
            string[] rootEntries =
            {
                Path.Combine(root, "z.dll"),
                Path.Combine(root, "outside-link"),
                Path.Combine(root, "broken.dll"),
                Path.Combine(root, "sub"),
                Path.Combine(root, "sub-alias"),
                Path.Combine(root, "alias.dll"),
                Path.Combine(root, "real.dll"),
                Path.Combine(root, "bad")
            };
            string[] subEntries =
            {
                Path.Combine(sub, "ignored.txt"),
                Path.Combine(sub, "child.DLL")
            };
            if (reverse)
            {
                Array.Reverse(rootEntries);
                Array.Reverse(subEntries);
            }

            FakePluginFileSystem fileSystem = new FakePluginFileSystem();
            fileSystem.AddDirectory(root, "dir-root", rootEntries);
            fileSystem.AddDirectory(sub, "dir-sub", subEntries);
            fileSystem.AddEntry(
                Path.Combine(root, "sub-alias"),
                "dir-sub",
                true,
                sub
            );
            fileSystem.AddDirectory(
                bad,
                "dir-bad",
                new UnauthorizedAccessException()
            );
            fileSystem.AddEntry(Path.Combine(root, "z.dll"), "file-z", false);
            fileSystem.AddEntry(
                Path.Combine(root, "outside-link"),
                "dir-outside",
                true,
                Path.GetFullPath(@"C:\outside\linked")
            );
            fileSystem.AddInspectionFailure(
                Path.Combine(root, "broken.dll"),
                new IOException()
            );
            fileSystem.AddEntry(
                Path.Combine(root, "alias.dll"),
                "file-real",
                false,
                Path.Combine(root, "real.dll")
            );
            fileSystem.AddEntry(
                Path.Combine(root, "real.dll"),
                "file-real",
                false
            );
            fileSystem.AddEntry(
                Path.Combine(sub, "child.DLL"),
                "file-child",
                false
            );
            fileSystem.AddEntry(
                Path.Combine(sub, "ignored.txt"),
                "file-ignored",
                false
            );
            return fileSystem;
        }

        private static void VerifyUnavailableRootIsLocalFailure()
        {
            string root = Path.GetFullPath(@"C:\missing-rm10-root");
            FakePluginFileSystem fileSystem = new FakePluginFileSystem();
            fileSystem.AddInspectionFailure(
                root,
                new DirectoryNotFoundException()
            );
            CandidateEnumerationResult result = new CandidateFileEnumerator(
                root,
                fileSystem
            ).Enumerate();
            TestAssert.Equal(0, result.CandidatePaths.Count, "missing-root candidates");
            TestAssert.Equal(1, result.Diagnostics.Count, "missing-root diagnostics");
            TestAssert.Equal(
                CandidateEnumerationDiagnosticCode.UnreadableEntry,
                result.Diagnostics[0].Code,
                "missing-root diagnostic"
            );
        }

        private static string[] Serialize(
            IReadOnlyList<CandidateEnumerationDiagnostic> diagnostics
        )
        {
            return diagnostics.Select(diagnostic =>
                diagnostic.Code + "|" + diagnostic.Path + "|" +
                diagnostic.Detail
            ).ToArray();
        }

        private static void AssertSequence<T>(
            IEnumerable<T> expected,
            IEnumerable<T> actual,
            string field
        )
        {
            TestAssert.Equal(
                string.Join("\n", expected),
                string.Join("\n", actual),
                field
            );
        }

        private sealed class FakePluginFileSystem : IPluginFileSystem
        {
            private readonly Dictionary<string, PluginFileSystemEntry> entries =
                new Dictionary<string, PluginFileSystemEntry>(
                    StringComparer.OrdinalIgnoreCase
                );
            private readonly Dictionary<string, string[]> children =
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, Exception> inspectionFailures =
                new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, Exception> enumerationFailures =
                new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> enumerationCounts =
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public string[] GetEntries(string directoryPath)
            {
                int count;
                enumerationCounts.TryGetValue(directoryPath, out count);
                enumerationCounts[directoryPath] = count + 1;
                Exception failure;
                if (enumerationFailures.TryGetValue(directoryPath, out failure))
                {
                    throw failure;
                }
                return (string[])children[directoryPath].Clone();
            }

            public PluginFileSystemEntry Inspect(string path)
            {
                Exception failure;
                if (inspectionFailures.TryGetValue(path, out failure))
                {
                    throw failure;
                }
                return entries[path];
            }

            internal void AddDirectory(
                string path,
                string identity,
                string[] directoryChildren
            )
            {
                AddEntry(path, identity, true);
                children[path] = directoryChildren;
            }

            internal void AddDirectory(
                string path,
                string identity,
                Exception enumerationFailure
            )
            {
                AddEntry(path, identity, true);
                enumerationFailures[path] = enumerationFailure;
            }

            internal void AddEntry(
                string path,
                string identity,
                bool isDirectory,
                string canonicalPath = null
            )
            {
                entries[path] = new PluginFileSystemEntry(
                    canonicalPath ?? path,
                    identity,
                    isDirectory
                );
            }

            internal void AddInspectionFailure(string path, Exception exception)
            {
                inspectionFailures[path] = exception;
            }

            internal int EnumerationCount(string path)
            {
                int count;
                enumerationCounts.TryGetValue(path, out count);
                return count;
            }
        }
    }
}
