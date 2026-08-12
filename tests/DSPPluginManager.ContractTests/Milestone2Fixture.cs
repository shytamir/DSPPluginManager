using System;
using System.IO;
using System.Linq;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.ContractTests
{
    internal static class Milestone2Fixture
    {
        internal static string[] Create(
            string contractPath,
            string validFixturePath,
            string dependencyDirectory,
            string gameManagedDirectory,
            string fixtureRoot,
            string executionSentinelPath,
            string[] lifecycleFixturePaths
        )
        {
            Milestone1Fixture.Create(
                contractPath,
                validFixturePath,
                dependencyDirectory,
                gameManagedDirectory,
                fixtureRoot,
                executionSentinelPath
            );

            string lifecycleDirectory = Path.Combine(
                Path.GetFullPath(fixtureRoot),
                "lifecycle"
            );
            Directory.CreateDirectory(lifecycleDirectory);
            foreach (string source in lifecycleFixturePaths)
            {
                File.Copy(
                    Path.GetFullPath(source),
                    Path.Combine(
                        lifecycleDirectory,
                        Path.GetFileName(source)
                    )
                );
            }

            CandidateDiscoveryPlan plan = CandidateDiscoveryPlanner.Create(
                Path.GetFullPath(fixtureRoot),
                new PluginInspectionReferences(
                    Path.GetFullPath(contractPath),
                    Path.GetFullPath(dependencyDirectory),
                    Path.GetFullPath(gameManagedDirectory)
                )
            );
            TestAssert.Equal(12, plan.EnumeratedCandidateCount,
                "milestone 2 fixture candidate count");
            TestAssert.Equal(0, plan.EnumerationDiagnostics.Count,
                "milestone 2 fixture enumeration diagnostics");
            TestAssert.Equal(0, plan.RuntimeLoadedCandidateCount,
                "milestone 2 fixture runtime-loaded count");
            TestAssert.Equal(6, plan.Reconciliation.Entries.Count(entry =>
                entry.State == CandidateReconciliationState.Selected
            ), "milestone 2 selected count");
            TestAssert.Equal(1, plan.Reconciliation.Entries.Count(entry =>
                entry.State == CandidateReconciliationState.Redundant
            ), "milestone 2 redundant count");
            TestAssert.Equal(1, plan.Reconciliation.Entries.Count(entry =>
                entry.State == CandidateReconciliationState.Superseded
            ), "milestone 2 superseded count");
            TestAssert.Equal(2, plan.Reconciliation.Entries.Count(entry =>
                entry.State == CandidateReconciliationState.Ambiguous
            ), "milestone 2 ambiguous count");
            TestAssert.Equal(2, plan.Reconciliation.Entries.Count(entry =>
                entry.State == CandidateReconciliationState.Rejected
            ), "milestone 2 rejected count");
            TestAssert.True(
                !File.Exists(executionSentinelPath),
                "Milestone 2 fixture generation executed candidate code."
            );
            return plan.ReportLines.ToArray();
        }
    }
}
