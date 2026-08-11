using System;

namespace DSPPluginManager.Dependencies
{
    internal static class ReservedDependencyCatalog
    {
        private static readonly ReservedDependencySpec[] Specifications =
        {
            new ReservedDependencySpec(
                "0Harmony",
                "0Harmony.dll",
                "2.5.5.0",
                "neutral",
                "null",
                "7BD2BD6F87C1758047DEF40F2F0F024C877456CE7C01D68031358EE0C615D850",
                "2.5.5.0"
            ),
            new ReservedDependencySpec(
                "MonoMod.RuntimeDetour",
                "MonoMod.RuntimeDetour.dll",
                "21.9.19.1",
                "neutral",
                "null",
                "281BFB29C5E9CC4CB98B81E7AFC0171AE12891A7B7370A98568D9E2A3060DB50",
                "21.8.19.1",
                "21.9.19.1"
            ),
            new ReservedDependencySpec(
                "MonoMod.Utils",
                "MonoMod.Utils.dll",
                "21.9.19.1",
                "neutral",
                "null",
                "4CC34A5C4278D78CE3F516BB3B43C9A5ED3509672DBBA932E036746A9360A570",
                "21.8.19.1",
                "21.9.19.1"
            ),
            new ReservedDependencySpec(
                "Mono.Cecil",
                "Mono.Cecil.dll",
                "0.10.4.0",
                "neutral",
                "50cebf1cceb9d05e",
                "7AE470288FFF4A402899C254D0A76CEFEF55877F5C54F96E83C797CC5BB6E2F6",
                "0.10.4.0"
            )
        };

        internal static ReservedDependencySpec Find(string name)
        {
            foreach (ReservedDependencySpec specification in Specifications)
            {
                if (string.Equals(
                        specification.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase
                    ))
                {
                    return specification;
                }
            }

            return null;
        }

        internal static ReservedDependencySpec[] GetAll()
        {
            return (ReservedDependencySpec[])Specifications.Clone();
        }
    }
}
