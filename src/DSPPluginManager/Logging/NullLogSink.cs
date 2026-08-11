namespace DSPPluginManager.Logging
{
    internal sealed class NullLogSink : ILogSink
    {
        internal static readonly NullLogSink Instance = new NullLogSink();

        private NullLogSink()
        {
        }

        public void Write(LogRecord record)
        {
        }
    }
}
