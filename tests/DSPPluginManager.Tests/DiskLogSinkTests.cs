using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using DSPPluginManager.Bootstrap;
using DSPPluginManager.Logging;

namespace DSPPluginManager.Tests
{
    internal static class DiskLogSinkTests
    {
        internal static void Run()
        {
            PrimaryLogIsReplacedAndReadable();
            PrimaryCollisionUsesOneFallback();
            TotalOpenFailureUsesEmergencyRecord();
            WriteAndFlushFailuresRemainNonThrowing();
            PeriodicAndOrderlyFlushAreObservable();
            OverlappingWritesRemainWholeOnDisk();
        }

        private static void PrimaryLogIsReplacedAndReadable()
        {
            using (Fixture fixture = new Fixture("primary"))
            {
                string primaryPath = Path.Combine(
                    fixture.LogDirectory,
                    DiskLogSink.PrimaryFileName
                );
                File.WriteAllText(primaryPath, "stale previous run");

                Exception primaryFailure;
                string emergencyPath;
                using (DiskLogSink sink = DiskLogSink.TryCreate(
                    fixture.LogDirectory,
                    fixture.FailureContext,
                    out primaryFailure,
                    out emergencyPath
                ))
                {
                    TestAssert.True(sink != null, "Primary log did not open.");
                    TestAssert.Equal(
                        primaryPath,
                        sink.SelectedPath,
                        "primary selected path"
                    );
                    TestAssert.Equal(null, primaryFailure, "primary failure");
                    TestAssert.Equal(null, emergencyPath, "emergency path");

                    DateTimeOffset timestamp = new DateTimeOffset(
                        2026,
                        8,
                        11,
                        21,
                        30,
                        0,
                        TimeSpan.Zero
                    );
                    SourceLogger logger = new LogDispatcher(
                        sink,
                        () => timestamp
                    ).CreateLogger(
                        new LogSourceContext(
                            LogSourceKind.Plugin,
                            "guide|check",
                            "Guide \\ Check"
                        )
                    );
                    logger.Error("first line\r\nsecond line");
                    sink.FlushForTest();

                    byte[] bytes = ReadLiveBytes(primaryPath);
                    TestAssert.True(
                        bytes.Length >= 3 &&
                            !(bytes[0] == 0xEF &&
                              bytes[1] == 0xBB &&
                              bytes[2] == 0xBF),
                        "The current-run log must be UTF-8 without a BOM."
                    );
                    string text = new UTF8Encoding(false, true).GetString(bytes);
                    TestAssert.True(
                        !text.Contains("stale previous run"),
                        "A new run must replace the previous current-run file."
                    );
                    TestAssert.True(
                        text.Contains("2026-08-11T21:30:00.0000000+00:00") &&
                            text.Contains(" | Error | Plugin | ") &&
                            text.Contains("id=guide\\|check") &&
                            text.Contains("name=Guide \\\\ Check") &&
                            text.Contains("first line\r\nsecond line") &&
                            text.Contains("----- END MESSAGE -----"),
                        "The live log record lost required fields or message text."
                    );
                }
            }
        }

        private static void PrimaryCollisionUsesOneFallback()
        {
            using (Fixture fixture = new Fixture("fallback"))
            {
                string primaryPath = Path.Combine(
                    fixture.LogDirectory,
                    DiskLogSink.PrimaryFileName
                );
                string fallbackPath = Path.Combine(
                    fixture.LogDirectory,
                    DiskLogSink.FallbackFileName
                );
                DiskLogWriterOpenAction open = path =>
                {
                    if (string.Equals(
                            path,
                            primaryPath,
                            StringComparison.OrdinalIgnoreCase
                        ))
                    {
                        throw new IOException("primary is locked");
                    }
                    return OpenTestWriter(path);
                };

                Exception primaryFailure;
                string emergencyPath;
                using (DiskLogSink sink = DiskLogSink.TryCreate(
                    fixture.LogDirectory,
                    fixture.FailureContext,
                    open,
                    TimeSpan.FromHours(1),
                    out primaryFailure,
                    out emergencyPath
                ))
                {
                    TestAssert.True(sink != null, "Fallback log did not open.");
                    TestAssert.Equal(
                        fallbackPath,
                        sink.SelectedPath,
                        "fallback selected path"
                    );
                    TestAssert.True(
                        primaryFailure != null &&
                            primaryFailure.Message.Contains("locked"),
                        "The primary open failure was not retained."
                    );
                    TestAssert.Equal(null, emergencyPath, "fallback emergency");
                    new LogDispatcher(sink).CreateLogger(
                        fixture.Source
                    ).Warning("fallback active");
                    sink.FlushForTest();
                    TestAssert.True(
                        ReadLiveText(fallbackPath).Contains("fallback active"),
                        "The selected fallback did not receive records."
                    );
                }

                string[] logs = Directory.GetFiles(
                    fixture.LogDirectory,
                    "*.log"
                );
                TestAssert.Equal(1, logs.Length, "bounded fallback file count");
                TestAssert.Equal(
                    fallbackPath,
                    logs[0],
                    "sole fallback filename"
                );
            }
        }

        private static void TotalOpenFailureUsesEmergencyRecord()
        {
            using (Fixture fixture = new Fixture("open-failure"))
            {
                int attempts = 0;
                DiskLogWriterOpenAction open = path =>
                {
                    attempts++;
                    throw new UnauthorizedAccessException(
                        "denied " + Path.GetFileName(path)
                    );
                };
                Exception primaryFailure;
                string emergencyPath;
                DiskLogSink sink = DiskLogSink.TryCreate(
                    fixture.LogDirectory,
                    fixture.FailureContext,
                    open,
                    TimeSpan.FromHours(1),
                    out primaryFailure,
                    out emergencyPath
                );

                TestAssert.Equal(null, sink, "failed disk sink");
                TestAssert.Equal(2, attempts, "bounded open attempts");
                TestAssert.True(
                    primaryFailure != null,
                    "The primary failure was not returned."
                );
                TestAssert.True(
                    emergencyPath != null && File.Exists(emergencyPath),
                    "Total sink failure did not create an RM-03 record."
                );
                string emergency = File.ReadAllText(emergencyPath);
                TestAssert.True(
                    emergency.Contains(DiskLogSink.PrimaryFileName) &&
                        emergency.Contains(DiskLogSink.FallbackFileName) &&
                        emergency.Contains("UnauthorizedAccessException"),
                    "The emergency record lacks both failed destinations."
                );

                SourceLogger logger = new LogDispatcher(
                    NullLogSink.Instance
                ).CreateLogger(fixture.Source);
                logger.Information("logging unavailable");
                logger.Warning(null);
                logger.Error(new InvalidOperationException("still safe"));
            }
        }

        private static void WriteAndFlushFailuresRemainNonThrowing()
        {
            VerifyRuntimeFailure(false, true, "write");
            VerifyRuntimeFailure(true, false, "periodic flush");
        }

        private static void VerifyRuntimeFailure(
            bool throwOnFlush,
            bool throwOnWrite,
            string expectedOperation
        )
        {
            using (Fixture fixture = new Fixture(expectedOperation))
            {
                ThrowingTextWriter writer = new ThrowingTextWriter(
                    throwOnWrite,
                    throwOnFlush
                );
                Exception primaryFailure;
                string emergencyPath;
                using (DiskLogSink sink = DiskLogSink.TryCreate(
                    fixture.LogDirectory,
                    fixture.FailureContext,
                    path => writer,
                    TimeSpan.FromHours(1),
                    out primaryFailure,
                    out emergencyPath
                ))
                {
                    SourceLogger logger = new LogDispatcher(sink).CreateLogger(
                        fixture.Source
                    );
                    logger.Error("runtime failure probe");
                    if (throwOnFlush)
                    {
                        sink.FlushForTest();
                    }
                }

                string[] emergencyFiles = Directory.GetFiles(
                    fixture.Root,
                    "DSPPluginManager-bootstrap-failure-*.txt"
                );
                TestAssert.Equal(
                    1,
                    emergencyFiles.Length,
                    expectedOperation + " emergency count"
                );
                TestAssert.True(
                    File.ReadAllText(emergencyFiles[0]).Contains(
                        expectedOperation
                    ),
                    "The runtime sink failure lacks operation context."
                );
            }
        }

        private static void PeriodicAndOrderlyFlushAreObservable()
        {
            using (Fixture fixture = new Fixture("flush"))
            {
                Exception primaryFailure;
                string emergencyPath;
                DiskLogSink sink = DiskLogSink.TryCreate(
                    fixture.LogDirectory,
                    fixture.FailureContext,
                    OpenTestWriter,
                    TimeSpan.FromMilliseconds(25),
                    out primaryFailure,
                    out emergencyPath
                );
                string path = sink.SelectedPath;
                SourceLogger logger = new LogDispatcher(sink).CreateLogger(
                    fixture.Source
                );
                logger.Information("periodic-visible");

                DateTime deadline = DateTime.UtcNow.AddSeconds(3);
                bool periodicObserved = false;
                while (DateTime.UtcNow < deadline)
                {
                    if (ReadLiveText(path).Contains("periodic-visible"))
                    {
                        periodicObserved = true;
                        break;
                    }
                    Thread.Sleep(10);
                }
                TestAssert.True(
                    periodicObserved,
                    "Periodic flush did not make buffered output readable."
                );

                logger.Information("orderly-tail");
                sink.Dispose();
                TestAssert.True(
                    File.ReadAllText(path).Contains("orderly-tail"),
                    "Orderly disposal did not synchronously flush the tail."
                );
            }
        }

        private static void OverlappingWritesRemainWholeOnDisk()
        {
            using (Fixture fixture = new Fixture("overlap"))
            {
                Exception primaryFailure;
                string emergencyPath;
                using (DiskLogSink sink = DiskLogSink.TryCreate(
                    fixture.LogDirectory,
                    fixture.FailureContext,
                    out primaryFailure,
                    out emergencyPath
                ))
                {
                    LogDispatcher dispatcher = new LogDispatcher(sink);
                    SourceLogger logger = dispatcher.CreateLogger(fixture.Source);
                    List<Thread> threads = new List<Thread>();
                    const int threadCount = 6;
                    const int recordsPerThread = 30;
                    for (int threadIndex = 0;
                        threadIndex < threadCount;
                        threadIndex++)
                    {
                        int capturedThread = threadIndex;
                        Thread thread = new Thread(() =>
                        {
                            for (int recordIndex = 0;
                                recordIndex < recordsPerThread;
                                recordIndex++)
                            {
                                logger.Information(
                                    "record-" + capturedThread + "-" +
                                    recordIndex
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
                    sink.FlushForTest();

                    string text = ReadLiveText(sink.SelectedPath);
                    TestAssert.Equal(
                        threadCount * recordsPerThread,
                        CountOccurrences(text, "----- BEGIN MESSAGE -----"),
                        "disk record start count"
                    );
                    TestAssert.Equal(
                        threadCount * recordsPerThread,
                        CountOccurrences(text, "----- END MESSAGE -----"),
                        "disk record end count"
                    );
                    for (int threadIndex = 0;
                        threadIndex < threadCount;
                        threadIndex++)
                    {
                        for (int recordIndex = 0;
                            recordIndex < recordsPerThread;
                            recordIndex++)
                        {
                            TestAssert.True(
                                text.Contains(
                                    "record-" + threadIndex + "-" +
                                    recordIndex
                                ),
                                "An overlapping disk record was lost."
                            );
                        }
                    }
                }
            }
        }

        private static TextWriter OpenTestWriter(string path)
        {
            return new StreamWriter(
                new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.Read
                ),
                new UTF8Encoding(false),
                4096
            );
        }

        private static byte[] ReadLiveBytes(string path)
        {
            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite
            ))
            using (MemoryStream copy = new MemoryStream())
            {
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }

        private static string ReadLiveText(string path)
        {
            return new UTF8Encoding(false, true).GetString(
                ReadLiveBytes(path)
            );
        }

        private static int CountOccurrences(string value, string search)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(
                    search,
                    offset,
                    StringComparison.Ordinal
                )) >= 0)
            {
                count++;
                offset += search.Length;
            }
            return count;
        }

        private sealed class ThrowingTextWriter : TextWriter
        {
            private readonly bool throwOnWrite;
            private readonly bool throwOnFlush;

            internal ThrowingTextWriter(bool throwOnWrite, bool throwOnFlush)
            {
                this.throwOnWrite = throwOnWrite;
                this.throwOnFlush = throwOnFlush;
            }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public override void Write(string value)
            {
                if (throwOnWrite)
                {
                    throw new IOException("write rejected");
                }
            }

            public override void Flush()
            {
                if (throwOnFlush)
                {
                    throw new IOException("flush rejected");
                }
            }
        }

        private sealed class Fixture : IDisposable
        {
            internal Fixture(string name)
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "DSPPluginManager-RM08-" + name + "-" +
                    Guid.NewGuid().ToString("N")
                );
                Directory.CreateDirectory(Root);
                LogDirectory = Path.Combine(Root, "logs");
                Directory.CreateDirectory(LogDirectory);
                string executable = Path.Combine(Root, "DSPGAME.exe");
                File.WriteAllBytes(executable, new byte[] { 0 });
                FailureContext = new BootstrapFailureContext(
                    "RM-08 disk logging",
                    Path.Combine(Root, "DSPPluginManager.dll"),
                    executable,
                    Path.Combine(Root, "Managed"),
                    Root,
                    Path.Combine(Root, "dependencies")
                );
                Source = new LogSourceContext(
                    LogSourceKind.Plugin,
                    "test.plugin",
                    "Test Plugin"
                );
            }

            internal string Root { get; }
            internal string LogDirectory { get; }
            internal BootstrapFailureContext FailureContext { get; }
            internal LogSourceContext Source { get; }

            public void Dispose()
            {
                Directory.Delete(Root, true);
            }
        }
    }
}
