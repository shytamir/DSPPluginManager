using System;
using System.IO;
using System.Security;
using System.Text;

namespace DSPPluginManager.Configuration
{
    internal enum ConfigurationPersistenceFailureStage
    {
        TemporaryWrite,
        Flush,
        FinalPath,
        Replace,
        Move
    }

    internal sealed class ConfigurationPersistenceResult
    {
        private ConfigurationPersistenceResult(
            ConfigurationPersistenceFailureStage? failureStage,
            Exception failure
        )
        {
            FailureStage = failureStage;
            Failure = failure;
        }

        internal bool Succeeded
        {
            get { return FailureStage == null; }
        }

        internal ConfigurationPersistenceFailureStage? FailureStage { get; }

        internal Exception Failure { get; }

        internal static ConfigurationPersistenceResult Success()
        {
            return new ConfigurationPersistenceResult(null, null);
        }

        internal static ConfigurationPersistenceResult Failed(
            ConfigurationPersistenceFailureStage stage,
            Exception failure
        )
        {
            return new ConfigurationPersistenceResult(stage, failure);
        }
    }

    internal interface IPluginConfigurationPersistence
    {
        ConfigurationPersistenceResult Save(string finalPath, string contents);
    }

    internal interface IConfigurationPersistenceFileSystem
    {
        Stream CreateTemporaryFile(string path);

        void FlushToDisk(Stream stream);

        ConfigurationPathKind GetPathKind(string path);

        void Replace(string sourcePath, string destinationPath);

        void Move(string sourcePath, string destinationPath);

        void Delete(string path);
    }

    internal sealed class PluginConfigurationPersistence :
        IPluginConfigurationPersistence
    {
        private static readonly UTF8Encoding Utf8WithoutBom =
            new UTF8Encoding(false);

        private readonly IConfigurationPersistenceFileSystem fileSystem;

        internal PluginConfigurationPersistence()
            : this(new ConfigurationPersistenceFileSystem())
        {
        }

        internal PluginConfigurationPersistence(
            IConfigurationPersistenceFileSystem fileSystem
        )
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(
                "fileSystem"
            );
        }

        public ConfigurationPersistenceResult Save(
            string finalPath,
            string contents
        )
        {
            if (string.IsNullOrEmpty(finalPath))
            {
                throw new ArgumentException(
                    "A final configuration path is required.",
                    "finalPath"
                );
            }
            if (contents == null)
            {
                throw new ArgumentNullException("contents");
            }

            string directory = Path.GetDirectoryName(finalPath);
            string temporaryPath = Path.Combine(
                directory,
                "." + Path.GetFileName(finalPath) + "." +
                Guid.NewGuid().ToString("N") + ".tmp"
            );
            bool temporaryOwned = false;
            try
            {
                Stream stream = null;
                try
                {
                    stream = fileSystem.CreateTemporaryFile(temporaryPath);
                    temporaryOwned = true;
                    byte[] bytes = Utf8WithoutBom.GetBytes(contents);
                    stream.Write(bytes, 0, bytes.Length);
                }
                catch (Exception exception) when (IsFileSystemFailure(exception))
                {
                    TryDispose(stream);
                    return Failed(
                        ConfigurationPersistenceFailureStage.TemporaryWrite,
                        exception
                    );
                }

                try
                {
                    using (stream)
                    {
                        fileSystem.FlushToDisk(stream);
                    }
                }
                catch (Exception exception) when (IsFileSystemFailure(exception))
                {
                    return Failed(
                        ConfigurationPersistenceFailureStage.Flush,
                        exception
                    );
                }

                ConfigurationPathKind finalKind;
                try
                {
                    finalKind = fileSystem.GetPathKind(finalPath);
                }
                catch (Exception exception) when (IsFileSystemFailure(exception))
                {
                    return Failed(
                        ConfigurationPersistenceFailureStage.FinalPath,
                        exception
                    );
                }

                if (finalKind == ConfigurationPathKind.Directory)
                {
                    return Failed(
                        ConfigurationPersistenceFailureStage.FinalPath,
                        new IOException(
                            "The final configuration path is a directory."
                        )
                    );
                }

                try
                {
                    if (finalKind == ConfigurationPathKind.File)
                    {
                        fileSystem.Replace(temporaryPath, finalPath);
                    }
                    else
                    {
                        fileSystem.Move(temporaryPath, finalPath);
                    }
                    temporaryOwned = false;
                    return ConfigurationPersistenceResult.Success();
                }
                catch (Exception exception) when (IsFileSystemFailure(exception))
                {
                    return Failed(
                        finalKind == ConfigurationPathKind.File
                            ? ConfigurationPersistenceFailureStage.Replace
                            : ConfigurationPersistenceFailureStage.Move,
                        exception
                    );
                }
            }
            finally
            {
                if (temporaryOwned)
                {
                    TryDelete(temporaryPath);
                }
            }
        }

        private ConfigurationPersistenceResult Failed(
            ConfigurationPersistenceFailureStage stage,
            Exception failure
        )
        {
            return ConfigurationPersistenceResult.Failed(stage, failure);
        }

        private static void TryDispose(Stream stream)
        {
            if (stream == null)
            {
                return;
            }
            try
            {
                stream.Dispose();
            }
            catch
            {
            }
        }

        private void TryDelete(string path)
        {
            try
            {
                fileSystem.Delete(path);
            }
            catch (Exception exception) when (IsFileSystemFailure(exception))
            {
            }
        }

        private static bool IsFileSystemFailure(Exception exception)
        {
            return exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is SecurityException ||
                exception is NotSupportedException;
        }
    }

    internal sealed class ConfigurationPersistenceFileSystem :
        IConfigurationPersistenceFileSystem
    {
        public Stream CreateTemporaryFile(string path)
        {
            return new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            );
        }

        public void FlushToDisk(Stream stream)
        {
            FileStream fileStream = stream as FileStream;
            if (fileStream == null)
            {
                throw new InvalidOperationException(
                    "The persistence stream is not a file stream."
                );
            }
            fileStream.Flush(true);
        }

        public ConfigurationPathKind GetPathKind(string path)
        {
            return new ConfigurationFileSystem().GetPathKind(path);
        }

        public void Replace(string sourcePath, string destinationPath)
        {
            File.Replace(sourcePath, destinationPath, null);
        }

        public void Move(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        public void Delete(string path)
        {
            File.Delete(path);
        }
    }
}
