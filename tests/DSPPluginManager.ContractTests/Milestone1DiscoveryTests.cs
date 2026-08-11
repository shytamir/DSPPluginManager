using System;
using System.IO;

namespace DSPPluginManager.ContractTests
{
    internal static class Milestone1DiscoveryTests
    {
        internal static void Run(
            string contractPath,
            string validFixturePath,
            string dependencyDirectory,
            string gameManagedDirectory
        )
        {
            string sandbox = Path.Combine(
                Path.GetTempPath(),
                "DSPPluginManager.Milestone1Tests",
                Guid.NewGuid().ToString("N")
            );
            string fixtureRoot = Path.Combine(sandbox, "plugins");
            string sentinel = Path.Combine(sandbox, "executed.txt");
            Directory.CreateDirectory(sandbox);
            try
            {
                string[] actual = Milestone1Fixture.Create(
                    contractPath,
                    validFixturePath,
                    dependencyDirectory,
                    gameManagedDirectory,
                    fixtureRoot,
                    sentinel
                );
                string[] expected =
                {
                    "state=Ambiguous|identifier=COM.EXAMPLE.AMBIGUOUS|version=2.0.0|path=ambiguous/a.dll|diagnostic=AmbiguousIdentity",
                    "state=Ambiguous|identifier=COM.EXAMPLE.AMBIGUOUS|version=2.0.0|path=ambiguous/b.dll|diagnostic=AmbiguousIdentity",
                    "state=Selected|identifier=COM.SHYTAMIR.DSPMIRRORBLUEPRINT|version=1.2.3|path=selected/a.dll|diagnostic=-",
                    "state=Redundant|identifier=COM.SHYTAMIR.DSPMIRRORBLUEPRINT|version=1.2.3|path=selected/z.dll|diagnostic=RedundantCopy",
                    "state=Superseded|identifier=COM.SHYTAMIR.DSPMIRRORBLUEPRINT|version=1.0.0|path=old/old.dll|diagnostic=SupersededVersion",
                    "state=Rejected|identifier=-|version=-|path=invalid/invalid.dll|diagnostic=InspectionRejected/InvalidMetadata",
                    "state=Rejected|identifier=-|version=-|path=rejected/native.dll|diagnostic=InspectionRejected/NonManagedFile"
                };
                TestAssert.Equal(
                    string.Join("\n", expected),
                    string.Join("\n", actual),
                    "milestone fixture plan"
                );
            }
            finally
            {
                if (Directory.Exists(sandbox))
                {
                    Directory.Delete(sandbox, true);
                }
            }
        }
    }
}
