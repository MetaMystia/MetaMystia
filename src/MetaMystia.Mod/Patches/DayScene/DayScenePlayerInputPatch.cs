using HarmonyLib;

using DayScene.Input;

using MetaMystia.Multiplayer;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.Input.DayScenePlayerInputGenerator))]
[AutoLog]
public partial class DayScenePlayerInputPatch
{
    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintPerformed))]
    [HarmonyPrefix]
    public static void OnSprintPerformed_Prefix()
    {
        PlayerManager.LocalIsSprinting = true;
        PlayerProfile.SendMotion();
    }

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintCanceled))]
    [HarmonyPrefix]
    public static void OnSprintCanceled_Prefix()
    {
        PlayerManager.LocalIsSprinting = false;
        PlayerProfile.SendMotion();
    }

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.TryInteract))]
    [HarmonyPrefix]
    public static bool TryInteract_Prefix()
    {
        if (!GameSession.HasRoomPeers)
        {
            return RunOriginal;
        }
        if (PlayerManager.LocalIsDayOver || DayDestinationManager.IsStoryLocked)
        {
            Log.Warning($"Day is over, skipping interaction");
            return SkipOriginal;
        }
        return RunOriginal;
    }
}
