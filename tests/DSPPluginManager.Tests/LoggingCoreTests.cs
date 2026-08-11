using System;
using System.Collections.Generic;
using System.Threading;
using DSPPluginManager.Logging;

namespace DSPPluginManager.Tests
{
    internal static class LoggingCoreTests
    {
        internal static void Run()
        {
            RecordsContainStableSourceAndSeverity();
            PayloadFormattingIsSafeAndLossless();
            InvalidSourceLabelsAreRejected();
            SinkAndClockFailuresNeverEscape();
            OverlappingCallersProduceWholeRecords();
        }

        private static void RecordsContainStableSourceAndSeverity()
        {
            CollectingSink sink = new CollectingSink();
            DateTimeOffset timestamp = new DateTimeOffset(
                2026,
                8,
                11,
                21,
                0,
                0,
                TimeSpan.Zero
            );
            LogDispatcher dispatcher = new LogDispatcher(
                sink,
                () => timestamp
            );
            LogSourceContext source = new LogSourceContext(
                LogSourceKind.Plugin,
                "mirror.blueprint",
                "DSP Mirror Blueprint"
            );
            SourceLogger logger = dispatcher.CreateLogger(source);

            logger.Information("ready");
            logger.Warning("degraded");
            logger.Error("failed");

            TestAssert.Equal(3, sink.Records.Count, "severity record count");
            LogSeverity[] expected =
            {
                LogSeverity.Information,
                LogSeverity.Warning,
                LogSeverity.Error
            };
            for (int index = 0; index < expected.Length; index++)
            {
                LogRecord record = sink.Records[index];
                TestAssert.Equal(timestamp, record.Timestamp, "timestamp");
                TestAssert.Equal(expected[index], record.Severity, "severity");
                TestAssert.True(
                    object.ReferenceEquals(source, record.Source),
                    "A retained logger must preserve its immutable source context."
                );
                TestAssert.Equal(
                    "mirror.blueprint",
                    record.Source.Identifier,
                    "source identifier"
                );
                TestAssert.Equal(
                    "DSP Mirror Blueprint",
                    record.Source.DisplayName,
                    "source display name"
                );
                TestAssert.Equal(
                    LogSourceKind.Plugin,
                    record.Source.Kind,
                    "source kind"
                );
            }

            SourceLogger hostLogger = dispatcher.CreateLogger(
                new LogSourceContext(
                    LogSourceKind.Host,
                    "dsp-plugin-manager",
                    "DSP Plugin Manager"
                )
            );
            hostLogger.Information("host-ready");
            TestAssert.Equal(
                LogSourceKind.Host,
                sink.Records[3].Source.Kind,
                "host source kind"
            );
        }

        private static void PayloadFormattingIsSafeAndLossless()
        {
            CollectingSink sink = new CollectingSink();
            SourceLogger logger = CreateLogger(sink);

            logger.Information("Grüße 世界 🚀");
            logger.Warning(null);
            InvalidOperationException multiline = new InvalidOperationException(
                "first line\r\nsecond line"
            );
            logger.Error(multiline);
            logger.Error(new ThrowingPayload());
            logger.Error(new NullPayload());

            TestAssert.Equal(
                "Grüße 世界 🚀",
                sink.Records[0].Message,
                "Unicode payload"
            );
            TestAssert.Equal("<null>", sink.Records[1].Message, "null payload");
            TestAssert.True(
                sink.Records[2].Message.Contains("first line\r\nsecond line"),
                "Multiline exception detail must be preserved."
            );
            TestAssert.True(
                sink.Records[3].Message.Contains("formatting failed for") &&
                    sink.Records[3].Message.Contains("ThrowingPayload") &&
                    sink.Records[3].Message.Contains("formatter exploded"),
                "A throwing payload must become a valid diagnostic message."
            );
            TestAssert.Equal(
                "<null>",
                sink.Records[4].Message,
                "null ToString result"
            );
            foreach (LogRecord record in sink.Records)
            {
                TestAssert.Equal(
                    "test.plugin",
                    record.Source.Identifier,
                    "formatted record attribution"
                );
            }
        }

        private static void InvalidSourceLabelsAreRejected()
        {
            TestAssert.Throws<ArgumentException>(
                () => new LogSourceContext(
                    LogSourceKind.Plugin,
                    string.Empty,
                    "Plugin"
                ),
                "identifier"
            );
            TestAssert.Throws<ArgumentException>(
                () => new LogSourceContext(
                    LogSourceKind.Plugin,
                    "plugin",
                    "unsafe\nlabel"
                ),
                "control"
            );
        }

        private static void SinkAndClockFailuresNeverEscape()
        {
            ThrowingSink sink = new ThrowingSink();
            LogDispatcher dispatcher = new LogDispatcher(
                sink,
                () => throw new InvalidOperationException("clock failed")
            );
            SourceLogger logger = dispatcher.CreateLogger(
                new LogSourceContext(
                    LogSourceKind.Host,
                    "host",
                    "Host"
                )
            );

            logger.Information("information");
            logger.Warning(new ThrowingPayload());
            logger.Error(new InvalidOperationException("error"));
            TestAssert.Equal(3, sink.Attempts, "throwing sink attempt count");
        }

        private static void OverlappingCallersProduceWholeRecords()
        {
            ConcurrentProbeSink sink = new ConcurrentProbeSink();
            LogDispatcher dispatcher = new LogDispatcher(sink);
            SourceLogger first = dispatcher.CreateLogger(
                new LogSourceContext(
                    LogSourceKind.Plugin,
                    "first",
                    "First"
                )
            );
            SourceLogger second = dispatcher.CreateLogger(
                new LogSourceContext(
                    LogSourceKind.Plugin,
                    "second",
                    "Second"
                )
            );
            List<Thread> threads = new List<Thread>();
            const int threadCount = 8;
            const int recordsPerThread = 75;
            for (int threadIndex = 0; threadIndex < threadCount; threadIndex++)
            {
                int capturedIndex = threadIndex;
                Thread thread = new Thread(() =>
                {
                    SourceLogger logger = capturedIndex % 2 == 0
                        ? first
                        : second;
                    for (int recordIndex = 0;
                        recordIndex < recordsPerThread;
                        recordIndex++)
                    {
                        logger.Information(
                            capturedIndex + ":" + recordIndex
                        );
                    }
                });
                threads.Add(thread);
                thread.Start();
            }
            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            TestAssert.Equal(
                threadCount * recordsPerThread,
                sink.Records.Count,
                "overlapping record count"
            );
            TestAssert.Equal(
                1,
                sink.MaximumConcurrentWrites,
                "maximum concurrent sink writes"
            );
            HashSet<string> messages = new HashSet<string>(
                StringComparer.Ordinal
            );
            foreach (LogRecord record in sink.Records)
            {
                TestAssert.True(
                    messages.Add(record.Message),
                    "Overlapping output contained a duplicate or torn message."
                );
                TestAssert.True(
                    record.Source.Identifier == "first" ||
                        record.Source.Identifier == "second",
                    "Overlapping output lost source attribution."
                );
            }
        }

        private static SourceLogger CreateLogger(ILogSink sink)
        {
            return CreateLogger(sink, "test.plugin", "Test Plugin");
        }

        private static SourceLogger CreateLogger(
            ILogSink sink,
            string identifier,
            string displayName
        )
        {
            return new LogDispatcher(sink).CreateLogger(
                new LogSourceContext(
                    LogSourceKind.Plugin,
                    identifier,
                    displayName
                )
            );
        }

        private sealed class CollectingSink : ILogSink
        {
            internal List<LogRecord> Records { get; } = new List<LogRecord>();

            public void Write(LogRecord record)
            {
                Records.Add(record);
            }
        }

        private sealed class ThrowingSink : ILogSink
        {
            internal int Attempts { get; private set; }

            public void Write(LogRecord record)
            {
                Attempts++;
                throw new InvalidOperationException("sink failed");
            }
        }

        private sealed class ConcurrentProbeSink : ILogSink
        {
            private int activeWrites;

            internal List<LogRecord> Records { get; } = new List<LogRecord>();

            internal int MaximumConcurrentWrites { get; private set; }

            public void Write(LogRecord record)
            {
                int active = Interlocked.Increment(ref activeWrites);
                if (active > MaximumConcurrentWrites)
                {
                    MaximumConcurrentWrites = active;
                }
                Thread.SpinWait(5000);
                Records.Add(record);
                Interlocked.Decrement(ref activeWrites);
            }
        }

        private sealed class ThrowingPayload
        {
            public override string ToString()
            {
                throw new InvalidOperationException("formatter exploded");
            }
        }

        private sealed class NullPayload
        {
            public override string ToString()
            {
                return null;
            }
        }
    }
}
