using System;
using System.IO;
using System.Reflection;
using System.Threading;
using DSPPluginManager.Dependencies;
using DSPPluginManager.Discovery;
using DSPPluginManager.Logging;

namespace DSPPluginManager.Bootstrap
{
    public static class DoorstopEntrypoint
    {
        private static readonly OneShotGate EntryGate = new OneShotGate();
        private static readonly OneShotGate HandoffGate = new OneShotGate();
        private static ReservedDependencyResolver resolver;
        private static BootstrapEnvironment environment;
        private static DiskLogSink diskLogSink;
        private static SourceLogger hostLogger;

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

                InitializeLogging(failureContext);
                hostLogger.Information(
                    "Managed bootstrap environment initialized."
                );

                resolver = new ReservedDependencyResolver(
                    environment.Paths.DependencyDirectory,
                    environment.Paths.PluginDirectory
                );
                resolver.Install();
                hostLogger.Information("Reserved dependency resolver installed.");
                RunPreActivationDiscovery(environment);
                InstallUnityHandoff(environment);
                hostLogger.Information("Unity main-thread handoff installed.");
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
            string containerOutcome = EnsureUnityHostCreated(environment);
            hostLogger.Information(
                "Unity main-thread handoff completed. " + containerOutcome
            );
        }

        private static string EnsureUnityHostCreated(
            BootstrapEnvironment bootstrapEnvironment
        )
        {
            string unityHostPath = Path.Combine(
                bootstrapEnvironment.Paths.HostRoot,
                "DSPPluginManager.UnityHost.dll"
            );
            Assembly unityHostAssembly = Assembly.LoadFrom(unityHostPath);
            Type entrypoint = unityHostAssembly.GetType(
                "DSPPluginManager.UnityHost.UnityHostEntrypoint",
                true,
                false
            );
            MethodInfo ensureCreated = entrypoint.GetMethod(
                "EnsureCreated",
                BindingFlags.Public | BindingFlags.Static
            );
            if (ensureCreated == null)
            {
                throw new MissingMethodException(
                    entrypoint.FullName,
                    "EnsureCreated"
                );
            }

            try
            {
                return (string)ensureCreated.Invoke(
                    null,
                    new object[] { Thread.CurrentThread.ManagedThreadId }
                );
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(
                    "The persistent Unity host root could not be created.",
                    exception.InnerException ?? exception
                );
            }
        }

        private static void RunPreActivationDiscovery(
            BootstrapEnvironment bootstrapEnvironment
        )
        {
            hostLogger.Information("Pre-activation discovery started.");
            CandidateDiscoveryPlan plan = CandidateDiscoveryPlanner.Create(
                bootstrapEnvironment.Paths.PluginDirectory,
                new PluginInspectionReferences(
                    Path.Combine(
                        bootstrapEnvironment.Paths.HostRoot,
                        "DSPPluginManager.Contracts.dll"
                    ),
                    bootstrapEnvironment.Paths.DependencyDirectory,
                    bootstrapEnvironment.Paths.ManagedDirectory
                )
            );
            foreach (CandidateEnumerationDiagnostic diagnostic in
                plan.EnumerationDiagnostics)
            {
                hostLogger.Warning(
                    "Discovery enumeration diagnostic: code=" +
                    diagnostic.Code + " path='" + diagnostic.Path +
                    "' detail=" + diagnostic.Detail
                );
            }
            foreach (string reportLine in plan.ReportLines)
            {
                hostLogger.Information("DiscoveryPlan|" + reportLine);
            }
            if (plan.RuntimeLoadedCandidateCount != 0)
            {
                throw new InvalidOperationException(
                    "Pre-activation discovery runtime-loaded " +
                    plan.RuntimeLoadedCandidateCount + " candidate assemblies."
                );
            }
            hostLogger.Information(
                "Pre-activation discovery completed: candidates=" +
                plan.EnumeratedCandidateCount + " entries=" +
                plan.Reconciliation.Entries.Count +
                " runtimeLoadedCandidates=" +
                plan.RuntimeLoadedCandidateCount + "."
            );
        }

        private static void InitializeLogging(
            BootstrapFailureContext failureContext
        )
        {
            Exception primaryOpenFailure;
            string emergencyDiagnosticPath;
            diskLogSink = DiskLogSink.TryCreate(
                environment.Paths.LogDirectory,
                failureContext,
                out primaryOpenFailure,
                out emergencyDiagnosticPath
            );
            ILogSink sink = diskLogSink == null
                ? (ILogSink)NullLogSink.Instance
                : diskLogSink;
            LogDispatcher dispatcher = new LogDispatcher(sink);
            hostLogger = dispatcher.CreateLogger(
                new LogSourceContext(
                    LogSourceKind.Host,
                    "dsp-plugin-manager",
                    "DSP Plugin Manager"
                )
            );

            if (diskLogSink == null)
            {
                return;
            }
            if (primaryOpenFailure != null)
            {
                hostLogger.Warning(
                    "Primary current-run log was unavailable; using '" +
                    diskLogSink.SelectedPath + "'. " +
                    primaryOpenFailure
                );
            }
            hostLogger.Information(
                "Current-run log opened at '" +
                diskLogSink.SelectedPath + "'."
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
