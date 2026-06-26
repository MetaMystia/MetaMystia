using HarmonyLib;

using DayScene;
using DayScene.Interactables;
using DayScene.Input;

using MetaMystia.Network;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.Input.DayScenePlayerInputGenerator))]
[AutoLog]
public partial class DayScenePlayerInputPatch
{
    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.UpdateCharacter), new[] { typeof(DaySceneMap), typeof(SpawnMarker) })]
    [HarmonyPostfix]
    public static void UpdateCharacter_Postfix()
    {
        PlayerManager.SpawnPeersForCurrentScene();
    }

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintPerformed))]
    [HarmonyPrefix]
    public static bool OnSprintPerformed_Prefix()
    {
        if (InGameConsole.IsOpen)
        {
            return SkipOriginal;
        }
        PlayerManager.LocalIsSprinting = true;
        MoveSyncBehavior.Send();
        return RunOriginal;
    }

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintCanceled))]
    [HarmonyPrefix]
    public static void OnSprintCanceled_Prefix()
    {
        PlayerManager.LocalIsSprinting = false;
        MoveSyncBehavior.Send();
    }

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.TryInteract))]
    [HarmonyPrefix]
    public static bool TryInteract_Prefix()
    {
        if (InGameConsole.IsOpen)
        {
            Log.Warning($"Console is open, skipping interaction");
            return SkipOriginal;
        }
        if (!MpManager.IsRoomConnected)
        {
            return RunOriginal;
        }
        if (PlayerManager.LocalIsDayOver)
        {
            Log.Warning($"Day is over, skipping interaction");
            return SkipOriginal;
        }
        return RunOriginal;
    }
}
