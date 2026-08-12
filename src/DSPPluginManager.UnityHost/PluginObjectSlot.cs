using System;
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
    }
}
