namespace DSPPluginManager.Logging
{
    internal interface ILogSink
    {
        void Write(LogRecord record);
    }
}
