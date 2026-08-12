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
                UnityHostContainerTests.Run(
                    args[0],
                    args[4],
                    args[5],
                    args[6]
                );
                BootstrapFailureRecordTests.Run();
                BootstrapEntrypointTests.Run();
                ReservedDependencyResolverTests.Run(args[3]);
                PluginActivationCoordinatorTests.Run(
                    args[3],
                    args[4],
                    args[5],
                    args[6],
                    args[7],
                    args[8],
                    args[9]
                );
                RuntimeDeliveryFixtureTests.Run(
                    args[3],
                    args[5],
                    args[6],
                    args[10]
                );
                PluginShutdownCoordinatorTests.Run(
                    args[3],
                    args[4],
                    args[5],
                    args[6],
                    args[11],
                    args[12]
                );
                LoggingCoreTests.Run();
                DiskLogSinkTests.Run();
                CandidateFileEnumeratorTests.Run();
                PluginLifecycleRecordTests.Run();
                Console.WriteLine(
                    "Compiled foundation, host/plugin paths, Unity container, " +
                    "bootstrap entry, diagnostic, " +
                    "reserved dependency, activation failure isolation, " +
                    "runtime-delivery metadata, orderly shutdown, logging, " +
                    "candidate enumeration, " +
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
