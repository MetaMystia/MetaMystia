using HarmonyLib;

using Common.UI;
using DayScene;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;
using SgrYuki.Utils;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(DayScene.SceneManager))]
[AutoLog]
public partial class DaySceneManagerPatch
{
    [HarmonyPatch(nameof(SceneManager.Awake))]
    [HarmonyPrefix]
    public static void Awake_Prefix()
    {
        RunTimeSchedulerPatch.ResetFirstTrialGuest();
        GameFlow.OnSceneTransit(Scene.DayScene);
        PlayerManager.Local.ResetState();
    }

    [HarmonyPatch(nameof(SceneManager.Awake))]
    [HarmonyPostfix]
    public static void Awake_Postfix()
    {
        ResourceExManager.OnDaySceneAwake();
        PrepSceneManager.ClearPrepTable();

        if (GameSession.IsOnline)
        {
            PlayerProfile.SendProfile();
        }

        if (PatchRegistry.PatchedException != null)
        {
            var warningMessage = TextId.ModPatchFailure.Get();
            InGameConsole.LogError(warningMessage);
        }
    }


    public static void OnDayOver()
    {
        if (GameSession.IsRoomClient)
        {
            GuestInviteAction.Send(GameData.RunTime.Common.StatusTracker.Instance?.InvitedGuests.ToManagedList());
        }
        Panel.CloseActivePanelsBeforeSceneTransit();
        OnDayOver_ReversePatch(SceneManager.Instance);
    }

    [HarmonyPatch(nameof(SceneManager.OnFirstEnterDaySceneFinish))]
    [HarmonyPostfix]
    public static void OnFirstEnterDaySceneFinish_Postfix(SceneManager __instance)
    {
        if (__instance == SceneManager.Instance) GameFlow.OnCharactersReady(Scene.DayScene);
    }

    [HarmonyPatch(nameof(SceneManager.OnDayOver))]
    [HarmonyPrefix]
    public static bool OnDayOver_Prefix()
    {
        Log.InfoCaller($"called");

        if (!GameSession.IsInRoom)
        {
            PlayerManager.LocalIsDayOver = true;
            return RunOriginal;
        }

        if (DayDestinationManager.ReplayingBusiness)
        {
            OnDayOver();
            return SkipOriginal;
        }
        DayDestinationManager.Submit(DayDestination.Business, OnDayOver);
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(SceneManager.OnDayOver))]
    [HarmonyReversePatch]
    private static void OnDayOver_ReversePatch(SceneManager __instance)
    { }

    [HarmonyPatch(nameof(SceneManager.SwapMap))]
    [HarmonyPrefix]
    public static bool SwapMap_Prefix(SceneManager __instance, string targetMapLabel, string targetMarkerName, int travelCount, ref Il2CppSystem.Action onSwapFinish)
    {
        Log.InfoCaller($"targetMapLabel {targetMapLabel}, targetMarkerName {targetMarkerName}");

        var refreshAllDayNpcs = SpecialGuestRegistry.RefreshAllDayNpcs; // TODO: 以更优雅的方式实现 Day NPC 刷新
        onSwapFinish += refreshAllDayNpcs;
        onSwapFinish += (System.Action)(() =>
        {
            if (__instance != SceneManager.Instance || GameFlow.LocalScene != Scene.DayScene || !GameFlow.CharactersReady) return;
            PlayerManager.RefreshCharacters();
            PlayerProfile.SendMotion();
        });

        return RunOriginal;
    }
}
