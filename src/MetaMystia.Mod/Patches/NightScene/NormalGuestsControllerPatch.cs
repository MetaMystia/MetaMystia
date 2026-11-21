using HarmonyLib;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.GuestManagementUtility.NormalGuestsController))]
[AutoLog]
public partial class NormalGuestsControllerPatch
{
    [HarmonyPatch(nameof(NormalGuestsController.PostEvaluation))]
    [HarmonyPrefix]
    public static void PostEvaluation_Prefix(NormalGuestsController __instance,
        ref GuestGroupController.EvaluationResult evaluationType) =>
        EvaluateOrderMessage.BeforePostEvaluation(__instance, ref evaluationType);
}
