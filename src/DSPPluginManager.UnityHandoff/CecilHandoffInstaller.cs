using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace DSPPluginManager.UnityHandoff
{
    public static class CecilHandoffInstaller
    {
        public static void Install(
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
                    coreName + " loaded before the manager installed its handoff."
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
                    "The manager callback assembly was not found.",
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
                    Assembly.Load(patched.ToArray());
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
            TypeDefinition entrypoint = callback.MainModule.GetType(
                "DSPPluginManager.Bootstrap.DoorstopEntrypoint"
            );
            MethodDefinition method = entrypoint == null
                ? null
                : entrypoint.Methods.SingleOrDefault(
                    candidate =>
                        candidate.Name == "UnityMainThreadHandoff" &&
                        candidate.IsPublic &&
                        candidate.IsStatic &&
                        candidate.Parameters.Count == 0 &&
                        candidate.ReturnType.FullName == "System.Void"
                );
            if (method == null)
            {
                throw new InvalidOperationException(
                    "The manager Unity handoff callback was not found."
                );
            }
            return method;
        }
    }
}
