using System;
using DSPPluginManager.Contracts;
using DSPPluginManager.Lifecycle;
using UnityEngine;

namespace DSPPluginManager.UnityHost
{
    internal sealed class PluginObjectSlot
    {
        internal PluginObjectSlot(string identifier, GameObject gameObject)
        {
            Identifier = identifier ?? throw new ArgumentNullException(
                "identifier"
            );
            GameObject = gameObject ?? throw new ArgumentNullException(
                "gameObject"
            );
        }

        internal string Identifier { get; }

        internal GameObject GameObject { get; }

        internal PluginBehaviour Instance { get; private set; }

        internal PluginActivationInvocationResult ActivationResult
        {
            get;
            private set;
        }

        internal void RetainInstance(PluginBehaviour instance)
        {
            if (Instance != null)
            {
                throw new InvalidOperationException(
                    "The plugin object already owns a component instance."
                );
            }
            Instance = instance ?? throw new ArgumentNullException("instance");
        }

        internal void RetainActivationResult(
            PluginActivationInvocationResult result
        )
        {
            if (ActivationResult != null)
            {
                throw new InvalidOperationException(
                    "The plugin object already has an activation result."
                );
            }
            ActivationResult = result ?? throw new ArgumentNullException(
                "result"
            );
        }
    }
}
