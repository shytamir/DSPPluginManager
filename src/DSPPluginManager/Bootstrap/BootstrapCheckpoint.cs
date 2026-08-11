using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace DSPPluginManager.Bootstrap
{
    internal static class BootstrapCheckpoint
    {
        private const string FileName = "bootstrap-checkpoint.txt";
        private const string OptInFileName = "bootstrap-checkpoint.enabled";

        internal static void WritePreload(string hostRoot, int threadId)
        {
            if (!IsEnabled(hostRoot))
            {
                return;
            }
            File.WriteAllText(
                Path.Combine(hostRoot, FileName),
                Format("managed-entry", threadId, null),
                new UTF8Encoding(false)
            );
        }

        internal static void WriteHandoff(string hostRoot, int threadId)
        {
            if (!IsEnabled(hostRoot))
            {
                return;
            }
            SynchronizationContext context = SynchronizationContext.Current;
            File.AppendAllText(
                Path.Combine(hostRoot, FileName),
                Format(
                    "unity-main-thread-handoff",
                    threadId,
                    context == null ? "<null>" : context.GetType().FullName
                ),
                new UTF8Encoding(false)
            );
        }

        private static bool IsEnabled(string hostRoot)
        {
            return File.Exists(Path.Combine(hostRoot, OptInFileName));
        }

        private static string Format(
            string eventName,
            int threadId,
            string synchronizationContext
        )
        {
            StringBuilder line = new StringBuilder();
            line.Append("utc=");
            line.Append(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            line.Append(" | event=");
            line.Append(eventName);
            line.Append(" | thread=");
            line.Append(threadId.ToString(CultureInfo.InvariantCulture));
            if (synchronizationContext != null)
            {
                line.Append(" | synchronizationContext=");
                line.Append(synchronizationContext);
            }
            line.AppendLine();
            return line.ToString();
        }
    }
}
