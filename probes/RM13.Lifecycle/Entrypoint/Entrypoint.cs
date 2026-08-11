using System;
using System.IO;
using System.Reflection;
using DSPPluginManager.Bootstrap;
using DSPPluginManager.Dependencies;

namespace DSPPluginManager.RM13Probe
{
    public static class Entrypoint
    {
        private static ReservedDependencyResolver resolver;

        public static void Main()
        {
            string probeAssembly = Assembly.GetExecutingAssembly().Location;
            string probeRoot = Path.GetDirectoryName(probeAssembly);
            ProbeRecorder.Initialize(probeRoot);

            string executablePath = Environment.GetEnvironmentVariable(
                "DOORSTOP_PROCESS_PATH"
            );
            string managedDirectory = Environment.GetEnvironmentVariable(
                "DOORSTOP_MANAGED_FOLDER_DIR"
            );
            string targetAssembly = Environment.GetEnvironmentVariable(
                "DOORSTOP_INVOKE_DLL_PATH"
            );
            string dependencyDirectory = Path.Combine(
                probeRoot,
                "dependencies"
            );
            string pluginDirectory = Path.Combine(probeRoot, "plugins");
            BootstrapFailureContext failureContext =
                new BootstrapFailureContext(
                    "RM-13 lifecycle observability entrypoint",
                    targetAssembly,
                    executablePath,
                    managedDirectory,
                    probeRoot,
                    dependencyDirectory
                );

            try
            {
                ProbeRecorder.Record(
                    "preload-main",
                    "target=" + ValueOrUnavailable(targetAssembly),
                    "executable=" + ValueOrUnavailable(executablePath),
                    "managed=" + ValueOrUnavailable(managedDirectory)
                );
                resolver = new ReservedDependencyResolver(
                    dependencyDirectory,
                    pluginDirectory
                );
                resolver.Install();
                ProbeRecorder.Record("reserved-resolver-installed");

                string callbackPath = Path.Combine(
                    probeRoot,
                    "DSPPluginManager.RM13Callback.dll"
                );
                Assembly callbackAssembly = Assembly.LoadFrom(callbackPath);
                ProbeRecorder.Record(
                    "callback-assembly-preloaded",
                    callbackAssembly.FullName,
                    callbackAssembly.Location
                );
                InstallCecilHandoff(
                    probeRoot,
                    managedDirectory,
                    callbackPath
                );
            }
            catch (Exception exception)
            {
                string diagnosticPath;
                bool written = BootstrapFailureRecord.TryWrite(
                    failureContext,
                    exception,
                    out diagnosticPath
                );
                ProbeRecorder.Record(
                    "preload-failure",
                    "diagnosticWritten=" + written,
                    "diagnosticPath=" + ValueOrUnavailable(diagnosticPath),
                    exception.ToString()
                );
                throw;
            }
        }

        private static void InstallCecilHandoff(
            string probeRoot,
            string managedDirectory,
            string callbackPath
        )
        {
            if (string.IsNullOrWhiteSpace(managedDirectory))
            {
                throw new InvalidOperationException(
                    "Doorstop did not supply the managed directory."
                );
            }

            string installerPath = Path.Combine(
                probeRoot,
                "DSPPluginManager.RM13CecilHandoff.dll"
            );
            Assembly installerAssembly = Assembly.LoadFrom(installerPath);
            Type installerType = installerAssembly.GetType(
                "DSPPluginManager.RM13CecilHandoff.CecilHandoffInstaller",
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
                string result = (string)install.Invoke(
                    null,
                    new object[] { managedDirectory, callbackPath }
                );
                ProbeRecorder.Record("cecil-handoff-installed", result);
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(
                    "The RM-13 Cecil handoff installer failed.",
                    exception.InnerException ?? exception
                );
            }
        }

        private static string ValueOrUnavailable(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<unavailable>" : value;
        }
    }
}
