using System;
using System.Threading;
using DSPPluginManager.Lifecycle;

namespace DSPPluginManager.UnityHost
{
    public static class UnityHostEntrypoint
    {
        private static readonly object Sync = new object();
        private static UnityHostContainer current;

        public static string EnsureCreated(int unityMainThreadId)
        {
            int callerThreadId = Thread.CurrentThread.ManagedThreadId;
            if (unityMainThreadId <= 0 || callerThreadId != unityMainThreadId)
            {
                throw new InvalidOperationException(
                    "The Unity host container must be created on the " +
                    "established Unity main thread."
                );
            }

            lock (Sync)
            {
                if (current == null)
                {
                    UnityShortcutPollingBridge.Install(unityMainThreadId);
                    current = UnityHostContainer.Create(unityMainThreadId);
                    return "Persistent Unity host root created.";
                }

                current.RequireMainThread();
                return "Persistent Unity host root already exists.";
            }
        }

        internal static UnityHostContainer Current
        {
            get
            {
                lock (Sync)
                {
                    return current ?? throw new InvalidOperationException(
                        "The Unity host container has not been created."
                    );
                }
            }
        }

        internal static PluginActivationInvocationResult ActivateSelected(
            PluginActivationRequest request
        )
        {
            return Current.ActivateSelected(request);
        }

        internal static PluginStopInvocationResult StopPlugin(
            string identifier
        )
        {
            return Current.StopPlugin(identifier);
        }
    }
}
