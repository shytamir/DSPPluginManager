using System.Collections;
using System.IO;
using System.Threading;
using DSPPluginManager.Contracts;
using UnityEngine;

namespace DSPPluginManager.RM22CleanupSuccess
{
    [Plugin(
        "fixture.rm22.b-cleanup-success",
        "RM-22 Cleanup Success",
        "1.0.0"
    )]
    public sealed class CleanupSuccessPlugin : PluginBehaviour
    {
        private static int cleanupCount;
        private int activateThread;

        public override void Activate()
        {
            activateThread = Thread.CurrentThread.ManagedThreadId;
            StartCoroutine(RequestOrderlyExit());
        }

        public override void Deactivate()
        {
            cleanupCount++;
            Logger.Information("RM-22 success fixture cleanup entered.");
            string[] lines =
            {
                "cleanupCount=" + cleanupCount,
                "activateThread=" + activateThread,
                "cleanupThread=" + Thread.CurrentThread.ManagedThreadId,
                "loggerAvailable=" + (Logger != null),
                "writableRootAvailable=" +
                    (!string.IsNullOrWhiteSpace(WritableRoot) &&
                     Directory.Exists(WritableRoot)),
                "componentAvailable=" +
                    (enabled && gameObject != null),
                "contractAvailable=" +
                    (typeof(PluginBehaviour).Assembly != null),
                "unityAvailable=" +
                    (typeof(GameObject).Assembly != null)
            };
            File.WriteAllLines(
                Path.Combine(WritableRoot, "RM22-SUCCESS-EVIDENCE.log"),
                lines
            );
        }

        private static IEnumerator RequestOrderlyExit()
        {
            for (int frame = 0; frame < 12; frame++)
            {
                yield return null;
            }
            Application.Quit();
        }
    }
}
