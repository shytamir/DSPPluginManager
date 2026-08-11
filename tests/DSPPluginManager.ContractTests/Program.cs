using System;
using System.IO;

namespace DSPPluginManager.ContractTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length == 8 &&
                    args[0] == "--write-milestone1-fixture")
                {
                    string[] plan = Milestone1Fixture.Create(
                        args[1],
                        args[2],
                        args[3],
                        args[4],
                        args[5],
                        args[6]
                    );
                    File.WriteAllLines(args[7], plan);
                    Console.WriteLine(args[7]);
                    return 0;
                }
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
                CandidateReconcilerTests.Run(
                    args[0],
                    args[1],
                    args[3],
                    args[4]
                );
                Milestone1DiscoveryTests.Run(
                    args[0],
                    args[1],
                    args[3],
                    args[4]
                );
                Console.WriteLine(
                    "RM-09 contract, RM-11 metadata, and RM-12 " +
                    "reconciliation tests passed."
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
