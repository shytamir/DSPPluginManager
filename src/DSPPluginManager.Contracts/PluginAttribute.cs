using System;

namespace DSPPluginManager.Contracts
{
    [AttributeUsage(
        AttributeTargets.Class,
        AllowMultiple = false,
        Inherited = false
    )]
    public sealed class PluginAttribute : Attribute
    {
        public PluginAttribute(
            string identifier,
            string displayName,
            string version
        )
        {
            Identifier = identifier;
            DisplayName = displayName;
            Version = version;
        }

        public string Identifier { get; }

        public string DisplayName { get; }

        public string Version { get; }
    }
}
