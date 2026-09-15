using HarmonyLib;

using NightScene;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.NightSceneDirector))]
[AutoLog]
public static partial class NightSceneDirectorPatch
{
    private const string YuyukoGuestLabel = "Yuyuko";

    [HarmonyPatch(nameof(NightSceneDirector.SpawnManualControlledSpecialGuest))]
    [HarmonyPostfix]
    public static void SpawnManualControlledSpecialGuest_Postfix(NightSceneDirector __instance, string label)
    {
        if (label == YuyukoGuestLabel && GameSession.HasPeers && PrepSceneManager.IsYuyukoChallenge)
            YuyukoGuestSync.Capture(__instance.GetControlled(label));
    }

    [HarmonyPatch(nameof(NightSceneDirector.GetControlled))]
    [HarmonyPostfix]
    public static void GetControlled_Postfix(string guestLabel, GuestGroupController __result)
    {
        if (guestLabel == YuyukoGuestLabel) YuyukoGuestSync.Capture(__result);
    }
}
