using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace DSPPluginManager.RM13Probe
{
    public static class ProbeRecorder
    {
        private static readonly object Sync = new object();
        private static readonly Stopwatch Elapsed = Stopwatch.StartNew();
        private static string evidencePath;
        private static int preloadThreadId;
        private static int callbackCount;

        public static int PreloadThreadId
        {
            get { return preloadThreadId; }
        }

        public static void Initialize(string probeRoot)
        {
            preloadThreadId = Thread.CurrentThread.ManagedThreadId;
            evidencePath = Path.Combine(probeRoot, "probe-evidence.log");
            Record("recorder-initialized");
        }

        public static int RecordCallback(params string[] details)
        {
            int count = Interlocked.Increment(ref callbackCount);
            string[] combined = new string[details.Length + 2];
            combined[0] = "callbackCount=" + count;
            combined[1] = "sameAsPreloadThread=" +
                (Thread.CurrentThread.ManagedThreadId == preloadThreadId);
            Array.Copy(details, 0, combined, 2, details.Length);
            Record("unity-callback", combined);
            return count;
        }

        public static void Record(string eventName, params string[] details)
        {
            if (string.IsNullOrWhiteSpace(evidencePath))
            {
                return;
            }

            StringBuilder line = new StringBuilder();
            line.Append("utc=");
            line.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            line.Append(" | elapsedMs=");
            line.Append(Elapsed.ElapsedMilliseconds.ToString(CultureInfo.InvariantCulture));
            line.Append(" | thread=");
            line.Append(
                Thread.CurrentThread.ManagedThreadId.ToString(
                    CultureInfo.InvariantCulture
                )
            );
            line.Append(" | event=");
            line.Append(Sanitize(eventName));
            foreach (string detail in details)
            {
                line.Append(" | ");
                line.Append(Sanitize(detail));
            }
            line.AppendLine();

            lock (Sync)
            {
                File.AppendAllText(
                    evidencePath,
                    line.ToString(),
                    new UTF8Encoding(false)
                );
            }
        }

        private static string Sanitize(string value)
        {
            if (value == null)
            {
                return "<null>";
            }
            return value
                .Replace("\\", "\\\\")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("|", "\\|");
        }
    }
}
