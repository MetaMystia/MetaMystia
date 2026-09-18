using HarmonyLib;

using NightScene;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.NightSceneDirector))]
[AutoLog]
public static partial class NightSceneDirectorPatch
{
    public static bool ReturningFromTrial { get; private set; }

    // TryLeaveSession 同步调用 LoadScene；只在该调用范围允许最终试炼正常返回。
    [HarmonyPatch(nameof(NightSceneDirector.TryLeaveSession))]
    [HarmonyPrefix]
    public static void TryLeaveSession_Prefix() => ReturningFromTrial = GameFlow.IsFinalTrial;

    [HarmonyPatch(nameof(NightSceneDirector.TryLeaveSession))]
    [HarmonyPostfix]
    public static void TryLeaveSession_Postfix() => ReturningFromTrial = false;

    private const string YuyukoGuestLabel = "Yuyuko";

    [HarmonyPatch(nameof(NightSceneDirector.SpawnManualControlledSpecialGuest))]
    [HarmonyPostfix]
    public static void SpawnManualControlledSpecialGuest_Postfix(NightSceneDirector __instance, string label)
    {
        if (label == YuyukoGuestLabel && GameSession.HasRoomPeers && PrepSceneManager.IsYuyukoChallenge)
            YuyukoGuestSync.Capture(__instance.GetControlled(label));
    }

    [HarmonyPatch(nameof(NightSceneDirector.GetControlled))]
    [HarmonyPostfix]
    public static void GetControlled_Postfix(string guestLabel, GuestGroupController __result)
    {
        if (guestLabel == YuyukoGuestLabel) YuyukoGuestSync.Capture(__result);
    }
}
