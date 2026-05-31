using HarmonyLib;

using Common.UI;
using DayScene;

using MetaMystia.Network;
using MetaMystia.UI;
using SgrYuki.Utils;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(DayScene.SceneManager))]
[AutoLog]
public partial class DaySceneManagerPatch
{
    [HarmonyPatch(nameof(SceneManager.Awake))]
    [HarmonyPostfix]
    public static void Awake_Postfix()
    {
        MpManager.OnSceneTransit(Scene.DayScene);
        PlayerManager.Local.ResetState();
        PlayerManager.InitLocalSkin();
        PlayerManager.SpawnPeers();
        ResourceExManager.OnDaySceneAwake();
        PrepSceneManager.ClearPrepTable();

        if (MpManager.IsConnected)
        {
            SkinChangeAction.Send(PlayerManager.Local.Skin);
        }

        if (PatchRegistry.PatchedException != null)
        {
            var warningMessage = TextId.ModPatchFailure.Get();
            InGameConsole.LogError(warningMessage);
        }


        // if (MpManager.IsConnected)
        // {
        //     CommandScheduler.EnqueueKey(
        //         key: MpManager.PeerGetCharacterUnitNotNullCommand,
        //         executeWhen: () => PlayerManager.Peer?.GetCharacterUnit() != null,
        //         execute: () =>
        //         {
        //             if (!MpManager.InStory)
        //             {
        //                 PlayerManager.EnablePeerCollision(true);
        //             }
        //             PlayerManager.Peer?.GetCharacterComponent()?.UpdateIcon(false);
        //         },
        //         timeoutSeconds: 120
        //     );
        // }
    }


    public static void OnDayOver()
    {
        if (MpManager.IsConnectedClient)
        {
            GuestInviteAction.Send(GameData.RunTime.Common.StatusTracker.Instance?.InvitedGuests.ToManagedList());
        }
        Panel.CloseActivePanelsBeforeSceneTransit();
        OnDayOver_ReversePatch(SceneManager.Instance);
    }

    [HarmonyPatch(nameof(SceneManager.OnDayOver))]
    [HarmonyPrefix]
    public static bool OnDayOver_Prefix()
    {
        Log.InfoCaller($"called");

        PlayerManager.LocalIsDayOver = true;

        if (!MpManager.IsConnected)
        {
            return RunOriginal;
        }

        InGameConsole.ShowPassive(TextId.MystiaReadyForWork.Get());
        ReadyAction.Send(ReadyType.DayOver);
        MpManager.DayOver();
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

        var refreshAllDayNpcs = ResourceExManager.RefreshAllDayNpcs; // TODO: 以更优雅的方式实现 Day NPC 刷新
        onSwapFinish += refreshAllDayNpcs;

        return RunOriginal;
    }
}
