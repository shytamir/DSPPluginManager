using System;

namespace DSPPluginManager.ContractTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3)
                {
                    throw new InvalidOperationException(
                        "Expected contract, consumer fixture, and version."
                    );
                }
                ContractSliceTests.Run(args[0], args[1], args[2]);
                Console.WriteLine(
                    "RM-09 contract and static consumer metadata tests passed."
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
