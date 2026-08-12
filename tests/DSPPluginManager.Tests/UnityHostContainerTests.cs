using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace DSPPluginManager.Tests
{
    internal static class UnityHostContainerTests
    {
        private const string RootName = "DSPPluginManager";

        internal static void Run(
            string productPath,
            string unityHostPath,
            string facadePath
        )
        {
            Assembly product = FindLoadedAssembly(productPath);
            TestAssert.True(
                !product.GetReferencedAssemblies().Any(reference =>
                    reference.Name == "UnityEngine.CoreModule" ||
                    reference.Name == "DSPPluginManager.UnityHost"
                ),
                "The pre-Unity entry assembly gained an eager Unity dependency."
            );

            Assembly facade = Assembly.LoadFrom(Path.GetFullPath(facadePath));
            Type runtime = facade.GetType("UnityEngine.FacadeRuntime", true);
            InvokeStatic(runtime, "Reset");

            Assembly unityHost = Assembly.LoadFrom(
                Path.GetFullPath(unityHostPath)
            );
            string[] references = unityHost.GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .ToArray();
            TestAssert.True(
                references.Contains("DSPPluginManager") &&
                references.Contains("UnityEngine.CoreModule") &&
                !references.Contains("DSPPluginManager.Contracts") &&
                !references.Contains("DSPPluginManager.RM09Consumer"),
                "The late Unity host has an invalid assembly boundary."
            );

            Type entrypoint = unityHost.GetType(
                "DSPPluginManager.UnityHost.UnityHostEntrypoint",
                true
            );
            MethodInfo ensureCreated = entrypoint.GetMethod("EnsureCreated");
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            ensureCreated.Invoke(null, new object[] { mainThreadId });
            object firstRoot = InvokeStatic(runtime, "FindRoot", RootName);
            TestAssert.True(firstRoot != null,
                "The persistent Unity root was not created.");
            TestAssert.Equal(
                1,
                InvokeStatic(runtime, "CountRoots", RootName),
                "initial Unity host root count"
            );
            TestAssert.Equal(
                true,
                InvokeStatic(runtime, "IsPersistent", firstRoot),
                "Unity host persistence marker"
            );
            TestAssert.Equal(
                true,
                firstRoot.GetType().GetProperty("activeSelf")
                    .GetValue(firstRoot, null),
                "Unity host active state"
            );

            ensureCreated.Invoke(null, new object[] { mainThreadId });
            TestAssert.True(
                object.ReferenceEquals(
                    firstRoot,
                    InvokeStatic(runtime, "FindRoot", RootName)
                ),
                "Repeated handoff replaced the retained Unity root."
            );
            TestAssert.Equal(
                1,
                InvokeStatic(runtime, "CountRoots", RootName),
                "repeated-handoff Unity host root count"
            );

            object container = entrypoint.GetProperty(
                "Current",
                BindingFlags.Static | BindingFlags.NonPublic
            ).GetValue(null, null);
            MethodInfo getPluginObject = container.GetType().GetMethod(
                "GetOrCreatePluginObject",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            object firstSlot = getPluginObject.Invoke(
                container,
                new object[] { "Fixture.Plugin" }
            );
            object repeatedSlot = getPluginObject.Invoke(
                container,
                new object[] { "fixture.plugin" }
            );
            object secondSlot = getPluginObject.Invoke(
                container,
                new object[] { "fixture.other" }
            );
            TestAssert.True(object.ReferenceEquals(firstSlot, repeatedSlot),
                "One plugin identity received duplicate Unity objects.");
            TestAssert.True(!object.ReferenceEquals(firstSlot, secondSlot),
                "Separate plugin identities shared a Unity object.");
            TestAssert.Equal(
                2,
                container.GetType().GetProperty(
                    "PluginObjectCount",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ).GetValue(container, null),
                "owned plugin object count"
            );

            object firstChild = SlotGameObject(firstSlot);
            object secondChild = SlotGameObject(secondSlot);
            foreach (object child in new[] { firstChild, secondChild })
            {
                TestAssert.True(
                    object.ReferenceEquals(
                        firstRoot,
                        InvokeStatic(runtime, "ParentOf", child)
                    ),
                    "Plugin object is not owned beneath the retained root."
                );
                TestAssert.Equal(
                    0,
                    InvokeStatic(runtime, "AttachedComponentCount", child),
                    "plugin component count before activation"
                );
            }

            object transient = Activator.CreateInstance(
                facade.GetType("UnityEngine.GameObject", true),
                new object[] { "RepresentativeSceneObject" }
            );
            InvokeStatic(runtime, "LoadRepresentativeScene");
            TestAssert.True(
                object.ReferenceEquals(
                    firstRoot,
                    InvokeStatic(runtime, "FindRoot", RootName)
                ) &&
                (bool)InvokeStatic(runtime, "Contains", firstChild) &&
                (bool)InvokeStatic(runtime, "Contains", secondChild),
                "The persistent host hierarchy did not survive a scene change."
            );
            TestAssert.Equal(
                false,
                InvokeStatic(runtime, "Contains", transient),
                "Representative transient scene object survived unexpectedly."
            );

            Exception backgroundFailure = null;
            Thread background = new Thread(() =>
            {
                try
                {
                    ensureCreated.Invoke(null, new object[] { mainThreadId });
                }
                catch (TargetInvocationException exception)
                {
                    backgroundFailure = exception.InnerException;
                }
            });
            background.Start();
            background.Join();
            TestAssert.True(backgroundFailure is InvalidOperationException,
                "Background-thread Unity host access was accepted.");
            TestAssert.Equal(
                1,
                InvokeStatic(runtime, "CountRoots", RootName),
                "post-failure Unity host root count"
            );
            TestAssert.True(
                !AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                    assembly.GetName().Name == "DSPPluginManager.RM09Consumer"
                ),
                "RM-18 runtime-loaded a candidate fixture assembly."
            );
        }

        private static object SlotGameObject(object slot)
        {
            return slot.GetType().GetProperty(
                "GameObject",
                BindingFlags.Instance | BindingFlags.NonPublic
            ).GetValue(slot, null);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments
        )
        {
            return type.GetMethod(methodName).Invoke(null, arguments);
        }

        private static Assembly FindLoadedAssembly(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return AppDomain.CurrentDomain.GetAssemblies().Single(assembly =>
                !assembly.IsDynamic &&
                string.Equals(
                    assembly.Location,
                    fullPath,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }
    }
}
