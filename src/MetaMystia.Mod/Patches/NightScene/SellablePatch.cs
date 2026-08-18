using HarmonyLib;

using GameData.Core.Collections;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Core.Collections.Sellable))]
[AutoLog]
public partial class SellablePatch
{
    [HarmonyPatch(nameof(Sellable.GetPopTag))]
    [HarmonyPrefix]
    public static bool GetPopTag_Prefix(Il2CppSystem.Collections.Generic.IEnumerable<int> sourceTag, ref Il2CppSystem.Collections.Generic.IEnumerable<int> __result)
    {
        if (MpManager.IsConnected)
        {
            __result = sourceTag;
            return SkipOriginal;
        }
        return RunOriginal;
    }
}
