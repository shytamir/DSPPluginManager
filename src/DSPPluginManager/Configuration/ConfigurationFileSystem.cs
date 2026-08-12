using System.IO;

namespace DSPPluginManager.Configuration
{
    internal enum ConfigurationPathKind
    {
        Missing,
        File,
        Directory
    }

    internal interface IConfigurationFileSystem
    {
        ConfigurationPathKind GetPathKind(string path);

        void CreateDirectory(string path);

        Stream OpenRead(string path);
    }

    internal sealed class ConfigurationFileSystem : IConfigurationFileSystem
    {
        public ConfigurationPathKind GetPathKind(string path)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.Directory) != 0
                    ? ConfigurationPathKind.Directory
                    : ConfigurationPathKind.File;
            }
            catch (FileNotFoundException)
            {
                return ConfigurationPathKind.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                return ConfigurationPathKind.Missing;
            }
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        public Stream OpenRead(string path)
        {
            return new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
        }
    }
}
