using System;
using System.Globalization;
using System.Text;

namespace DSPPluginManager.Logging
{
    internal static class DiskLogRecordFormatter
    {
        internal static string Format(LogRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            StringBuilder text = new StringBuilder();
            text.Append(record.Timestamp.ToString("O", CultureInfo.InvariantCulture));
            text.Append(" | ");
            text.Append(record.Severity);
            text.Append(" | ");
            text.Append(record.Source.Kind);
            text.Append(" | id=");
            text.Append(EscapeLabel(record.Source.Identifier));
            text.Append(" | name=");
            text.AppendLine(EscapeLabel(record.Source.DisplayName));
            text.AppendLine("----- BEGIN MESSAGE -----");
            text.Append(record.Message);
            if (!EndsWithNewLine(record.Message))
            {
                text.AppendLine();
            }
            text.AppendLine("----- END MESSAGE -----");
            return text.ToString();
        }

        private static string EscapeLabel(string value)
        {
            return value.Replace("\\", "\\\\").Replace("|", "\\|");
        }

        private static bool EndsWithNewLine(string value)
        {
            if (value.Length == 0)
            {
                return false;
            }
            char last = value[value.Length - 1];
            return last == '\r' || last == '\n';
        }
    }
}
