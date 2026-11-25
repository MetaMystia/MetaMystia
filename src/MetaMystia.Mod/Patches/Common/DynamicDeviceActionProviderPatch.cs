using HarmonyLib;

using DEYU.InputHelper;

using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DEYU.InputHelper.DynamicDeviceActionProvider))]
public class DynamicDeviceActionProviderPatch
{
    // J/K 会切到键盘模式并隐藏游戏光标，打字时不切换。
    [HarmonyPatch(nameof(DynamicDeviceActionProvider.UpdateCurrentMouseState))]
    [HarmonyPrefix]
    public static bool UpdateCurrentMouseState_Prefix(bool isMouse)
    {
        return InGameConsole.IsOpen && !isMouse ? SkipOriginal : RunOriginal;
    }
}
