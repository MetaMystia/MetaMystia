using HarmonyLib;

using DayScene.Input;

using MetaMystia.Multiplayer;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.Input.DayScenePlayerInputGenerator))]
[AutoLog]
public partial class DayScenePlayerInputPatch
{
    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.UpdateCharacter))]
    [HarmonyPostfix]
    public static void UpdateCharacter_Postfix() => DecorationRegistry.RefreshDayEffects();

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintPerformed))]
    [HarmonyPrefix]
    public static bool OnSprintPerformed_Prefix()
    {
        if (InGameConsole.IsOpen)
        {
            return SkipOriginal;
        }
        PlayerManager.LocalIsSprinting = true;
        PlayerProfile.SendMotion();
        return RunOriginal;
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
        if (InGameConsole.IsOpen)
        {
            Log.Debug($"Console is open, skipping interaction");
            return SkipOriginal;
        }
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
