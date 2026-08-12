using System;
using System.IO;
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

            Assembly assembly = Assembly.LoadFrom(path);
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
