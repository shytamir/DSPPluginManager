using System;
using System.Linq;
using System.Threading;
using DSPPluginManager.Contracts;
using UnityEngine;

namespace DSPPluginManager.UnityHost
{
    internal sealed class UnityShortcutPollingBridge
    {
        private static readonly object InstallSync = new object();
        private static readonly KeyCode[] KeyboardKeys =
            Enum.GetValues(typeof(KeyCode))
                .Cast<KeyCode>()
                .Where(key => key != KeyCode.None && key < KeyCode.Mouse0)
                .Distinct()
                .ToArray();

        private readonly int mainThreadId;
        private readonly Func<KeyCode, bool> getKeyDown;
        private readonly Func<KeyCode, bool> getKey;
        private static int installedThreadId;

        private UnityShortcutPollingBridge(
            int mainThreadId,
            Func<KeyCode, bool> getKeyDown,
            Func<KeyCode, bool> getKey
        )
        {
            this.mainThreadId = mainThreadId;
            this.getKeyDown = getKeyDown;
            this.getKey = getKey;
        }

        internal static void Install(int mainThreadId)
        {
            if (mainThreadId <= 0 ||
                Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                throw new InvalidOperationException(
                    "Shortcut polling must be installed on the admitted " +
                    "Unity main thread."
                );
            }

            lock (InstallSync)
            {
                if (installedThreadId != 0)
                {
                    if (installedThreadId != mainThreadId)
                    {
                        throw new InvalidOperationException(
                            "Shortcut polling is already installed for a " +
                            "different Unity main thread."
                        );
                    }
                    return;
                }

                UnityShortcutPollingBridge bridge =
                    new UnityShortcutPollingBridge(
                        mainThreadId,
                        Input.GetKeyDown,
                        Input.GetKey
                    );
                KeyboardShortcut.InitializePolling(bridge.Poll);
                installedThreadId = mainThreadId;
            }
        }

        private bool Poll(KeyboardShortcut shortcut)
        {
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
            {
                throw new InvalidOperationException(
                    "Shortcuts may only be polled from Unity's admitted " +
                    "main thread."
                );
            }

            KeyCode mainKey = shortcut.MainKey;
            if (!getKeyDown(mainKey))
            {
                return false;
            }

            foreach (KeyCode key in KeyboardKeys)
            {
                if (shortcut.ContainsHeldKey(key) && !getKey(key))
                {
                    return false;
                }
            }
            foreach (KeyCode key in KeyboardKeys)
            {
                if (key != mainKey &&
                    !shortcut.ContainsHeldKey(key) &&
                    getKey(key))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
