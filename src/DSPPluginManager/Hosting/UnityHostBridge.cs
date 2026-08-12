using System;
using System.IO;
using System.Linq;
using System.Reflection;
using DSPPluginManager.Lifecycle;

namespace DSPPluginManager.Hosting
{
    internal sealed class UnityHostBridge
    {
        private readonly Type entrypoint;
        private readonly MethodInfo ensureCreated;
        private readonly MethodInfo activateSelected;

        internal UnityHostBridge(string unityHostAssemblyPath)
            : this(unityHostAssemblyPath, null)
        {
        }

        internal UnityHostBridge(
            string unityHostAssemblyPath,
            string contractAssemblyPath
        )
        {
            if (string.IsNullOrWhiteSpace(unityHostAssemblyPath))
            {
                throw new ArgumentException(
                    "The Unity host assembly path is required.",
                    "unityHostAssemblyPath"
                );
            }
            string path = Path.GetFullPath(unityHostAssemblyPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The Unity host assembly was not found.",
                    path
                );
            }

            Assembly contract = LoadOwnedContract(
                string.IsNullOrWhiteSpace(contractAssemblyPath)
                    ? Path.Combine(
                        Path.GetDirectoryName(path),
                        "DSPPluginManager.Contracts.dll"
                    )
                    : contractAssemblyPath
            );
            Assembly assembly = Assembly.LoadFrom(path);
            AssemblyName contractReference = assembly.GetReferencedAssemblies()
                .SingleOrDefault(reference => string.Equals(
                    reference.Name,
                    "DSPPluginManager.Contracts",
                    StringComparison.Ordinal
                ));
            if (contractReference == null || !string.Equals(
                    contractReference.FullName,
                    contract.GetName().FullName,
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    "The Unity host does not reference the manager-owned " +
                    "plugin contract identity."
                );
            }
            entrypoint = assembly.GetType(
                "DSPPluginManager.UnityHost.UnityHostEntrypoint",
                true,
                false
            );
            ensureCreated = entrypoint.GetMethod(
                "EnsureCreated",
                BindingFlags.Public | BindingFlags.Static
            );
            activateSelected = entrypoint.GetMethod(
                "ActivateSelected",
                BindingFlags.NonPublic | BindingFlags.Static
            );
            if (ensureCreated == null || activateSelected == null)
            {
                throw new MissingMethodException(
                    entrypoint.FullName,
                    ensureCreated == null
                        ? "EnsureCreated"
                        : "ActivateSelected"
                );
            }
        }

        private static Assembly LoadOwnedContract(string contractPath)
        {
            string path = Path.GetFullPath(contractPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "The manager-owned plugin contract was not found.",
                    path
                );
            }
            AssemblyName fileIdentity = AssemblyName.GetAssemblyName(path);
            if (!string.Equals(
                    fileIdentity.Name,
                    "DSPPluginManager.Contracts",
                    StringComparison.Ordinal
                ))
            {
                throw new InvalidOperationException(
                    "The manager-owned plugin contract has unexpected " +
                    "identity '" + fileIdentity.FullName + "'."
                );
            }

            Assembly contract = Assembly.LoadFrom(path);
            if (!string.Equals(
                    fileIdentity.FullName,
                    contract.GetName().FullName,
                    StringComparison.Ordinal
                ) || !string.Equals(
                    Path.GetFullPath(contract.Location),
                    path,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new InvalidOperationException(
                    "The plugin contract resolved from an unexpected " +
                    "identity or path: '" + contract.Location + "'."
                );
            }
            return contract;
        }

        internal string EnsureCreated(int unityMainThreadId)
        {
            return Invoke<string>(
                ensureCreated,
                new object[] { unityMainThreadId },
                "The persistent Unity host root could not be created."
            );
        }

        internal PluginActivationInvocationResult ActivateSelected(
            PluginActivationRequest request
        )
        {
            return Invoke<PluginActivationInvocationResult>(
                activateSelected,
                new object[]
                {
                    request ?? throw new ArgumentNullException("request")
                },
                "The selected plugin could not be invoked by the Unity host."
            );
        }

        private static T Invoke<T>(
            MethodInfo method,
            object[] arguments,
            string failureMessage
        )
        {
            try
            {
                return (T)method.Invoke(null, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidOperationException(
                    failureMessage,
                    exception.InnerException ?? exception
                );
            }
        }
    }
}
