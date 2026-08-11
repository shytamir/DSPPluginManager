using System;
using System.IO;

namespace DSPPluginManager.Discovery
{
    internal enum ManagedImageKind
    {
        Managed,
        NonManaged,
        Malformed
    }

    internal static class ManagedImageProbe
    {
        internal static ManagedImageKind Inspect(string path)
        {
            try
            {
                using (FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                ))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    if (stream.Length < 2 || reader.ReadUInt16() != 0x5A4D)
                    {
                        return ManagedImageKind.NonManaged;
                    }
                    if (stream.Length < 64)
                    {
                        return ManagedImageKind.Malformed;
                    }
                    stream.Position = 0x3C;
                    int peOffset = reader.ReadInt32();
                    if (peOffset < 0 || peOffset > stream.Length - 24)
                    {
                        return ManagedImageKind.Malformed;
                    }
                    stream.Position = peOffset;
                    if (reader.ReadUInt32() != 0x00004550)
                    {
                        return ManagedImageKind.Malformed;
                    }
                    stream.Position += 16;
                    ushort optionalSize = reader.ReadUInt16();
                    stream.Position += 2;
                    long optionalStart = stream.Position;
                    if (optionalSize < 2 ||
                        optionalStart > stream.Length - optionalSize)
                    {
                        return ManagedImageKind.Malformed;
                    }
                    ushort magic = reader.ReadUInt16();
                    int dataDirectoryOffset;
                    if (magic == 0x10B)
                    {
                        dataDirectoryOffset = 96;
                    }
                    else if (magic == 0x20B)
                    {
                        dataDirectoryOffset = 112;
                    }
                    else
                    {
                        return ManagedImageKind.Malformed;
                    }
                    const int clrDirectoryIndex = 14;
                    long clrDirectory = optionalStart + dataDirectoryOffset +
                        clrDirectoryIndex * 8;
                    if (clrDirectory > optionalStart + optionalSize - 8)
                    {
                        return ManagedImageKind.Malformed;
                    }
                    stream.Position = clrDirectory;
                    uint clrRva = reader.ReadUInt32();
                    uint clrSize = reader.ReadUInt32();
                    return clrRva == 0 || clrSize == 0 ?
                        ManagedImageKind.NonManaged : ManagedImageKind.Managed;
                }
            }
            catch (EndOfStreamException)
            {
                return ManagedImageKind.Malformed;
            }
        }
    }
}
