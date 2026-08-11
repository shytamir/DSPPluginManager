using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DSPPluginManager.RM05CecilHandoff
{
    public static class CecilHandoffInstaller
    {
        public static string Install(
            string managedDirectory,
            string callbackAssemblyPath
        )
        {
            const string coreName = "UnityEngine.CoreModule";
            if (AppDomain.CurrentDomain.GetAssemblies().Any(
                assembly => string.Equals(
                    assembly.GetName().Name,
                    coreName,
                    StringComparison.OrdinalIgnoreCase
                )
            ))
            {
                throw new InvalidOperationException(
                    coreName + " was loaded before the Cecil handoff probe."
                );
            }

            string corePath = Path.Combine(
                Path.GetFullPath(managedDirectory),
                coreName + ".dll"
            );
            if (!File.Exists(corePath))
            {
                throw new FileNotFoundException(
                    "The Unity core assembly was not found.",
                    corePath
                );
            }
            if (!File.Exists(callbackAssemblyPath))
            {
                throw new FileNotFoundException(
                    "The RM-05 callback assembly was not found.",
                    callbackAssemblyPath
                );
            }

            using (AssemblyDefinition core = AssemblyDefinition.ReadAssembly(
                corePath,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            using (AssemblyDefinition callback =
                AssemblyDefinition.ReadAssembly(
                    callbackAssemblyPath,
                    new ReaderParameters
                    {
                        InMemory = true,
                        ReadSymbols = false
                    }
                ))
            {
                TypeDefinition application = core.MainModule.GetType(
                    "UnityEngine.Application"
                );
                if (application == null)
                {
                    throw new InvalidOperationException(
                        "UnityEngine.Application was not found."
                    );
                }
                MethodDefinition constructor = application.Methods.SingleOrDefault(
                    method => method.IsConstructor && method.IsStatic
                );
                if (constructor == null || !constructor.HasBody ||
                    constructor.Body.Instructions.Count == 0)
                {
                    throw new InvalidOperationException(
                        "UnityEngine.Application static constructor is unavailable."
                    );
                }

                TypeDefinition callbacks = callback.MainModule.GetType(
                    "DSPPluginManager.RM05Callback.ProbeCallbacks"
                );
                MethodDefinition callbackMethod = callbacks == null
                    ? null
                    : callbacks.Methods.SingleOrDefault(
                        method => method.Name == "CecilHandoff" &&
                            method.IsStatic &&
                            method.Parameters.Count == 0
                    );
                if (callbackMethod == null)
                {
                    throw new InvalidOperationException(
                        "The Cecil callback method was not found."
                    );
                }

                MethodReference imported = core.MainModule.ImportReference(
                    callbackMethod
                );
                ILProcessor processor = constructor.Body.GetILProcessor();
                processor.InsertBefore(
                    constructor.Body.Instructions[0],
                    processor.Create(OpCodes.Call, imported)
                );

                using (MemoryStream patched = new MemoryStream())
                {
                    core.Write(patched);
                    byte[] image = patched.ToArray();
                    Assembly loaded = Assembly.Load(image);
                    return "target=" + corePath +
                        "; callback=" + imported.FullName +
                        "; imageBytes=" + image.Length +
                        "; loaded=" + loaded.FullName +
                        "; onDiskWrite=false";
                }
            }
        }
    }
}
