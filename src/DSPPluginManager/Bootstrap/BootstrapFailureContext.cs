namespace DSPPluginManager.Bootstrap
{
    internal sealed class BootstrapFailureContext
    {
        internal BootstrapFailureContext(
            string phase,
            string targetAssemblyPath,
            string executablePath,
            string managedDirectory,
            string hostRoot,
            string dependencyDirectory
        )
        {
            Phase = phase;
            TargetAssemblyPath = targetAssemblyPath;
            ExecutablePath = executablePath;
            ManagedDirectory = managedDirectory;
            HostRoot = hostRoot;
            DependencyDirectory = dependencyDirectory;
        }

        internal string Phase { get; }

        internal string TargetAssemblyPath { get; }

        internal string ExecutablePath { get; }

        internal string ManagedDirectory { get; }

        internal string HostRoot { get; }

        internal string DependencyDirectory { get; }
    }
}
