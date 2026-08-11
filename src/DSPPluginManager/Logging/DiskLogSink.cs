using System;
using System.IO;
using System.Text;
using System.Threading;
using DSPPluginManager.Bootstrap;

namespace DSPPluginManager.Logging
{
    internal delegate TextWriter DiskLogWriterOpenAction(string path);

    internal sealed class DiskLogSink : ILogSink, IDisposable
    {
        internal const string PrimaryFileName = "DSPPluginManager.log";
        internal const string FallbackFileName =
            "DSPPluginManager-fallback.log";

        private static readonly TimeSpan DefaultFlushInterval =
            TimeSpan.FromSeconds(2);

        private readonly TextWriter writer;
        private readonly BootstrapFailureContext failureContext;
        private readonly object sync = new object();
        private readonly Timer flushTimer;
        private int failureReported;
        private bool disposed;

        private DiskLogSink(
            TextWriter writer,
            string selectedPath,
            BootstrapFailureContext failureContext,
            TimeSpan flushInterval
        )
        {
            this.writer = writer ?? throw new ArgumentNullException("writer");
            SelectedPath = selectedPath ??
                throw new ArgumentNullException("selectedPath");
            this.failureContext = failureContext;
            flushTimer = new Timer(
                FlushTimerCallback,
                null,
                flushInterval,
                flushInterval
            );
        }

        internal string SelectedPath { get; }

        internal static DiskLogSink TryCreate(
            string logDirectory,
            BootstrapFailureContext failureContext,
            out Exception primaryOpenFailure,
            out string emergencyDiagnosticPath
        )
        {
            return TryCreate(
                logDirectory,
                failureContext,
                OpenWriter,
                DefaultFlushInterval,
                out primaryOpenFailure,
                out emergencyDiagnosticPath
            );
        }

        internal static DiskLogSink TryCreate(
            string logDirectory,
            BootstrapFailureContext failureContext,
            DiskLogWriterOpenAction openWriter,
            TimeSpan flushInterval,
            out Exception primaryOpenFailure,
            out string emergencyDiagnosticPath
        )
        {
            primaryOpenFailure = null;
            emergencyDiagnosticPath = null;
            try
            {
                if (string.IsNullOrWhiteSpace(logDirectory))
                {
                    throw new ArgumentException(
                        "The log directory is required.",
                        "logDirectory"
                    );
                }
                if (openWriter == null)
                {
                    throw new ArgumentNullException("openWriter");
                }
                if (flushInterval <= TimeSpan.Zero ||
                    flushInterval.TotalMilliseconds > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException("flushInterval");
                }

                string directory = Path.GetFullPath(logDirectory);
                Directory.CreateDirectory(directory);
                string primaryPath = Path.Combine(
                    directory,
                    PrimaryFileName
                );
                string fallbackPath = Path.Combine(
                    directory,
                    FallbackFileName
                );

                TextWriter selectedWriter;
                string selectedPath;
                try
                {
                    selectedWriter = openWriter(primaryPath);
                    selectedPath = primaryPath;
                }
                catch (Exception exception)
                {
                    primaryOpenFailure = exception;
                    try
                    {
                        selectedWriter = openWriter(fallbackPath);
                        selectedPath = fallbackPath;
                    }
                    catch (Exception fallbackFailure)
                    {
                        ReportOpenFailure(
                            failureContext,
                            primaryPath,
                            exception,
                            fallbackPath,
                            fallbackFailure,
                            out emergencyDiagnosticPath
                        );
                        return null;
                    }
                }

                try
                {
                    return new DiskLogSink(
                        selectedWriter,
                        selectedPath,
                        failureContext,
                        flushInterval
                    );
                }
                catch (Exception)
                {
                    selectedWriter.Dispose();
                    throw;
                }
            }
            catch (Exception exception)
            {
                if (emergencyDiagnosticPath == null)
                {
                    BootstrapFailureRecord.TryWrite(
                        failureContext,
                        new InvalidOperationException(
                            "Normal disk logging could not be initialized.",
                            exception
                        ),
                        out emergencyDiagnosticPath
                    );
                }
                return null;
            }
        }

        public void Write(LogRecord record)
        {
            if (record == null)
            {
                return;
            }

            lock (sync)
            {
                if (disposed)
                {
                    return;
                }
                try
                {
                    writer.Write(DiskLogRecordFormatter.Format(record));
                }
                catch (Exception exception)
                {
                    ReportRuntimeFailure("write", exception);
                }
            }
        }

        internal void FlushForTest()
        {
            FlushSafely();
        }

        public void Dispose()
        {
            flushTimer.Dispose();
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }
                try
                {
                    writer.Flush();
                }
                catch (Exception exception)
                {
                    ReportRuntimeFailure("orderly flush", exception);
                }
                try
                {
                    writer.Dispose();
                }
                catch (Exception exception)
                {
                    ReportRuntimeFailure("orderly disposal", exception);
                }
                disposed = true;
            }
        }

        private void FlushTimerCallback(object state)
        {
            FlushSafely();
        }

        private void FlushSafely()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return;
                }
                try
                {
                    writer.Flush();
                }
                catch (Exception exception)
                {
                    ReportRuntimeFailure("periodic flush", exception);
                }
            }
        }

        private void ReportRuntimeFailure(string operation, Exception failure)
        {
            if (Interlocked.CompareExchange(ref failureReported, 1, 0) != 0)
            {
                return;
            }
            string ignoredPath;
            BootstrapFailureRecord.TryWrite(
                failureContext,
                new IOException(
                    "Normal disk logging failed during " + operation +
                    " for '" + SelectedPath + "'.",
                    failure
                ),
                out ignoredPath
            );
        }

        private static TextWriter OpenWriter(string path)
        {
            FileStream stream = new FileStream(
                path,
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read
            );
            try
            {
                return new StreamWriter(
                    stream,
                    new UTF8Encoding(false),
                    4096
                );
            }
            catch (Exception)
            {
                stream.Dispose();
                throw;
            }
        }

        private static void ReportOpenFailure(
            BootstrapFailureContext context,
            string primaryPath,
            Exception primaryFailure,
            string fallbackPath,
            Exception fallbackFailure,
            out string emergencyPath
        )
        {
            AggregateException failures = new AggregateException(
                "Normal disk logging could not open primary '" +
                primaryPath + "' or fallback '" + fallbackPath + "'.",
                primaryFailure,
                fallbackFailure
            );
            BootstrapFailureRecord.TryWrite(
                context,
                failures,
                out emergencyPath
            );
        }
    }
}
