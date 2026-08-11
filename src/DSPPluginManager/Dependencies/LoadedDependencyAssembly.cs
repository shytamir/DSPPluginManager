using System;
using System.Reflection;

namespace DSPPluginManager.Dependencies
{
    internal sealed class LoadedDependencyAssembly
    {
        internal LoadedDependencyAssembly(
            Assembly assembly,
            AssemblyName identity,
            string location
        )
        {
            Assembly = assembly;
            Identity = identity;
            Location = location;
        }

        internal Assembly Assembly { get; }

        internal AssemblyName Identity { get; }

        internal string Location { get; }

        internal static LoadedDependencyAssembly FromAssembly(Assembly assembly)
        {
            string location;
            try
            {
                location = assembly.IsDynamic ? null : assembly.Location;
            }
            catch (Exception)
            {
                location = null;
            }

            return new LoadedDependencyAssembly(
                assembly,
                assembly.GetName(),
                location
            );
        }
    }
}
