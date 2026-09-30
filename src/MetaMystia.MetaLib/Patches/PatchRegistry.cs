using HarmonyLib;

namespace MetaMystia.MetaLib.Patches;

internal static class PatchRegistry
{
    internal static void Apply(Harmony harmony) => harmony.CreateClassProcessor(typeof(SaveManagementPatch)).Patch();
}
