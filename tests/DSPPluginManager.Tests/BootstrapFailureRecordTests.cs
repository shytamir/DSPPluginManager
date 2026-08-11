using System;
using System.IO;
using System.Text;
using DSPPluginManager.Bootstrap;

namespace DSPPluginManager.Tests
{
    internal static class BootstrapFailureRecordTests
    {
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
                VerifySuccessfulRecord(sandbox);
                VerifyUnavailableDirectory(sandbox);
                VerifyDeniedWritePreservesFailure(sandbox);
                VerifyRelativeExecutableIsRejected();
            }
            finally
            {
                Directory.Delete(sandbox, true);
            }
        }

        private static void VerifySuccessfulRecord(string sandbox)
        {
            string gameDirectory = Path.Combine(sandbox, "game");
            Directory.CreateDirectory(gameDirectory);
            string executable = Path.Combine(gameDirectory, "DSPGAME.exe");
            File.WriteAllText(executable, string.Empty);

            BootstrapFailureContext context = new BootstrapFailureContext(
                "resolver\r\nspoofed-field: value",
                Path.Combine(sandbox, "host", "Bootstrap.dll"),
                executable,
                Path.Combine(sandbox, "game", "Managed") + "\ncontinued",
                null,
                Path.Combine(sandbox, "host", "dependencies")
            );
            Exception failure = CreateMultilineFailure();

            string diagnosticPath;
            bool written = BootstrapFailureRecord.TryWrite(
                context,
                failure,
                out diagnosticPath
            );

            TestAssert.True(written, "The bootstrap failure record was not written.");
            TestAssert.True(
                File.Exists(diagnosticPath),
                "The reported bootstrap failure path does not exist."
            );
            TestAssert.Equal(
                Path.GetFullPath(gameDirectory),
                Path.GetDirectoryName(diagnosticPath),
                "bootstrap recovery directory"
            );
            TestAssert.True(
                Path.GetFileName(diagnosticPath).StartsWith(
                    "DSPPluginManager-bootstrap-failure-",
                    StringComparison.Ordinal
                ),
                "The bootstrap failure filename is not recognizable."
            );

            byte[] bytes = File.ReadAllBytes(diagnosticPath);
            TestAssert.True(
                bytes.Length >= 3 &&
                !(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF),
                "The bootstrap record must be UTF-8 without a byte-order mark."
            );
            string record = Encoding.UTF8.GetString(bytes);
            Contains(record, "Phase: resolver\\r\\nspoofed-field: value");
            Contains(record, "Target assembly: " + context.TargetAssemblyPath);
            Contains(record, "Executable: " + executable);
            Contains(
                record,
                "Managed directory: " +
                context.ManagedDirectory.Replace("\n", "\\n")
            );
            Contains(record, "Host root: <unavailable>");
            Contains(
                record,
                "Dependency directory: " + context.DependencyDirectory
            );
            Contains(record, "----- BEGIN EXCEPTION -----");
            Contains(record, "outer failure");
            Contains(record, "inner line one\r\ninner line two");
            Contains(record, "----- END EXCEPTION -----");

            int phaseLineCount = CountLinesStartingWith(record, "Phase: ");
            TestAssert.Equal(1, phaseLineCount, "phase field line count");
        }

        private static void VerifyUnavailableDirectory(string sandbox)
        {
            string missingDirectory = Path.Combine(sandbox, "missing-game");
            BootstrapFailureContext context = new BootstrapFailureContext(
                "entrypoint",
                "Bootstrap.dll",
                Path.Combine(missingDirectory, "DSPGAME.exe"),
                null,
                null,
                null
            );

            string diagnosticPath;
            bool written = BootstrapFailureRecord.TryWrite(
                context,
                new InvalidOperationException("startup failed"),
                out diagnosticPath
            );

            TestAssert.True(
                !written,
                "An unavailable recovery directory must report write failure."
            );
            TestAssert.Equal(
                Path.GetFullPath(missingDirectory),
                Path.GetDirectoryName(diagnosticPath),
                "attempted unavailable recovery directory"
            );
            TestAssert.True(
                !Directory.Exists(missingDirectory),
                "Emergency diagnostics must not create an unavailable directory."
            );
        }

        private static void VerifyDeniedWritePreservesFailure(string sandbox)
        {
            string executable = Path.Combine(sandbox, "DSPGAME.exe");
            File.WriteAllText(executable, string.Empty);
            BootstrapFailureContext context = new BootstrapFailureContext(
                "entrypoint",
                "Bootstrap.dll",
                executable,
                null,
                null,
                null
            );
            Exception original = new InvalidOperationException(
                "original startup failure"
            );
            Exception observed = null;

            try
            {
                throw original;
            }
            catch (Exception startupFailure)
            {
                string diagnosticPath;
                bool written = BootstrapFailureRecord.TryWrite(
                    context,
                    startupFailure,
                    DenyWrite,
                    out diagnosticPath
                );
                TestAssert.True(
                    !written,
                    "A denied diagnostic write must report failure."
                );
                observed = startupFailure;
            }

            TestAssert.True(
                object.ReferenceEquals(original, observed),
                "Diagnostic failure replaced the original startup exception."
            );
        }

        private static void VerifyRelativeExecutableIsRejected()
        {
            BootstrapFailureContext context = new BootstrapFailureContext(
                "entrypoint",
                "Bootstrap.dll",
                "relative-game.exe",
                null,
                null,
                null
            );

            string diagnosticPath;
            bool written = BootstrapFailureRecord.TryWrite(
                context,
                new InvalidOperationException("startup failed"),
                out diagnosticPath
            );
            TestAssert.True(
                !written,
                "A relative executable path must not select a recovery location."
            );
            TestAssert.Equal(
                null,
                diagnosticPath,
                "relative executable diagnostic path"
            );
        }

        private static Exception CreateMultilineFailure()
        {
            Exception inner = new InvalidOperationException(
                "inner line one\r\ninner line two"
            );
            return new ApplicationException("outer failure", inner);
        }

        private static void DenyWrite(string path, string content)
        {
            throw new UnauthorizedAccessException("write denied by test");
        }

        private static void Contains(string value, string expected)
        {
            TestAssert.True(
                value.IndexOf(expected, StringComparison.Ordinal) >= 0,
                "Bootstrap record did not contain '" + expected + "'."
            );
        }

        private static int CountLinesStartingWith(string value, string prefix)
        {
            int count = 0;
            using (StringReader reader = new StringReader(value))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        count++;
                    }
                }
            }

            return count;
        }
    }
}
