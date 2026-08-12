using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using DSPPluginManager.Hosting;

namespace DSPPluginManager.ContractTests
{
    internal static class PluginWritableRootContractTests
    {
        internal static void Run(string contractPath, string fixturePath)
        {
            string root = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "rm17-writable-root-fixtures"
            );
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
            Directory.CreateDirectory(root);

            try
            {
                string parent = Path.Combine(root, "explicit-parent");
                string mirrorRoot = PluginWritableRootPath.Create(
                    parent,
                    "com.shytamir.dspmirrorblueprint"
                );
                string guideRoot = PluginWritableRootPath.Create(
                    parent,
                    "local.dsp.progressionstatusexporter"
                );
                TestAssert.True(
                    !string.Equals(
                        mirrorRoot,
                        guideRoot,
                        StringComparison.OrdinalIgnoreCase
                    ),
                    "Separate plugin identities shared a writable root."
                );

                Assembly contract = FindLoadedAssembly(contractPath);
                Assembly fixture = Assembly.LoadFrom(
                    Path.GetFullPath(fixturePath)
                );
                VerifyProvisionedProperty(contract, fixture, mirrorRoot, root);
                VerifyConsumerRoundTrips(
                    fixture,
                    mirrorRoot,
                    guideRoot
                );
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static void VerifyProvisionedProperty(
            Assembly contract,
            Assembly fixture,
            string root,
            string workingRoot
        )
        {
            Type baseType = contract.GetType(
                "DSPPluginManager.Contracts.PluginBehaviour",
                true
            );
            Type pluginType = fixture.GetType(
                "DSPPluginManager.RM09Consumer.MirrorShapedPlugin",
                true
            );
            object plugin = FormatterServices.GetUninitializedObject(pluginType);
            MethodInfo initialize = baseType.GetMethod(
                "InitializeWritableRoot",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            initialize.Invoke(plugin, new object[] { Path.Combine(root, ".") });

            PropertyInfo property = baseType.GetProperty("WritableRoot");
            string first = (string)property.GetValue(plugin, null);
            string originalDirectory = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = workingRoot;
                TestAssert.Equal(
                    first,
                    property.GetValue(plugin, null),
                    "writable-root working-directory independence"
                );
            }
            finally
            {
                Environment.CurrentDirectory = originalDirectory;
            }
            TestAssert.Equal(root, first, "provisioned writable root");

            try
            {
                initialize.Invoke(plugin, new object[] { root });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is InvalidOperationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Plugin writable-root replacement was accepted."
            );
        }

        private static void VerifyConsumerRoundTrips(
            Assembly fixture,
            string mirrorRoot,
            string guideRoot
        )
        {
            Type helper = fixture.GetType(
                "DSPPluginManager.RM09Consumer.MirrorOutputHelper",
                true
            );
            MethodInfo write = helper.GetMethod(
                "WriteSnapshot",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            string mirrorText = "Mirror geometry: Δ=1";
            string guideText = "Guide status: completed";
            string mirrorPath = (string)write.Invoke(
                null,
                new object[] { mirrorRoot, mirrorText }
            );
            string guidePath = (string)write.Invoke(
                null,
                new object[] { guideRoot, guideText }
            );

            TestAssert.True(
                !string.Equals(
                    mirrorPath,
                    guidePath,
                    StringComparison.OrdinalIgnoreCase
                ),
                "Consumer output paths collided."
            );
            Encoding utf8 = new UTF8Encoding(false, true);
            TestAssert.Equal(
                mirrorText,
                utf8.GetString(File.ReadAllBytes(mirrorPath)),
                "Mirror UTF-8 round trip"
            );
            TestAssert.Equal(
                guideText,
                utf8.GetString(File.ReadAllBytes(guidePath)),
                "Guide UTF-8 round trip"
            );
            byte[] mirrorBytes = File.ReadAllBytes(mirrorPath);
            TestAssert.True(
                mirrorBytes.Length < 256 &&
                !(mirrorBytes.Length >= 3 &&
                  mirrorBytes[0] == 0xef &&
                  mirrorBytes[1] == 0xbb &&
                  mirrorBytes[2] == 0xbf),
                "Consumer fixture did not write bounded BOM-free UTF-8."
            );
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
