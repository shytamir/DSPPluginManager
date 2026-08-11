using System;
using System.Globalization;

namespace DSPPluginManager.Discovery
{
    internal static class PluginContractRules
    {
        internal const string ContractAssemblyName =
            "DSPPluginManager.Contracts";
        internal const string ContractNamespace =
            "DSPPluginManager.Contracts";
        internal const string MetadataTypeName =
            "DSPPluginManager.Contracts.PluginAttribute";
        internal const string BaseTypeName =
            "DSPPluginManager.Contracts.PluginBehaviour";

        internal static StringComparer IdentifierComparer
        {
            get { return StringComparer.OrdinalIgnoreCase; }
        }

        internal static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return false;
            }
            foreach (char character in identifier)
            {
                bool valid = character >= 'a' && character <= 'z' ||
                    character >= 'A' && character <= 'Z' ||
                    character >= '0' && character <= '9' ||
                    character == '.' ||
                    character == '_' ||
                    character == '-';
                if (!valid)
                {
                    return false;
                }
            }
            return true;
        }

        internal static bool TryParseVersion(
            string value,
            out Version version
        )
        {
            version = null;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }
            int[] components = new int[3];
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index];
                if (part.Length == 0 ||
                    (part.Length > 1 && part[0] == '0'))
                {
                    return false;
                }
                foreach (char character in part)
                {
                    if (character < '0' || character > '9')
                    {
                        return false;
                    }
                }
                if (!int.TryParse(
                        part,
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out components[index]
                    ))
                {
                    return false;
                }
            }

            version = new Version(
                components[0],
                components[1],
                components[2]
            );
            return true;
        }
    }
}
