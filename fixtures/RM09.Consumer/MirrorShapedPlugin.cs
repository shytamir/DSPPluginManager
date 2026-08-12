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

        public void CaptureLogger()
        {
            logger = Logger;
            MirrorLoggingHelper.Report(logger);
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
}
