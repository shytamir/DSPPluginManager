using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DSPPluginManager.RM13CecilHandoff
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
                    coreName + " was loaded before the RM-13 handoff."
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
                MethodDefinition constructor = FindApplicationConstructor(core);
                MethodDefinition callbackMethod = FindCallback(callback);
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

        private static MethodDefinition FindApplicationConstructor(
            AssemblyDefinition core
        )
        {
            TypeDefinition application = core.MainModule.GetType(
                "UnityEngine.Application"
            );
            MethodDefinition constructor = application == null
                ? null
                : application.Methods.SingleOrDefault(
                    method => method.IsConstructor && method.IsStatic
                );
            if (constructor == null || !constructor.HasBody ||
                constructor.Body.Instructions.Count == 0)
            {
                throw new InvalidOperationException(
                    "UnityEngine.Application static constructor is unavailable."
                );
            }
            return constructor;
        }

        private static MethodDefinition FindCallback(AssemblyDefinition callback)
        {
            TypeDefinition type = callback.MainModule.GetType(
                "DSPPluginManager.RM13Callback.LifecycleProbe"
            );
            MethodDefinition method = type == null
                ? null
                : type.Methods.SingleOrDefault(candidate =>
                    candidate.Name == "Handoff" &&
                    candidate.IsPublic &&
                    candidate.IsStatic &&
                    candidate.Parameters.Count == 0 &&
                    candidate.ReturnType.FullName == "System.Void"
                );
            if (method == null)
            {
                throw new InvalidOperationException(
                    "The RM-13 lifecycle callback was not found."
                );
            }
            return method;
        }
    }
}
