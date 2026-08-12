using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using DSPPluginManager.Contracts;
using HarmonyLib;

namespace DSPPluginManager.RM23HarmonyLifecycle
{
    [Plugin(
        "fixture.rm23.b-harmony-lifecycle",
        "RM-23 Harmony Lifecycle",
        "1.0.0"
    )]
    public sealed class HarmonyLifecyclePlugin : PluginBehaviour
    {
        private const string OwnedPatchId =
            "fixture.rm23.b-harmony-lifecycle";
        private const string ControlPatchId =
            "fixture.rm23.control-owner";

        private Harmony ownedHarmony;
        private Harmony controlHarmony;
        private MethodInfo target;
        private int activationCount;
        private int cleanupCount;
        private int activationThread;
        private int cleanupThread;
        private int patchedResult;
        private int ownedRemovalResult;
        private int finalResult;
        private bool ownedPatchAttributed;
        private bool controlPatchAttributed;
        private bool ownedPatchRemoved;
        private bool controlPatchPreserved;
        private bool allPatchesRemoved;
        private string activationClosure;
        private string cleanupClosure;
        private bool activationConfigurationAvailable;
        private bool cleanupConfigurationAvailable;

        public override void Activate()
        {
            activationCount++;
            activationThread = Thread.CurrentThread.ManagedThreadId;
            activationConfigurationAvailable = Config != null;
            target = AccessTools.Method(
                typeof(HarmonyPatchTarget),
                "Compute",
                new[] { typeof(int) }
            );
            MethodInfo ownedPostfix = AccessTools.Method(
                typeof(HarmonyLifecyclePlugin),
                "OwnedPostfix"
            );
            MethodInfo controlPostfix = AccessTools.Method(
                typeof(HarmonyLifecyclePlugin),
                "ControlPostfix"
            );
            if (target == null || ownedPostfix == null ||
                controlPostfix == null)
            {
                throw new MissingMethodException(
                    "The RM-23 patch target or postfix was not found."
                );
            }

            controlHarmony = new Harmony(ControlPatchId);
            controlHarmony.Patch(
                target,
                postfix: new HarmonyMethod(controlPostfix)
            );
            ownedHarmony = new Harmony(OwnedPatchId);
            ownedHarmony.Patch(
                target,
                postfix: new HarmonyMethod(ownedPostfix)
            );

            Patches patches = Harmony.GetPatchInfo(target);
            ownedPatchAttributed = HasOwner(patches, OwnedPatchId);
            controlPatchAttributed = HasOwner(patches, ControlPatchId);
            patchedResult = HarmonyPatchTarget.Compute(1);
            activationClosure = CaptureExactClosure();
            if (!ownedPatchAttributed || !controlPatchAttributed ||
                patchedResult != 112)
            {
                throw new InvalidOperationException(
                    "The attributable RM-23 postfix was not applied."
                );
            }
            Logger.Information(
                "RM-23 attributable Harmony postfix applied: owner=" +
                OwnedPatchId + " result=" + patchedResult + ". " +
                activationClosure
            );
        }

        public override void Deactivate()
        {
            cleanupCount++;
            cleanupThread = Thread.CurrentThread.ManagedThreadId;
            cleanupConfigurationAvailable = Config != null;
            cleanupClosure = CaptureExactClosure();
            try
            {
                ownedHarmony.UnpatchSelf();
                Patches remaining = Harmony.GetPatchInfo(target);
                ownedPatchRemoved = !HasOwner(remaining, OwnedPatchId);
                controlPatchPreserved = HasOwner(
                    remaining,
                    ControlPatchId
                );
                ownedRemovalResult = HarmonyPatchTarget.Compute(1);
                if (!ownedPatchRemoved || !controlPatchPreserved ||
                    ownedRemovalResult != 102)
                {
                    throw new InvalidOperationException(
                        "UnpatchSelf did not preserve the unrelated owner."
                    );
                }
            }
            finally
            {
                if (controlHarmony != null)
                {
                    controlHarmony.UnpatchSelf();
                }
            }

            Patches finalPatches = Harmony.GetPatchInfo(target);
            allPatchesRemoved = finalPatches == null ||
                !HasOwner(finalPatches, OwnedPatchId) &&
                !HasOwner(finalPatches, ControlPatchId);
            finalResult = HarmonyPatchTarget.Compute(1);
            WriteEvidence();
            if (!allPatchesRemoved || finalResult != 2)
            {
                throw new InvalidOperationException(
                    "The RM-23 patch target did not return to baseline."
                );
            }
            Logger.Information(
                "RM-23 Harmony cleanup verified: ownedRemoved=" +
                ownedPatchRemoved + " otherOwnerPreserved=" +
                controlPatchPreserved + " resultAfterOwnedRemoval=" +
                ownedRemovalResult + " finalResult=" + finalResult + ". " +
                cleanupClosure
            );
        }

        private void WriteEvidence()
        {
            List<string> lines = new List<string>
            {
                "activationCount=" + activationCount,
                "cleanupCount=" + cleanupCount,
                "activationThread=" + activationThread,
                "cleanupThread=" + cleanupThread,
                "activationConfigurationAvailable=" +
                    activationConfigurationAvailable,
                "cleanupConfigurationAvailable=" +
                    cleanupConfigurationAvailable,
                "patchedResult=" + patchedResult,
                "ownedRemovalResult=" + ownedRemovalResult,
                "finalResult=" + finalResult,
                "ownedPatchAttributed=" + ownedPatchAttributed,
                "controlPatchAttributed=" + controlPatchAttributed,
                "ownedPatchRemoved=" + ownedPatchRemoved,
                "controlPatchPreserved=" + controlPatchPreserved,
                "allPatchesRemoved=" + allPatchesRemoved
            };
            AppendClosure(lines, "activation", activationClosure);
            AppendClosure(lines, "cleanup", cleanupClosure);
            File.WriteAllLines(
                Path.Combine(WritableRoot, "RM23-HARMONY-EVIDENCE.log"),
                lines.ToArray()
            );
        }

        private static string CaptureExactClosure()
        {
            string[] expectedNames =
            {
                "0Harmony",
                "MonoMod.RuntimeDetour",
                "MonoMod.Utils",
                "Mono.Cecil"
            };
            Dictionary<string, string> expectedVersions =
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "0Harmony", "2.5.5.0" },
                    { "MonoMod.RuntimeDetour", "21.9.19.1" },
                    { "MonoMod.Utils", "21.9.19.1" },
                    { "Mono.Cecil", "0.10.4.0" }
                };
            List<string> parts = new List<string>();
            string closureDirectory = null;
            foreach (string name in expectedNames)
            {
                Assembly[] matches = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(candidate => string.Equals(
                        candidate.GetName().Name,
                        name,
                        StringComparison.Ordinal
                    )).ToArray();
                if (matches.Length != 1)
                {
                    throw new InvalidOperationException(
                        "Expected one loaded " + name + " assembly; found " +
                        matches.Length + "."
                    );
                }
                Assembly assembly = matches[0];
                string version = assembly.GetName().Version.ToString();
                string location = Path.GetFullPath(assembly.Location);
                string directory = Path.GetDirectoryName(location);
                if (version != expectedVersions[name] ||
                    closureDirectory != null && !string.Equals(
                        closureDirectory,
                        directory,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    throw new InvalidOperationException(
                        "The loaded " + name +
                        " assembly is outside one exact pinned closure: " +
                        assembly.FullName + " at '" + location + "'."
                    );
                }
                closureDirectory = directory;
                parts.Add(name + "|" + version + "|" + location);
            }
            return string.Join(";", parts.ToArray());
        }

        private static void AppendClosure(
            ICollection<string> lines,
            string phase,
            string closure
        )
        {
            foreach (string entry in closure.Split(';'))
            {
                string[] fields = entry.Split('|');
                lines.Add(phase + "." + fields[0] + ".version=" + fields[1]);
                lines.Add(phase + "." + fields[0] + ".path=" + fields[2]);
            }
        }

        private static bool HasOwner(Patches patches, string owner)
        {
            return patches != null && patches.Owners.Contains(owner);
        }

        private static void OwnedPostfix(ref int __result)
        {
            __result += 10;
        }

        private static void ControlPostfix(ref int __result)
        {
            __result += 100;
        }
    }

    internal static class HarmonyPatchTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int Compute(int value)
        {
            return value + 1;
        }
    }
}
