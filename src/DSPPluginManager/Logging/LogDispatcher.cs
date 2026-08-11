using System;

namespace DSPPluginManager.Logging
{
    internal delegate DateTimeOffset LogTimestampProvider();

    internal sealed class LogDispatcher
    {
        private readonly ILogSink sink;
        private readonly LogTimestampProvider timestampProvider;
        private readonly object sync = new object();

        internal LogDispatcher(ILogSink sink)
            : this(sink, GetUtcNow)
        {
        }

        internal LogDispatcher(
            ILogSink sink,
            LogTimestampProvider timestampProvider
        )
        {
            this.sink = sink ?? throw new ArgumentNullException("sink");
            this.timestampProvider = timestampProvider ??
                throw new ArgumentNullException("timestampProvider");
        }

        internal SourceLogger CreateLogger(LogSourceContext source)
        {
            return new SourceLogger(this, source);
        }

        internal void Write(
            LogSourceContext source,
            LogSeverity severity,
            object payload
        )
        {
            try
            {
                string message = LogPayloadFormatter.Format(payload);
                DateTimeOffset timestamp = GetTimestamp();
                LogRecord record = new LogRecord(
                    timestamp,
                    severity,
                    source,
                    message
                );
                lock (sync)
                {
                    sink.Write(record);
                }
            }
            catch (Exception)
            {
                // Logging must never change the caller's lifecycle outcome.
            }
        }

        private DateTimeOffset GetTimestamp()
        {
            try
            {
                return timestampProvider();
            }
            catch (Exception)
            {
                return DateTimeOffset.UtcNow;
            }
        }

        private static DateTimeOffset GetUtcNow()
        {
            return DateTimeOffset.UtcNow;
        }
    }
}
