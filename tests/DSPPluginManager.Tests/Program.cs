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
                Console.WriteLine("Compiled foundation and path tests passed.");
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
