using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace DSPPluginManager.Bootstrap
{
    internal delegate void BootstrapDiagnosticWriteAction(
        string path,
        string content
    );

    internal static class BootstrapFailureRecord
    {
        private const string FilePrefix =
            "DSPPluginManager-bootstrap-failure-";

        internal static bool TryWrite(
            BootstrapFailureContext context,
            Exception startupFailure,
            out string diagnosticPath
        )
        {
            return TryWrite(
                context,
                startupFailure,
                WriteUtf8File,
                out diagnosticPath
            );
        }

        internal static bool TryWrite(
            BootstrapFailureContext context,
            Exception startupFailure,
            BootstrapDiagnosticWriteAction write,
            out string diagnosticPath
        )
        {
            diagnosticPath = null;

            try
            {
                if (context == null || write == null)
                {
                    return false;
                }

                string recoveryDirectory = GetRecoveryDirectory(
                    context.ExecutablePath
                );
                if (recoveryDirectory == null)
                {
                    return false;
                }

                diagnosticPath = Path.Combine(
                    recoveryDirectory,
                    FilePrefix +
                    DateTime.UtcNow.ToString(
                        "yyyyMMdd'T'HHmmssfff'Z'",
                        CultureInfo.InvariantCulture
                    ) +
                    "-" + Guid.NewGuid().ToString("N") + ".txt"
                );

                write(
                    diagnosticPath,
                    FormatRecord(context, startupFailure)
                );
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string GetRecoveryDirectory(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) ||
                !Path.IsPathRooted(executablePath) ||
                IsDriveRelative(executablePath))
            {
                return null;
            }

            string normalized = Path.GetFullPath(executablePath);
            return Path.GetDirectoryName(normalized);
        }

        private static bool IsDriveRelative(string path)
        {
            return path.Length >= 2 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path.Length == 2 || !IsDirectorySeparator(path[2]));
        }

        private static bool IsDirectorySeparator(char value)
        {
            return value == Path.DirectorySeparatorChar ||
                value == Path.AltDirectorySeparatorChar;
        }

        private static string FormatRecord(
            BootstrapFailureContext context,
            Exception startupFailure
        )
        {
            StringBuilder record = new StringBuilder();
            record.AppendLine("DSP Plugin Manager bootstrap failure");
            AppendField(record, "Phase", context.Phase);
            AppendField(
                record,
                "Target assembly",
                context.TargetAssemblyPath
            );
            AppendField(record, "Executable", context.ExecutablePath);
            AppendField(record, "Managed directory", context.ManagedDirectory);
            AppendField(record, "Host root", context.HostRoot);
            AppendField(
                record,
                "Dependency directory",
                context.DependencyDirectory
            );
            record.AppendLine("Exception:");
            record.AppendLine("----- BEGIN EXCEPTION -----");
            record.AppendLine(FormatException(startupFailure));
            record.AppendLine("----- END EXCEPTION -----");
            return record.ToString();
        }

        private static void AppendField(
            StringBuilder record,
            string name,
            string value
        )
        {
            record.Append(name);
            record.Append(": ");
            record.AppendLine(FormatField(value));
        }

        private static string FormatField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "<unavailable>";
            }

            StringBuilder safe = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\r':
                        safe.Append("\\r");
                        break;
                    case '\n':
                        safe.Append("\\n");
                        break;
                    case '\t':
                        safe.Append("\\t");
                        break;
                    default:
                        if (char.IsControl(character))
                        {
                            safe.Append("\\u");
                            safe.Append(
                                ((int)character).ToString(
                                    "X4",
                                    CultureInfo.InvariantCulture
                                )
                            );
                        }
                        else
                        {
                            safe.Append(character);
                        }
                        break;
                }
            }

            return safe.ToString();
        }

        private static string FormatException(Exception startupFailure)
        {
            if (startupFailure == null)
            {
                return "<exception unavailable>";
            }

            try
            {
                return startupFailure.ToString();
            }
            catch (Exception)
            {
                return "<exception formatting failed for " +
                    startupFailure.GetType().FullName + ">";
            }
        }

        private static void WriteUtf8File(string path, string content)
        {
            File.WriteAllText(path, content, new UTF8Encoding(false));
        }
    }
}
