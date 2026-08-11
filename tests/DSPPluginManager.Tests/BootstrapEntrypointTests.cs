using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using DSPPluginManager.Bootstrap;

namespace DSPPluginManager.Tests
{
    internal static class BootstrapEntrypointTests
    {
        internal static void Run()
        {
            OneShotGateAllowsOneCaller();
            EnvironmentAcceptsRelativeDoorstopTarget();
            EnvironmentRejectsAnotherProcess();
            EnvironmentRejectsMismatchedTarget();
        }

        private static void OneShotGateAllowsOneCaller()
        {
            OneShotGate gate = new OneShotGate();
            int winners = 0;
            List<Thread> threads = new List<Thread>();
            for (int index = 0; index < 24; index++)
            {
                Thread thread = new Thread(() =>
                {
                    if (gate.TryEnter())
                    {
                        Interlocked.Increment(ref winners);
                    }
                });
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            TestAssert.Equal(1, winners, "one-shot gate winner count");
            TestAssert.True(
                !gate.TryEnter(),
                "A completed one-shot gate must reject later callers."
            );
        }

        private static void EnvironmentAcceptsRelativeDoorstopTarget()
        {
            using (BootstrapTree tree = new BootstrapTree("DSPGAME.exe"))
            {
                BootstrapEnvironment environment = BootstrapEnvironment.Create(
                    tree.ExecutablePath,
                    tree.ManagedDirectory,
                    "DSPPluginManager\\DSPPluginManager.dll",
                    tree.ManagerAssemblyPath
                );
                TestAssert.Equal(
                    tree.ManagerRoot,
                    environment.Paths.HostRoot,
                    "bootstrap host root"
                );
                TestAssert.True(
                    Directory.Exists(environment.Paths.DependencyDirectory),
                    "Bootstrap must materialize the reserved dependency directory."
                );
            }
        }

        private static void EnvironmentRejectsAnotherProcess()
        {
            using (BootstrapTree tree = new BootstrapTree("OtherGame.exe"))
            {
                TestAssert.Throws<InvalidOperationException>(
                    () => BootstrapEnvironment.Create(
                        tree.ExecutablePath,
                        tree.ManagedDirectory,
                        tree.ManagerAssemblyPath,
                        tree.ManagerAssemblyPath
                    ),
                    "DSPGAME.exe"
                );
            }
        }

        private static void EnvironmentRejectsMismatchedTarget()
        {
            using (BootstrapTree tree = new BootstrapTree("DSPGAME.exe"))
            {
                string other = Path.Combine(tree.ManagerRoot, "Other.dll");
                File.WriteAllBytes(other, new byte[] { 0 });
                TestAssert.Throws<InvalidOperationException>(
                    () => BootstrapEnvironment.Create(
                        tree.ExecutablePath,
                        tree.ManagedDirectory,
                        other,
                        tree.ManagerAssemblyPath
                    ),
                    "targeted",
                    "entry assembly"
                );
            }
        }

        private sealed class BootstrapTree : IDisposable
        {
            internal BootstrapTree(string executableName)
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "DSPPluginManager-RM06-" + Guid.NewGuid().ToString("N")
                );
                Directory.CreateDirectory(Root);
                ExecutablePath = Path.Combine(Root, executableName);
                File.WriteAllBytes(ExecutablePath, new byte[] { 0 });
                ManagedDirectory = Path.Combine(Root, "DSPGAME_Data", "Managed");
                Directory.CreateDirectory(ManagedDirectory);
                ManagerRoot = Path.Combine(Root, "DSPPluginManager");
                Directory.CreateDirectory(ManagerRoot);
                ManagerAssemblyPath = Path.Combine(
                    ManagerRoot,
                    "DSPPluginManager.dll"
                );
                File.WriteAllBytes(ManagerAssemblyPath, new byte[] { 0 });
            }

            internal string Root { get; }
            internal string ExecutablePath { get; }
            internal string ManagedDirectory { get; }
            internal string ManagerRoot { get; }
            internal string ManagerAssemblyPath { get; }

            public void Dispose()
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
