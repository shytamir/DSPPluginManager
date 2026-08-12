using System;
using System.Collections.Generic;
using System.Threading;
using DSPPluginManager.Discovery;
using UnityEngine;

namespace DSPPluginManager.UnityHost
{
    internal sealed class UnityHostContainer
    {
        internal const string RootName = "DSPPluginManager";
        private const string PluginObjectPrefix = "DSPPluginManager.Plugin.";

        private readonly int mainThreadId;
        private readonly Dictionary<string, PluginObjectSlot> pluginObjects;

        private UnityHostContainer(int mainThreadId, GameObject root)
        {
            this.mainThreadId = mainThreadId;
            Root = root ?? throw new ArgumentNullException("root");
            pluginObjects = new Dictionary<string, PluginObjectSlot>(
                PluginContractRules.IdentifierComparer
            );
        }

        internal GameObject Root { get; }

        internal int PluginObjectCount
        {
            get { return pluginObjects.Count; }
        }

        internal static UnityHostContainer Create(int mainThreadId)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                throw new InvalidOperationException(
                    "Unity host creation left the established main thread."
                );
            }

            GameObject root = new GameObject(RootName);
            UnityEngine.Object.DontDestroyOnLoad(root);
            return new UnityHostContainer(mainThreadId, root);
        }

        internal PluginObjectSlot GetOrCreatePluginObject(string identifier)
        {
            RequireMainThread();
            string key = PluginContractRules.GetIdentifierComparisonKey(
                identifier
            );
            PluginObjectSlot existing;
            if (pluginObjects.TryGetValue(key, out existing))
            {
                return existing;
            }

            string canonicalIdentifier = identifier.ToLowerInvariant();
            GameObject child = new GameObject(
                PluginObjectPrefix + canonicalIdentifier
            );
            child.transform.SetParent(Root.transform, false);
            PluginObjectSlot created = new PluginObjectSlot(
                canonicalIdentifier,
                child
            );
            pluginObjects.Add(key, created);
            return created;
        }

        internal void RequireMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                throw new InvalidOperationException(
                    "Unity host access must remain on its creation thread."
                );
            }
        }
    }
}
