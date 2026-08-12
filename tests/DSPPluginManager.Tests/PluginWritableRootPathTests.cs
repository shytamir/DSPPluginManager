using System;
using System.IO;
using DSPPluginManager.Hosting;

namespace DSPPluginManager.Tests
{
    internal static class PluginWritableRootPathTests
    {
        internal static void Run()
        {
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.RM17.Tests",
                Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(sandbox);
            try
            {
                VerifyDeterministicAbsoluteChild(sandbox);
                VerifyInvalidIdentifiers(sandbox);
                VerifyInvalidParents(sandbox);
                VerifyFileConflicts(sandbox);
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static void VerifyDeterministicAbsoluteChild(string sandbox)
        {
            string parent = Path.Combine(sandbox, "parent", "child", "..");
            string workingOne = Path.Combine(sandbox, "working-one");
            string workingTwo = Path.Combine(sandbox, "working-two");
            Directory.CreateDirectory(workingOne);
            Directory.CreateDirectory(workingTwo);

            string originalDirectory = Environment.CurrentDirectory;
            string first;
            string second;
            try
            {
                Environment.CurrentDirectory = workingOne;
                first = PluginWritableRootPath.Create(
                    parent,
                    "Com.ShyTamir.DSPMirrorBlueprint"
                );
                Environment.CurrentDirectory = workingTwo;
                second = PluginWritableRootPath.Create(
                    parent,
                    "com.shytamir.dspmirrorblueprint"
                );
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
            }

            TestAssert.True(Path.IsPathRooted(first),
                "Plugin writable root is not absolute.");
            TestAssert.Equal(first, Path.GetFullPath(first),
                "normalized plugin writable root");
            TestAssert.Equal(first, second,
                "case-insensitive identity writable root");
            TestAssert.Equal(
                Path.GetFullPath(Path.Combine(sandbox, "parent")),
                Path.GetDirectoryName(first),
                "plugin writable parent"
            );
            TestAssert.True(Directory.Exists(first),
                "Plugin writable root was not created.");
        }

        private static void VerifyInvalidIdentifiers(string sandbox)
        {
            foreach (string identifier in new[]
            {
                null, "", "contains space", "../escape", "contains/slash"
            })
            {
                string captured = identifier;
                TestAssert.Throws<ArgumentException>(
                    () => PluginWritableRootPath.Create(sandbox, captured),
                    "identifier",
                    "invalid"
                );
            }
        }

        private static void VerifyInvalidParents(string sandbox)
        {
            foreach (string parent in new[]
            {
                null, "", "relative-parent", "C:drive-relative"
            })
            {
                string captured = parent;
                TestAssert.Throws<ArgumentException>(
                    () => PluginWritableRootPath.Create(
                        captured,
                        "fixture.plugin"
                    ),
                    "parent"
                );
            }
        }

        private static void VerifyFileConflicts(string sandbox)
        {
            string parentFile = Path.Combine(sandbox, "parent-file");
            File.WriteAllText(parentFile, string.Empty);
            TestAssert.Throws<InvalidOperationException>(
                () => PluginWritableRootPath.Create(
                    parentFile,
                    "fixture.plugin"
                ),
                "parent",
                "file",
                "directory"
            );

            string parent = Path.Combine(sandbox, "child-conflict");
            Directory.CreateDirectory(parent);
            string childFile = Path.Combine(parent, "fixture.plugin");
            File.WriteAllText(childFile, string.Empty);
            TestAssert.Throws<InvalidOperationException>(
                () => PluginWritableRootPath.Create(
                    parent,
                    "fixture.plugin"
                ),
                "plugin writable root",
                childFile
            );
        }
    }
}
