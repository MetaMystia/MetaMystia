using HarmonyLib;
using UnityEngine.InputSystem;

using Common.UI;
using DEYU.AdpUISystem.ComponentCollection;

using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DEYU.AdpUISystem.ComponentCollection.AdpPlayerInputWrapper))]
[AutoLog]
public partial class AdpPlayerInputWrapperPatch
{
    // 控制台打开时只放行松开和指针位置，按住的键在关闭后不会残留。
    [HarmonyPatch(nameof(AdpPlayerInputWrapper.OnActionTriggered))]
    [HarmonyPrefix]
    public static bool OnActionTriggered_Prefix(InputAction.CallbackContext callbackContext)
    {
        if (!InGameConsole.IsOpen || callbackContext.phase == InputActionPhase.Canceled
            || callbackContext.action.name == InputNames.Action.Shared.Point)
            return RunOriginal;
        return SkipOriginal;
    }
}
