using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace DSPPluginManager.Dependencies
{
    internal delegate LoadedDependencyAssembly[] LoadedAssemblyProvider();

    internal sealed class ReservedDependencyResolver : IDisposable
    {
        private readonly string dependencyDirectory;
        private readonly Dictionary<string, string[]> pluginDuplicates;
        private readonly LoadedAssemblyProvider loadedAssemblyProvider;
        private readonly object sync = new object();
        private bool installed;

        internal ReservedDependencyResolver(
            string dependencyDirectory,
            string pluginDirectory
        ) : this(
            dependencyDirectory,
            pluginDirectory,
            GetLoadedAssemblies
        )
        {
        }

        internal ReservedDependencyResolver(
            string dependencyDirectory,
            string pluginDirectory,
            LoadedAssemblyProvider loadedAssemblyProvider
        )
        {
            this.dependencyDirectory = RequireDirectory(
                dependencyDirectory,
                "dependency"
            );
            string plugins = RequireDirectory(pluginDirectory, "plugin");
            this.loadedAssemblyProvider = loadedAssemblyProvider ??
                throw new ArgumentNullException("loadedAssemblyProvider");
            pluginDuplicates = FindPluginDuplicates(plugins);
        }

        internal void Install()
        {
            lock (sync)
            {
                if (installed)
                {
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                installed = true;
            }
        }

        internal Assembly ResolveForTest(
            AssemblyName requestedIdentity,
            Assembly requestingAssembly
        )
        {
            return Resolve(requestedIdentity, requestingAssembly);
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (!installed)
                {
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
                installed = false;
            }
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName requestedIdentity;
            try
            {
                requestedIdentity = new AssemblyName(args.Name);
            }
            catch (Exception)
            {
                return null;
            }

            return Resolve(requestedIdentity, args.RequestingAssembly);
        }

        private Assembly Resolve(
            AssemblyName requestedIdentity,
            Assembly requestingAssembly
        )
        {
            if (requestedIdentity == null)
            {
                return null;
            }

            ReservedDependencySpec specification =
                ReservedDependencyCatalog.Find(requestedIdentity.Name);
            if (specification == null)
            {
                return null;
            }

            string requester = DescribeRequester(requestingAssembly);
            lock (sync)
            {
                if (!specification.AcceptsRequest(requestedIdentity))
                {
                    throw Failure(
                        requestedIdentity,
                        requester,
                        "the requested identity is not approved; expected " +
                        specification.DescribeAcceptedRequests() + "."
                    );
                }

                string[] duplicates;
                if (pluginDuplicates.TryGetValue(
                        specification.Name,
                        out duplicates
                    ))
                {
                    throw Failure(
                        requestedIdentity,
                        requester,
                        "plugin-local reserved dependency duplicate(s) were " +
                        "found at: " + string.Join(", ", duplicates) + "."
                    );
                }

                string selectedPath = Path.Combine(
                    dependencyDirectory,
                    specification.FileName
                );
                ValidateSelectedFile(
                    specification,
                    selectedPath,
                    requestedIdentity,
                    requester
                );

                List<LoadedDependencyAssembly> loadedMatches =
                    FindLoadedMatches(specification.Name);
                if (loadedMatches.Count > 1)
                {
                    throw Failure(
                        requestedIdentity,
                        requester,
                        "multiple assemblies with the reserved identity are " +
                        "already loaded: " + DescribeLoaded(loadedMatches) + "."
                    );
                }
                if (loadedMatches.Count == 1)
                {
                    LoadedDependencyAssembly loaded = loadedMatches[0];
                    ValidateLoadedAssembly(
                        specification,
                        selectedPath,
                        loaded,
                        requestedIdentity,
                        requester
                    );
                    return loaded.Assembly;
                }

                Assembly selected;
                try
                {
                    selected = Assembly.LoadFrom(selectedPath);
                }
                catch (Exception exception)
                {
                    throw Failure(
                        requestedIdentity,
                        requester,
                        "the validated host-owned assembly at '" +
                        selectedPath + "' could not be loaded.",
                        exception
                    );
                }

                ValidateLoadedAssembly(
                    specification,
                    selectedPath,
                    LoadedDependencyAssembly.FromAssembly(selected),
                    requestedIdentity,
                    requester
                );
                return selected;
            }
        }

        private List<LoadedDependencyAssembly> FindLoadedMatches(string name)
        {
            List<LoadedDependencyAssembly> matches =
                new List<LoadedDependencyAssembly>();
            LoadedDependencyAssembly[] loaded = loadedAssemblyProvider();
            if (loaded == null)
            {
                return matches;
            }

            foreach (LoadedDependencyAssembly candidate in loaded)
            {
                if (candidate != null && candidate.Identity != null &&
                    string.Equals(
                        candidate.Identity.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    matches.Add(candidate);
                }
            }

            return matches;
        }

        private static void ValidateLoadedAssembly(
            ReservedDependencySpec specification,
            string selectedPath,
            LoadedDependencyAssembly loaded,
            AssemblyName requestedIdentity,
            string requester
        )
        {
            if (loaded.Assembly == null || loaded.Identity == null ||
                !specification.MatchesSelectedIdentity(loaded.Identity) ||
                string.IsNullOrWhiteSpace(loaded.Location) ||
                !string.Equals(
                    Path.GetFullPath(loaded.Location),
                    selectedPath,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw Failure(
                    requestedIdentity,
                    requester,
                    "a conflicting reserved assembly is already loaded: " +
                    DescribeLoaded(loaded) + "; required host identity '" +
                    specification.DescribeSelectedIdentity() + "' at '" +
                    selectedPath + "'."
                );
            }
        }

        private static void ValidateSelectedFile(
            ReservedDependencySpec specification,
            string selectedPath,
            AssemblyName requestedIdentity,
            string requester
        )
        {
            if (!File.Exists(selectedPath))
            {
                throw Failure(
                    requestedIdentity,
                    requester,
                    "the required host-owned file is missing: '" +
                    selectedPath + "'."
                );
            }

            AssemblyName actualIdentity;
            try
            {
                actualIdentity = AssemblyName.GetAssemblyName(selectedPath);
            }
            catch (Exception exception)
            {
                throw Failure(
                    requestedIdentity,
                    requester,
                    "the host-owned file is not a readable managed assembly: '" +
                    selectedPath + "'.",
                    exception
                );
            }

            if (!specification.MatchesSelectedIdentity(actualIdentity))
            {
                throw Failure(
                    requestedIdentity,
                    requester,
                    "the host-owned file at '" + selectedPath +
                    "' has identity '" + actualIdentity.FullName +
                    "'; expected '" +
                    specification.DescribeSelectedIdentity() + "'."
                );
            }

            string actualHash;
            try
            {
                actualHash = ComputeSha256(selectedPath);
            }
            catch (Exception exception)
            {
                throw Failure(
                    requestedIdentity,
                    requester,
                    "the host-owned file could not be integrity checked: '" +
                    selectedPath + "'.",
                    exception
                );
            }

            if (!string.Equals(
                    actualHash,
                    specification.Sha256,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw Failure(
                    requestedIdentity,
                    requester,
                    "the host-owned file failed SHA-256 integrity validation: '" +
                    selectedPath + "'; expected " + specification.Sha256 +
                    ", found " + actualHash + "."
                );
            }
        }

        private static Dictionary<string, string[]> FindPluginDuplicates(
            string pluginDirectory
        )
        {
            Dictionary<string, List<string>> found =
                new Dictionary<string, List<string>>(
                    StringComparer.OrdinalIgnoreCase
                );
            string[] files = Directory.GetFiles(
                pluginDirectory,
                "*.dll",
                SearchOption.AllDirectories
            );
            Array.Sort(files, StringComparer.Ordinal);

            foreach (string file in files)
            {
                ReservedDependencySpec specification =
                    ReservedDependencyCatalog.Find(
                        Path.GetFileNameWithoutExtension(file)
                    );
                if (specification == null)
                {
                    try
                    {
                        specification = ReservedDependencyCatalog.Find(
                            AssemblyName.GetAssemblyName(file).Name
                        );
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                if (specification == null)
                {
                    continue;
                }

                List<string> paths;
                if (!found.TryGetValue(specification.Name, out paths))
                {
                    paths = new List<string>();
                    found.Add(specification.Name, paths);
                }
                paths.Add(Path.GetFullPath(file));
            }

            Dictionary<string, string[]> result =
                new Dictionary<string, string[]>(
                    StringComparer.OrdinalIgnoreCase
                );
            foreach (KeyValuePair<string, List<string>> pair in found)
            {
                result.Add(pair.Key, pair.Value.ToArray());
            }
            return result;
        }

        private static LoadedDependencyAssembly[] GetLoadedAssemblies()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            LoadedDependencyAssembly[] result =
                new LoadedDependencyAssembly[assemblies.Length];
            for (int index = 0; index < assemblies.Length; index++)
            {
                result[index] = LoadedDependencyAssembly.FromAssembly(
                    assemblies[index]
                );
            }
            return result;
        }

        private static string RequireDirectory(string path, string role)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
            {
                throw new ArgumentException(
                    role + " directory must be an absolute path.",
                    role
                );
            }

            string normalized = Path.GetFullPath(path);
            string root = Path.GetPathRoot(normalized);
            if (!string.Equals(
                    normalized,
                    root,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                normalized = normalized.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );
            }
            if (!Directory.Exists(normalized))
            {
                throw new InvalidOperationException(
                    role + " directory does not exist: '" + normalized + "'."
                );
            }
            return normalized;
        }

        private static string ComputeSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }

        private static string DescribeRequester(Assembly requestingAssembly)
        {
            if (requestingAssembly == null)
            {
                return "<unknown requesting assembly>";
            }

            string location;
            try
            {
                location = requestingAssembly.IsDynamic
                    ? "<dynamic>"
                    : requestingAssembly.Location;
            }
            catch (Exception)
            {
                location = "<unavailable>";
            }

            return "'" + requestingAssembly.FullName + "' at '" + location +
                "'";
        }

        private static string DescribeLoaded(
            IList<LoadedDependencyAssembly> loaded
        )
        {
            List<string> descriptions = new List<string>();
            foreach (LoadedDependencyAssembly assembly in loaded)
            {
                descriptions.Add(DescribeLoaded(assembly));
            }
            return string.Join(", ", descriptions.ToArray());
        }

        private static string DescribeLoaded(LoadedDependencyAssembly loaded)
        {
            string identity = loaded == null || loaded.Identity == null
                ? "<unknown identity>"
                : loaded.Identity.FullName;
            string location = loaded == null ||
                string.IsNullOrWhiteSpace(loaded.Location)
                ? "<dynamic or unavailable>"
                : loaded.Location;
            return "'" + identity + "' at '" + location + "'";
        }

        private static InvalidOperationException Failure(
            AssemblyName requestedIdentity,
            string requester,
            string reason,
            Exception innerException = null
        )
        {
            string message = "Reserved dependency resolution failed for '" +
                requestedIdentity.FullName + "' requested by " + requester +
                ": " + reason;
            return innerException == null
                ? new InvalidOperationException(message)
                : new InvalidOperationException(message, innerException);
        }
    }
}
