using System;
using UnityEngine;

namespace DSPPluginManager.Contracts
{
    public abstract class PluginBehaviour : MonoBehaviour
    {
        private PluginLogger logger;

        public PluginLogger Logger
        {
            get
            {
                if (logger == null)
                {
                    throw new InvalidOperationException(
                        "The host has not prepared the plugin logger."
                    );
                }

                return logger;
            }
        }

        internal void InitializeLogger(PluginLogger value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            if (logger != null)
            {
                throw new InvalidOperationException(
                    "The plugin logger has already been prepared."
                );
            }

            logger = value;
        }
    }
}
