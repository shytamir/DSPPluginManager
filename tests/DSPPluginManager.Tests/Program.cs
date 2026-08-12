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
                PluginWritableRootPathTests.Run();
                UnityHostContainerTests.Run(args[0], args[4], args[5]);
                BootstrapFailureRecordTests.Run();
                BootstrapEntrypointTests.Run();
                ReservedDependencyResolverTests.Run(args[3]);
                LoggingCoreTests.Run();
                DiskLogSinkTests.Run();
                CandidateFileEnumeratorTests.Run();
                PluginLifecycleRecordTests.Run();
                Console.WriteLine(
                    "Compiled foundation, host/plugin paths, Unity container, " +
                    "bootstrap entry, diagnostic, " +
                    "reserved dependency, logging, candidate enumeration, " +
                    "and lifecycle state tests passed."
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
