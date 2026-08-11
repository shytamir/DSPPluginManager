using System;

namespace DSPPluginManager.Logging
{
    internal sealed class SourceLogger
    {
        private readonly LogDispatcher dispatcher;

        internal SourceLogger(
            LogDispatcher dispatcher,
            LogSourceContext source
        )
        {
            this.dispatcher = dispatcher ??
                throw new ArgumentNullException("dispatcher");
            Source = source ?? throw new ArgumentNullException("source");
        }

        internal LogSourceContext Source { get; }

        internal void Information(object payload)
        {
            dispatcher.Write(Source, LogSeverity.Information, payload);
        }

        internal void Warning(object payload)
        {
            dispatcher.Write(Source, LogSeverity.Warning, payload);
        }

        internal void Error(object payload)
        {
            dispatcher.Write(Source, LogSeverity.Error, payload);
        }
    }
}
