using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace DSPPluginManager.RM05Probe
{
    public static class ProbeRecorder
    {
        private static readonly object Sync = new object();
        private static readonly Stopwatch Elapsed = Stopwatch.StartNew();
        private static string evidencePath;
        private static string mode;
        private static int preloadThreadId;
        private static int callbackCount;

        public static string Mode
        {
            get { return mode; }
        }

        public static int PreloadThreadId
        {
            get { return preloadThreadId; }
        }

        public static void Initialize(string probeRoot, string selectedMode)
        {
            mode = selectedMode;
            preloadThreadId = Thread.CurrentThread.ManagedThreadId;
            evidencePath = Path.Combine(
                probeRoot,
                "probe-evidence-" + selectedMode + ".log"
            );
            Record("recorder-initialized", "mode=" + selectedMode);
        }

        public static int RecordCallback(string candidate, params string[] details)
        {
            int count = Interlocked.Increment(ref callbackCount);
            string[] combined = new string[details.Length + 3];
            combined[0] = "candidate=" + candidate;
            combined[1] = "callbackCount=" + count;
            combined[2] = "sameAsPreloadThread=" +
                (Thread.CurrentThread.ManagedThreadId == preloadThreadId);
            Array.Copy(details, 0, combined, 3, details.Length);
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
