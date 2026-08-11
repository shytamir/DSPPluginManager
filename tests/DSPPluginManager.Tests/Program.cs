using System;

namespace DSPPluginManager.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                FoundationTests.Run(args);
                HostEnvironmentPathsTests.Run();
                BootstrapFailureRecordTests.Run();
                BootstrapEntrypointTests.Run();
                ReservedDependencyResolverTests.Run(args[3]);
                LoggingCoreTests.Run();
                DiskLogSinkTests.Run();
                CandidateFileEnumeratorTests.Run();
                Console.WriteLine(
                    "Compiled foundation, path, bootstrap entry, diagnostic, " +
                    "reserved dependency, logging, and candidate enumeration " +
                    "tests passed."
                );
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }
    }
}
