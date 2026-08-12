using System.IO;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.Tests
{
    internal static class RuntimeDeliveryFixtureTests
    {
        internal static void Run(
            string dependencyDirectory,
            string facadePath,
            string contractPath,
            string fixturePath
        )
        {
            PluginInspectionResult inspection =
                new PluginMetadataReader(
                    new PluginInspectionReferences(
                        Path.GetFullPath(contractPath),
                        Path.GetFullPath(dependencyDirectory),
                        Path.GetDirectoryName(Path.GetFullPath(facadePath))
                    )
                ).Inspect(Path.GetFullPath(fixturePath));
            TestAssert.True(
                inspection.IsRecognized,
                "The RM-21 runtime-delivery fixture was not recognized."
            );
            TestAssert.Equal(
                "fixture.rm21.runtime-delivery",
                inspection.Candidate.Identifier,
                "RM-21 fixture identifier"
            );
            TestAssert.Equal(
                "DSPPluginManager.RM21RuntimeDelivery.RuntimeDeliveryPlugin",
                inspection.Candidate.TypeName,
                "RM-21 fixture type"
            );
        }
    }
}
