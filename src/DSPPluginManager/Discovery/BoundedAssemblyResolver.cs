using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace DSPPluginManager.Discovery
{
    internal sealed class BoundedAssemblyResolver : IAssemblyResolver
    {
        private readonly string contractPath;
        private readonly AssemblyNameDefinition contractIdentity;
        private readonly string[] searchDirectories;
        private readonly Dictionary<string, AssemblyDefinition> cache =
            new Dictionary<string, AssemblyDefinition>(StringComparer.Ordinal);

        internal BoundedAssemblyResolver(
            PluginInspectionReferences references,
            string candidateDirectory
        )
        {
            if (references == null)
            {
                throw new ArgumentNullException("references");
            }
            contractPath = references.ContractAssemblyPath;
            using (AssemblyDefinition contract = AssemblyDefinition.ReadAssembly(
                contractPath,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            {
                contractIdentity = Clone(contract.Name);
            }

            searchDirectories = new[]
            {
                references.DependencyDirectory,
                references.GameManagedDirectory,
                Path.GetFullPath(candidateDirectory)
            }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public AssemblyDefinition Resolve(AssemblyNameReference name)
        {
            return Resolve(name, new ReaderParameters());
        }

        public AssemblyDefinition Resolve(
            AssemblyNameReference name,
            ReaderParameters parameters
        )
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }
            AssemblyDefinition cached;
            if (cache.TryGetValue(name.FullName, out cached))
            {
                return cached;
            }

            if (string.Equals(
                    name.Name,
                    PluginContractRules.ContractAssemblyName,
                    StringComparison.Ordinal
                ))
            {
                if (!IdentityMatches(name, contractIdentity))
                {
                    throw new AssemblyResolutionException(name);
                }
                return ReadAndCache(name, contractPath, parameters);
            }

            if (!IsSafeSimpleName(name.Name))
            {
                throw new AssemblyResolutionException(name);
            }
            foreach (string directory in searchDirectories)
            {
                string path = Path.Combine(directory, name.Name + ".dll");
                if (!File.Exists(path))
                {
                    continue;
                }
                AssemblyNameDefinition found;
                try
                {
                    using (AssemblyDefinition assembly =
                        AssemblyDefinition.ReadAssembly(
                            path,
                            new ReaderParameters
                            {
                                InMemory = true,
                                ReadSymbols = false
                            }
                        ))
                    {
                        found = Clone(assembly.Name);
                    }
                }
                catch (Exception exception)
                    when (exception is BadImageFormatException ||
                          exception is IOException ||
                          exception is UnauthorizedAccessException)
                {
                    continue;
                }
                if (IdentityMatches(name, found))
                {
                    return ReadAndCache(name, path, parameters);
                }
            }
            throw new AssemblyResolutionException(name);
        }

        public void Dispose()
        {
            foreach (AssemblyDefinition assembly in cache.Values.Distinct())
            {
                assembly.Dispose();
            }
            cache.Clear();
        }

        private AssemblyDefinition ReadAndCache(
            AssemblyNameReference requested,
            string path,
            ReaderParameters parameters
        )
        {
            ReaderParameters bounded = new ReaderParameters
            {
                AssemblyResolver = this,
                InMemory = true,
                ReadSymbols = false,
                ReadingMode = parameters == null ?
                    ReadingMode.Deferred : parameters.ReadingMode
            };
            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(
                path,
                bounded
            );
            cache.Add(requested.FullName, assembly);
            return assembly;
        }

        private static bool IsSafeSimpleName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                string.Equals(name, Path.GetFileName(name), StringComparison.Ordinal) &&
                name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }

        private static bool IdentityMatches(
            AssemblyNameReference requested,
            AssemblyNameReference found
        )
        {
            return string.Equals(
                    requested.Name,
                    found.Name,
                    StringComparison.Ordinal
                ) && Equals(requested.Version, found.Version) &&
                string.Equals(
                    NormalizeCulture(requested.Culture),
                    NormalizeCulture(found.Culture),
                    StringComparison.OrdinalIgnoreCase
                ) && TokensEqual(
                    requested.PublicKeyToken,
                    found.PublicKeyToken
                );
        }

        private static string NormalizeCulture(string culture)
        {
            return string.IsNullOrEmpty(culture) ||
                string.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase) ?
                string.Empty : culture;
        }

        private static bool TokensEqual(byte[] left, byte[] right)
        {
            left = left ?? new byte[0];
            right = right ?? new byte[0];
            return left.SequenceEqual(right);
        }

        private static AssemblyNameDefinition Clone(AssemblyNameReference name)
        {
            AssemblyNameDefinition clone = new AssemblyNameDefinition(
                name.Name,
                name.Version
            )
            {
                Culture = name.Culture
            };
            if (name.PublicKeyToken != null)
            {
                clone.PublicKeyToken = (byte[])name.PublicKeyToken.Clone();
            }
            return clone;
        }
    }
}
