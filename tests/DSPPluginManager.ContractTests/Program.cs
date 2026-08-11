using System;

namespace DSPPluginManager.ContractTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 5)
                {
                    throw new InvalidOperationException(
                        "Expected contract, consumer, version, dependency, " +
                        "and game-managed inputs."
                    );
                }
                ContractSliceTests.Run(args[0], args[1], args[2]);
                PluginMetadataReaderTests.Run(
                    args[0],
                    args[1],
                    args[3],
                    args[4]
                );
                Console.WriteLine(
                    "RM-09 contract and RM-11 static metadata tests passed."
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
