using HarmonyLib;

using NightScene.CookingUtility;

using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.CookingUtility.CookSystemManager))]
[AutoLog]
public static class CookSystemManagerPatch
{
    [HarmonyPatch(nameof(CookSystemManager.CallCooker))]
    [HarmonyPrefix]
    public static bool CallCooker_Prefix()
    {
        if (!BusinessStart.IsWaitingForStart) return RunOriginal;
        // 在打开选材面板或改变厨具状态前阻止交互。
        InGameConsole.ShowPassive(TextId.BusinessStartWaiting.Get());
        return SkipOriginal;
    }
}
