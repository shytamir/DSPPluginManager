using System;

namespace DSPPluginManager.Logging
{
    internal sealed class LogRecord
    {
        internal LogRecord(
            DateTimeOffset timestamp,
            LogSeverity severity,
            LogSourceContext source,
            string message
        )
        {
            Timestamp = timestamp;
            Severity = severity;
            Source = source ?? throw new ArgumentNullException("source");
            Message = message ?? throw new ArgumentNullException("message");
        }

        internal DateTimeOffset Timestamp { get; }

        internal LogSeverity Severity { get; }

        internal LogSourceContext Source { get; }

        internal string Message { get; }
    }
}
