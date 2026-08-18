using HarmonyLib;

using NightScene.GuestManagementUtility;


namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.GuestManagementUtility.NormalGuestsController))]
[AutoLog]
public partial class NormalGuestsControllerPatch
{
    /// <summary>
    /// 主机或客机用于推进 EatingDelay -> ContinueDecision
    /// </summary>
    /// <param name="__instance"></param>
    [HarmonyPatch(nameof(NormalGuestsController.PostEvaluation))]
    [HarmonyPostfix]
    public static void NormalGuest_PostEvaluation_Postfix(NormalGuestsController __instance)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        GuestFSM.OnPostEvaluation(__instance);
    }
}
