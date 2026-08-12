using System;
using System.IO;
using System.Threading;
using DSPPluginManager.Contracts;

namespace DSPPluginManager.RM22CleanupFailure
{
    [Plugin(
        "fixture.rm22.a-cleanup-failure",
        "RM-22 Cleanup Failure",
        "1.0.0"
    )]
    public sealed class CleanupFailurePlugin : PluginBehaviour
    {
        private static int cleanupCount;
        private PluginConfigurationEntry<bool> lifecycleSetting;

        public override void Activate()
        {
            lifecycleSetting = Config.Bind(
                "Lifecycle",
                "Enabled",
                true,
                "RM-31 unavailable-source lifecycle fixture."
            );
        }

        public override void Deactivate()
        {
            cleanupCount++;
            Logger.Information("RM-22 failure fixture cleanup entered.");
            WriteEvidence();
            throw new InvalidOperationException(
                "RM-22 intentional cleanup failure."
            );
        }

        private void WriteEvidence()
        {
            string[] lines =
            {
                "cleanupCount=" + cleanupCount,
                "cleanupThread=" + Thread.CurrentThread.ManagedThreadId,
                "loggerAvailable=" + (Logger != null),
                "writableRootAvailable=" +
                    (!string.IsNullOrWhiteSpace(WritableRoot) &&
                     Directory.Exists(WritableRoot)),
                "configurationAvailable=" + (Config != null),
                "configurationValue=" + lifecycleSetting.Value,
                "componentAvailable=" +
                    (enabled && gameObject != null),
                "contractAvailable=" +
                    (typeof(PluginBehaviour).Assembly != null),
                "unityAvailable=" +
                    (typeof(UnityEngine.GameObject).Assembly != null)
            };
            File.WriteAllLines(
                Path.Combine(WritableRoot, "RM22-FAILURE-EVIDENCE.log"),
                lines
            );
        }
    }
}
