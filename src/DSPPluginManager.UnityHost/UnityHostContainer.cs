using System;
using System.Collections.Generic;
using System.Threading;
using DSPPluginManager.Contracts;
using DSPPluginManager.Discovery;
using DSPPluginManager.Lifecycle;
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

        internal PluginActivationInvocationResult ActivateSelected(
            PluginActivationRequest request
        )
        {
            RequireMainThread();
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }
            PluginObjectSlot slot = GetOrCreatePluginObject(
                request.Candidate.Identifier
            );
            if (slot.ActivationResult != null)
            {
                return slot.ActivationResult;
            }

            PluginBehaviour instance;
            try
            {
                Component component = slot.GameObject.AddComponent(
                    request.PluginType
                );
                instance = component as PluginBehaviour;
                if (instance == null ||
                    instance.GetType() != request.PluginType)
                {
                    throw new InvalidOperationException(
                        "Unity did not attach the exact inspected plugin type."
                    );
                }
                slot.RetainInstance(instance);
            }
            catch (Exception exception)
            {
                return RetainFailure(
                    slot,
                    slot.Instance,
                    "component-construction",
                    exception
                );
            }

            try
            {
                PluginLogger logger = new PluginLogger(
                    request.Candidate.Identifier,
                    request.Candidate.DisplayName,
                    request.Logger.Information,
                    request.Logger.Warning,
                    request.Logger.Error
                );
                instance.InitializeLogger(logger);
                instance.InitializeWritableRoot(request.WritableRoot);
                instance.enabled = true;
            }
            catch (Exception exception)
            {
                return RetainFailure(
                    slot,
                    CleanupFailedInstance(slot, instance, exception),
                    "service-preparation",
                    exception
                );
            }

            try
            {
                instance.Activate();
                PluginActivationInvocationResult active =
                    PluginActivationInvocationResult.Active(instance);
                slot.RetainActivationResult(active);
                return active;
            }
            catch (Exception exception)
            {
                return RetainFailure(
                    slot,
                    CleanupFailedInstance(slot, instance, exception),
                    "activation",
                    exception
                );
            }
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

        private static PluginActivationInvocationResult RetainFailure(
            PluginObjectSlot slot,
            object instance,
            string phase,
            Exception exception
        )
        {
            PluginActivationInvocationResult failure =
                PluginActivationInvocationResult.Failed(
                    instance,
                    phase,
                    exception
                );
            slot.RetainActivationResult(failure);
            return failure;
        }

        private static object CleanupFailedInstance(
            PluginObjectSlot slot,
            PluginBehaviour instance,
            Exception activationFailure
        )
        {
            try
            {
                instance.enabled = false;
                UnityEngine.Object.Destroy(instance);
                slot.ReleaseFailedInstance(instance);
                return null;
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "The failed plugin component could not be cleaned.",
                    activationFailure,
                    cleanupFailure
                );
            }
        }
    }
}
