using HarmonyLib;

using DayScene.Input;

using MetaMystia.Multiplayer;
using MetaMystia.ResourceEx.Registries;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.Input.DayScenePlayerInputGenerator))]
[AutoLog]
public partial class DayScenePlayerInputPatch
{
    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.UpdateCharacter))]
    [HarmonyPostfix]
    public static void UpdateCharacter_Postfix() => DecorationRegistry.ApplyDayEffects();

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintPerformed))]
    [HarmonyPrefix]
    public static void OnSprintPerformed_Prefix()
    {
        PlayerManager.LocalIsSprinting = true;
        PlayerProfile.SendMotion();
    }

    [HarmonyPatch(nameof(DayScenePlayerInputGenerator.OnSprintCanceled))]
    [HarmonyPrefix]
    [IcfFolded("与 OnPanelWillOpenCallback 折叠，两者都只设置 Sprint = false，打开面板时同步停止疾跑同样正确")]
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
