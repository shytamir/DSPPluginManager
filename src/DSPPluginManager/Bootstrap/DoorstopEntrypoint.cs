using System;
using System.IO;
using System.Reflection;
using System.Threading;
using DSPPluginManager.Dependencies;

namespace DSPPluginManager.Bootstrap
{
    public static class DoorstopEntrypoint
    {
        private static readonly OneShotGate EntryGate = new OneShotGate();
        private static readonly OneShotGate HandoffGate = new OneShotGate();
        private static ReservedDependencyResolver resolver;
        private static BootstrapEnvironment environment;

        public static void Main()
        {
            if (!EntryGate.TryEnter())
            {
                return;
            }

            string executablePath = Environment.GetEnvironmentVariable(
                "DOORSTOP_PROCESS_PATH"
            );
            string managedDirectory = Environment.GetEnvironmentVariable(
                "DOORSTOP_MANAGED_FOLDER_DIR"
            );
            string targetAssemblyPath = Environment.GetEnvironmentVariable(
                "DOORSTOP_INVOKE_DLL_PATH"
            );
            string executingAssemblyPath =
                Assembly.GetExecutingAssembly().Location;
            string hostRoot = string.IsNullOrWhiteSpace(executingAssemblyPath)
                ? null
                : Path.GetDirectoryName(executingAssemblyPath);
            string dependencyDirectory = string.IsNullOrWhiteSpace(hostRoot)
                ? null
                : Path.Combine(hostRoot, "dependencies");
            BootstrapFailureContext failureContext =
                new BootstrapFailureContext(
                    "UnityDoorstop managed entry",
                    targetAssemblyPath,
                    executablePath,
                    managedDirectory,
                    hostRoot,
                    dependencyDirectory
                );

            try
            {
                environment = BootstrapEnvironment.Create(
                    executablePath,
                    managedDirectory,
                    targetAssemblyPath,
                    executingAssemblyPath
                );
                BootstrapCheckpoint.WritePreload(
                    environment.Paths.HostRoot,
                    Thread.CurrentThread.ManagedThreadId
                );

                resolver = new ReservedDependencyResolver(
                    environment.Paths.DependencyDirectory,
                    environment.Paths.PluginDirectory
                );
                resolver.Install();
                InstallUnityHandoff(environment);
            }
            catch (Exception exception)
            {
                string ignoredDiagnosticPath;
                BootstrapFailureRecord.TryWrite(
                    failureContext,
                    exception,
                    out ignoredDiagnosticPath
                );
                throw;
            }
        }

        public static void UnityMainThreadHandoff()
        {
            if (!HandoffGate.TryEnter())
            {
                return;
            }
            if (environment == null)
            {
                throw new InvalidOperationException(
                    "Unity handoff arrived before bootstrap initialization."
                );
            }

            BootstrapCheckpoint.WriteHandoff(
                environment.Paths.HostRoot,
                Thread.CurrentThread.ManagedThreadId
            );
        }

        private static void InstallUnityHandoff(
            BootstrapEnvironment bootstrapEnvironment
        )
        {
            string installerPath = Path.Combine(
                bootstrapEnvironment.Paths.HostRoot,
                "DSPPluginManager.UnityHandoff.dll"
            );
            Assembly installerAssembly = Assembly.LoadFrom(installerPath);
            Type installerType = installerAssembly.GetType(
                "DSPPluginManager.UnityHandoff.CecilHandoffInstaller",
                true,
                false
            );
            MethodInfo install = installerType.GetMethod(
                "Install",
                BindingFlags.Public | BindingFlags.Static
            );
            if (install == null)
            {
                throw new MissingMethodException(
                    installerType.FullName,
                    "Install"
                );
            }

            try
            {
                install.Invoke(
                    null,
                    new object[]
                    {
                        bootstrapEnvironment.Paths.ManagedDirectory,
                        bootstrapEnvironment.TargetAssemblyPath
                    }
                );
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(
                    "The Unity main-thread handoff could not be installed.",
                    exception.InnerException ?? exception
                );
            }
        }
    }
}
