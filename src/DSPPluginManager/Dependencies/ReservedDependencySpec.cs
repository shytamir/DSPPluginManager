using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace DSPPluginManager.Dependencies
{
    internal sealed class ReservedDependencySpec
    {
        private readonly Version[] acceptedRequestVersions;

        internal ReservedDependencySpec(
            string name,
            string fileName,
            string version,
            string culture,
            string publicKeyToken,
            string sha256,
            params string[] acceptedRequestVersions
        )
        {
            Name = name;
            FileName = fileName;
            Version = new Version(version);
            Culture = culture;
            PublicKeyToken = publicKeyToken;
            Sha256 = sha256;

            this.acceptedRequestVersions = new Version[
                acceptedRequestVersions.Length
            ];
            for (int index = 0;
                index < acceptedRequestVersions.Length;
                index++)
            {
                this.acceptedRequestVersions[index] = new Version(
                    acceptedRequestVersions[index]
                );
            }
        }

        internal string Name { get; }

        internal string FileName { get; }

        internal Version Version { get; }

        internal string Culture { get; }

        internal string PublicKeyToken { get; }

        internal string Sha256 { get; }

        internal bool AcceptsRequest(AssemblyName request)
        {
            if (!string.Equals(
                    request.Name,
                    Name,
                    StringComparison.OrdinalIgnoreCase
                ) ||
                !string.Equals(
                    GetCulture(request),
                    Culture,
                    StringComparison.OrdinalIgnoreCase
                ) ||
                !string.Equals(
                    GetPublicKeyToken(request),
                    PublicKeyToken,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                return false;
            }

            foreach (Version acceptedVersion in acceptedRequestVersions)
            {
                if (object.Equals(request.Version, acceptedVersion))
                {
                    return true;
                }
            }

            return false;
        }

        internal bool MatchesSelectedIdentity(AssemblyName identity)
        {
            return string.Equals(
                    identity.Name,
                    Name,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                object.Equals(identity.Version, Version) &&
                string.Equals(
                    GetCulture(identity),
                    Culture,
                    StringComparison.OrdinalIgnoreCase
                ) &&
                string.Equals(
                    GetPublicKeyToken(identity),
                    PublicKeyToken,
                    StringComparison.OrdinalIgnoreCase
                );
        }

        internal string DescribeAcceptedRequests()
        {
            List<string> versions = new List<string>();
            foreach (Version version in acceptedRequestVersions)
            {
                versions.Add(version.ToString());
            }

            return "name '" + Name + "', version " +
                string.Join(" or ", versions.ToArray()) +
                ", culture '" + Culture + "', public key token '" +
                PublicKeyToken + "'";
        }

        internal string DescribeSelectedIdentity()
        {
            return Name + ", Version=" + Version + ", Culture=" + Culture +
                ", PublicKeyToken=" + PublicKeyToken;
        }

        internal static string GetCulture(AssemblyName identity)
        {
            if (identity.CultureInfo == null ||
                string.IsNullOrWhiteSpace(identity.CultureInfo.Name))
            {
                return "neutral";
            }

            return identity.CultureInfo.Name;
        }

        internal static string GetPublicKeyToken(AssemblyName identity)
        {
            byte[] token = identity.GetPublicKeyToken();
            if (token == null || token.Length == 0)
            {
                return "null";
            }

            return BitConverter.ToString(token)
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
