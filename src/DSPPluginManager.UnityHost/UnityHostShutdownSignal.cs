using DSPPluginManager.Bootstrap;
using UnityEngine;

namespace DSPPluginManager.UnityHost
{
    public sealed class UnityHostShutdownSignal : MonoBehaviour
    {
        private void OnApplicationQuit()
        {
            DoorstopEntrypoint.UnityOrderlyShutdown();
        }
    }
}
