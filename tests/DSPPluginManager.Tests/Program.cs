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
                ReservedDependencyResolverTests.Run(args[3]);
                Console.WriteLine(
                    "Compiled foundation, path, bootstrap diagnostic, and " +
                    "reserved dependency tests passed."
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
