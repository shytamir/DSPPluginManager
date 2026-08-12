using System;
using System.IO;
using System.Text;
using DSPPluginManager.Contracts;

namespace DSPPluginManager.RM09Consumer
{
    [Plugin(
        "com.shytamir.dspmirrorblueprint",
        "DSP Mirror Blueprint",
        "1.2.3"
    )]
    public sealed class MirrorShapedPlugin : PluginBehaviour
    {
        private PluginLogger logger;
        private string writableRoot;

        public void CaptureLogger()
        {
            logger = Logger;
            MirrorLoggingHelper.Report(logger);
        }

        public void CaptureWritableRoot()
        {
            writableRoot = WritableRoot;
            MirrorOutputHelper.WriteSnapshot(
                writableRoot,
                "Mirror fixture snapshot."
            );
        }

        public override void Activate()
        {
            MirrorActivationEvidence.ActivationCount++;
            MirrorActivationEvidence.LoggerAvailable = Logger != null;
            MirrorActivationEvidence.WritableRoot = WritableRoot;
            MirrorActivationEvidence.InitiallyEnabled = enabled;
            MirrorActivationEvidence.AttachedGameObject = gameObject != null;
            Logger.Information("RM-19 activation acknowledged.");
            Action observer = MirrorActivationEvidence.Observer;
            if (observer != null)
            {
                observer();
            }
        }

        public override void Deactivate()
        {
            MirrorActivationEvidence.DeactivationCount++;
        }
    }

    internal static class MirrorLoggingHelper
    {
        internal static void Report(PluginLogger logger)
        {
            logger.Information("Mirror fixture started.");
            logger.Warning("Mirror fixture warning.");
            logger.Error("Mirror fixture error.");
        }
    }

    internal static class MirrorOutputHelper
    {
        internal static string WriteSnapshot(string root, string contents)
        {
            string directory = Path.Combine(root, "Diagnostics");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "snapshot.txt");
            File.WriteAllText(
                path,
                contents,
                new UTF8Encoding(false, true)
            );
            return path;
        }
    }

    internal static class MirrorActivationEvidence
    {
        internal static int ActivationCount { get; set; }

        internal static int DeactivationCount { get; set; }

        internal static bool LoggerAvailable { get; set; }

        internal static string WritableRoot { get; set; }

        internal static bool InitiallyEnabled { get; set; }

        internal static bool AttachedGameObject { get; set; }

        internal static Action Observer { get; set; }
    }
}
