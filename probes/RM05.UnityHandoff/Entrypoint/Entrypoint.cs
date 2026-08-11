using System;
using System.IO;
using System.Reflection;
using DSPPluginManager.Bootstrap;
using DSPPluginManager.Dependencies;

namespace DSPPluginManager.RM05Probe
{
    public static class Entrypoint
    {
        private static ReservedDependencyResolver resolver;

        public static void Main()
        {
            string probeAssembly = Assembly.GetExecutingAssembly().Location;
            string probeRoot = Path.GetDirectoryName(probeAssembly);
            string mode = ReadMode(probeRoot);
            ProbeRecorder.Initialize(probeRoot, mode);

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
                    "RM-05 " + mode + " pre-Unity entrypoint",
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

                if (string.Equals(
                        mode,
                        "early-failure",
                        StringComparison.Ordinal
                    ))
                {
                    throw new InvalidOperationException(
                        "RM-05 deliberate multiline early failure.\r\n" +
                        "The second line proves complete exception recording."
                    );
                }

                resolver = new ReservedDependencyResolver(
                    dependencyDirectory,
                    pluginDirectory
                );
                resolver.Install();
                ProbeRecorder.Record("reserved-resolver-installed");

                string callbackPath = Path.Combine(
                    probeRoot,
                    "DSPPluginManager.RM05Callback.dll"
                );
                Assembly callbackAssembly = Assembly.LoadFrom(callbackPath);
                ProbeRecorder.Record(
                    "callback-assembly-preloaded",
                    callbackAssembly.FullName,
                    callbackAssembly.Location
                );

                if (string.Equals(mode, "runtime-attribute", StringComparison.Ordinal))
                {
                    ProbeRecorder.Record("runtime-attribute-awaiting-unity");
                    return;
                }
                if (string.Equals(mode, "cecil", StringComparison.Ordinal))
                {
                    InstallCecilHandoff(
                        probeRoot,
                        managedDirectory,
                        callbackPath
                    );
                    return;
                }

                throw new InvalidOperationException(
                    "Unsupported RM-05 probe mode '" + mode + "'."
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
                "DSPPluginManager.RM05CecilHandoff.dll"
            );
            Assembly installerAssembly = Assembly.LoadFrom(installerPath);
            Type installerType = installerAssembly.GetType(
                "DSPPluginManager.RM05CecilHandoff.CecilHandoffInstaller",
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

            string result;
            try
            {
                result = (string)install.Invoke(
                    null,
                    new object[] { managedDirectory, callbackPath }
                );
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(
                    "The Cecil handoff installer failed.",
                    exception.InnerException ?? exception
                );
            }
            ProbeRecorder.Record("cecil-handoff-installed", result);
        }

        private static string ReadMode(string probeRoot)
        {
            string modePath = Path.Combine(probeRoot, "mode.txt");
            if (!File.Exists(modePath))
            {
                throw new FileNotFoundException(
                    "The RM-05 probe mode file is missing.",
                    modePath
                );
            }
            return File.ReadAllText(modePath).Trim();
        }

        private static string ValueOrUnavailable(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "<unavailable>" : value;
        }
    }
}
