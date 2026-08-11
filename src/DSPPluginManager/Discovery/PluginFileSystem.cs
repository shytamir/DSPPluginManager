using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DSPPluginManager.Discovery
{
    internal interface IPluginFileSystem
    {
        string[] GetEntries(string directoryPath);

        PluginFileSystemEntry Inspect(string path);
    }

    internal sealed class PluginFileSystemEntry
    {
        internal PluginFileSystemEntry(
            string canonicalPath,
            string identity,
            bool isDirectory
        )
        {
            CanonicalPath = canonicalPath ??
                throw new ArgumentNullException("canonicalPath");
            Identity = identity ?? throw new ArgumentNullException("identity");
            IsDirectory = isDirectory;
        }

        internal string CanonicalPath { get; }

        internal string Identity { get; }

        internal bool IsDirectory { get; }
    }

    internal sealed class WindowsPluginFileSystem : IPluginFileSystem
    {
        private const uint OpenExisting = 3;
        private const uint BackupSemantics = 0x02000000;
        private const uint ShareRead = 0x00000001;
        private const uint ShareWrite = 0x00000002;
        private const uint ShareDelete = 0x00000004;

        public string[] GetEntries(string directoryPath)
        {
            return Directory.GetFileSystemEntries(directoryPath);
        }

        public PluginFileSystemEntry Inspect(string path)
        {
            string normalized = Path.GetFullPath(path);
            bool isDirectory = (File.GetAttributes(normalized) &
                FileAttributes.Directory) != 0;

            using (SafeFileHandle handle = CreateFile(
                normalized,
                0,
                ShareRead | ShareWrite | ShareDelete,
                IntPtr.Zero,
                OpenExisting,
                BackupSemantics,
                IntPtr.Zero
            ))
            {
                if (handle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                ByHandleFileInformation information;
                if (!GetFileInformationByHandle(handle, out information))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                string identity = information.VolumeSerialNumber.ToString("X8") +
                    ":" + information.FileIndexHigh.ToString("X8") +
                    information.FileIndexLow.ToString("X8");
                return new PluginFileSystemEntry(
                    NormalizeFinalPath(GetFinalPath(handle)),
                    identity,
                    isDirectory
                );
            }
        }

        private static string GetFinalPath(SafeFileHandle handle)
        {
            StringBuilder buffer = new StringBuilder(512);
            uint length = GetFinalPathNameByHandle(
                handle,
                buffer,
                (uint)buffer.Capacity,
                0
            );
            if (length == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            if (length >= buffer.Capacity)
            {
                buffer = new StringBuilder(checked((int)length + 1));
                length = GetFinalPathNameByHandle(
                    handle,
                    buffer,
                    (uint)buffer.Capacity,
                    0
                );
                if (length == 0 || length >= buffer.Capacity)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            return buffer.ToString();
        }

        private static string NormalizeFinalPath(string path)
        {
            const string extendedUnc = @"\\?\UNC\";
            const string extended = @"\\?\";
            if (path.StartsWith(extendedUnc, StringComparison.OrdinalIgnoreCase))
            {
                path = @"\\" + path.Substring(extendedUnc.Length);
            }
            else if (path.StartsWith(extended, StringComparison.Ordinal))
            {
                path = path.Substring(extended.Length);
            }
            return Path.GetFullPath(path);
        }

        [DllImport(
            "kernel32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true
        )]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file,
            out ByHandleFileInformation information
        );

        [DllImport(
            "kernel32.dll",
            CharSet = CharSet.Unicode,
            SetLastError = true
        )]
        private static extern uint GetFinalPathNameByHandle(
            SafeFileHandle file,
            StringBuilder filePath,
            uint filePathLength,
            uint flags
        );

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            internal uint FileAttributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            internal uint VolumeSerialNumber;
            internal uint FileSizeHigh;
            internal uint FileSizeLow;
            internal uint NumberOfLinks;
            internal uint FileIndexHigh;
            internal uint FileIndexLow;
        }
    }
}
