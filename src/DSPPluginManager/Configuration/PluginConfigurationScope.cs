using System;
using System.IO;
using System.Security;
using System.Text;
using DSPPluginManager.Discovery;

namespace DSPPluginManager.Configuration
{
    internal enum PluginConfigurationSourceState
    {
        Missing,
        Empty,
        Present,
        Unavailable
    }

    internal sealed class PluginConfigurationScope
    {
        private PluginConfigurationScope(
            string identifier,
            string filePath,
            PluginConfigurationSourceState sourceState,
            string contents,
            Exception failure
        )
        {
            Identifier = identifier;
            FilePath = filePath;
            SourceState = sourceState;
            Contents = contents;
            Failure = failure;
        }

        internal string Identifier { get; }

        internal string FilePath { get; }

        internal PluginConfigurationSourceState SourceState { get; }

        internal Exception Failure { get; }

        internal string Contents { get; }

        internal bool IsUsable
        {
            get
            {
                return SourceState !=
                    PluginConfigurationSourceState.Unavailable;
            }
        }

        internal static PluginConfigurationScope Create(
            string configurationDirectory,
            string identifier
        )
        {
            return Create(
                configurationDirectory,
                identifier,
                new ConfigurationFileSystem()
            );
        }

        internal static PluginConfigurationScope Create(
            string configurationDirectory,
            string identifier,
            IConfigurationFileSystem fileSystem
        )
        {
            if (!PluginContractRules.IsValidIdentifier(identifier))
            {
                throw new ArgumentException(
                    "Plugin identifier is invalid.",
                    "identifier"
                );
            }
            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }

            string directory = NormalizeAbsoluteDirectory(
                configurationDirectory
            );
            string canonicalIdentifier = identifier.ToLowerInvariant();
            string filePath = Path.GetFullPath(Path.Combine(
                directory,
                canonicalIdentifier + ".cfg"
            ));
            if (!string.Equals(
                    Path.GetDirectoryName(filePath),
                    directory,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new ArgumentException(
                    "Plugin configuration path escaped its configured root.",
                    "identifier"
                );
            }

            try
            {
                ConfigurationPathKind directoryKind =
                    fileSystem.GetPathKind(directory);
                if (directoryKind == ConfigurationPathKind.File)
                {
                    return Unavailable(
                        canonicalIdentifier,
                        filePath,
                        new IOException(
                            "The configuration parent is a file: '" +
                            directory + "'."
                        )
                    );
                }
                if (directoryKind == ConfigurationPathKind.Missing)
                {
                    fileSystem.CreateDirectory(directory);
                }

                ConfigurationPathKind fileKind =
                    fileSystem.GetPathKind(filePath);
                if (fileKind == ConfigurationPathKind.Missing)
                {
                    return Available(
                        canonicalIdentifier,
                        filePath,
                        PluginConfigurationSourceState.Missing,
                        string.Empty
                    );
                }
                if (fileKind == ConfigurationPathKind.Directory)
                {
                    return Unavailable(
                        canonicalIdentifier,
                        filePath,
                        new IOException(
                            "The plugin configuration path is a directory: '" +
                            filePath + "'."
                        )
                    );
                }

                using (Stream stream = fileSystem.OpenRead(filePath))
                using (StreamReader reader = new StreamReader(
                    stream,
                    new UTF8Encoding(false, true),
                    true
                ))
                {
                    bool empty = stream.Length == 0;
                    string contents = reader.ReadToEnd();
                    return Available(
                        canonicalIdentifier,
                        filePath,
                        empty
                            ? PluginConfigurationSourceState.Empty
                            : PluginConfigurationSourceState.Present,
                        contents
                    );
                }
            }
            catch (Exception exception) when (IsFileSystemFailure(exception))
            {
                return Unavailable(
                    canonicalIdentifier,
                    filePath,
                    exception
                );
            }
        }

        private static PluginConfigurationScope Available(
            string identifier,
            string filePath,
            PluginConfigurationSourceState sourceState,
            string contents
        )
        {
            return new PluginConfigurationScope(
                identifier,
                filePath,
                sourceState,
                contents,
                null
            );
        }

        private static PluginConfigurationScope Unavailable(
            string identifier,
            string filePath,
            Exception failure
        )
        {
            return new PluginConfigurationScope(
                identifier,
                filePath,
                PluginConfigurationSourceState.Unavailable,
                null,
                failure
            );
        }

        private static string NormalizeAbsoluteDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Configuration directory is required.",
                    "configurationDirectory"
                );
            }
            if (!Path.IsPathRooted(path) || IsDriveRelative(path))
            {
                throw new ArgumentException(
                    "Configuration directory must be absolute: '" + path +
                    "'.",
                    "configurationDirectory"
                );
            }

            try
            {
                string normalized = Path.GetFullPath(path);
                string volumeRoot = Path.GetPathRoot(normalized);
                if (!string.Equals(
                        normalized,
                        volumeRoot,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    normalized = normalized.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    );
                }
                return normalized;
            }
            catch (Exception exception)
                when (exception is ArgumentException ||
                      exception is NotSupportedException ||
                      exception is PathTooLongException)
            {
                throw new ArgumentException(
                    "Configuration directory is invalid: '" + path + "'.",
                    "configurationDirectory",
                    exception
                );
            }
        }

        private static bool IsFileSystemFailure(Exception exception)
        {
            return exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is SecurityException ||
                exception is DecoderFallbackException;
        }

        private static bool IsDriveRelative(string path)
        {
            return path.Length >= 2 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path.Length == 2 ||
                 path[2] != Path.DirectorySeparatorChar &&
                 path[2] != Path.AltDirectorySeparatorChar);
        }
    }
}
