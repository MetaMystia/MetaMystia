using HarmonyLib;

using GameData.Profile;

using MetaMystia.Network;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(GameData.Profile.MystiaQTEBuffReward))]
[AutoLog]
public partial class MystiaQTEBuffRewardPatch
{
    [HarmonyPatch(nameof(MystiaQTEBuffReward.Player_ThrowDeliver))]
    [HarmonyPrefix]
    public static void Player_ThrowDeliver_Prefix(MystiaQTEBuffReward __instance)
    {
        Log.Debug($"Player_ThrowDeliver Prefix");
        if (QTERewardManagerPatch.BuffLocalTrigger)
        {
            BuffAction.Send(QTEBuff.ThrowDeliver);
        }
    }

    [HarmonyPatch(nameof(MystiaQTEBuffReward.Player_InstantEvaluation))]
    [HarmonyPrefix]
    public static void Player_InstantEvaluation_Prefix(MystiaQTEBuffReward __instance)
    {
        Log.Debug($"Player_InstantEvaluation Prefix");
        if (QTERewardManagerPatch.BuffLocalTrigger)
        {
            BuffAction.Send(QTEBuff.InstantEvaluation);
        }
    }

    [HarmonyPatch(nameof(MystiaQTEBuffReward.Player_PatientFreeze))]
    [HarmonyPrefix]
    public static void Player_PatientFreeze_Prefix(MystiaQTEBuffReward __instance)
    {
        Log.Debug($"Player_PatientFreeze Prefix");
        if (QTERewardManagerPatch.BuffLocalTrigger)
        {
            BuffAction.Send(QTEBuff.PatientFreeze);
        }
    }

    [HarmonyPatch(nameof(MystiaQTEBuffReward.Player_Fever))]
    [HarmonyPrefix]
    public static void Player_Fever_Prefix(MystiaQTEBuffReward __instance)
    {
        Log.Debug($"Player_Fever Prefix");
        if (QTERewardManagerPatch.BuffLocalTrigger)
        {
            BuffAction.Send(QTEBuff.Fever);
        }
    }

    [HarmonyPatch(nameof(MystiaQTEBuffReward.Player_Fever_Infinite))]
    [HarmonyPrefix]
    public static void Player_Fever_Infinite_Prefix(MystiaQTEBuffReward __instance)
    {
        Log.Debug($"Player_Fever_Infinite Prefix");
        if (QTERewardManagerPatch.BuffLocalTrigger)
        {
            BuffAction.Send(QTEBuff.Fever_Infinite);
        }
    }

}
