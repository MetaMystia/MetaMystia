using Mystia.Listeners;
using Mystia.Scenes;

using Common.UI;
using GameData.Profile;
using GameData.RunTime.Common;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;
using SgrYuki.Utils;

namespace MetaMystia.Listeners;

/// <summary>
/// 白天场景的对话、地图切换与结束白天。
/// <para>通知来自 <see cref="IDayListener"/>（游戏回调时机，作用域外）；任何需要
/// <see cref="IDaySceneServices"/> 的动作都在 <see cref="IDaySceneGameLoop"/> 的
/// <c>Setup</c>/<c>Update</c> 内执行，因此通知只登记「待办」，不直接调服务。</para>
/// </summary>
[AutoLog]
public sealed partial class DaySync : IDayListener, IDaySceneGameLoop
{
    /// <summary>结束白天的待办来源：原版直接结束，或多人协作结束（先同步邀请列表并关面板）。</summary>
    private enum PendingEnd
    {
        None,
        Vanilla,
        Cooperative,
    }

    private static PendingEnd s_pendingEnd;
    private static bool s_ending;

    public void Setup(IDaySceneServices services)
    {
        // 展示服务（标签、角色像素集）只在场景循环的服务窗口内可用，窗口由本类开合。
        ScenePresentation.Begin(services.Presentation);
        // 结束白天一律由模组决定：原版 OnDayOver 被拦下，改由 OnDayEnded 通知触发后续动作。
        services.Schedule.SetEndEnabled(false);
        ApplyInputGates(services);
    }

    public void Update(IDaySceneServices services, float delta)
    {
        ScenePresentation.Pump();
        services.Common.SetNightTransitionEnabled(!GameSession.HasRoomPeers);
        ApplyInputGates(services);
        RunPendingEnd(services);
    }

    public void Shutdown(IDaySceneServices services)
    {
        ScenePresentation.End();
        s_pendingEnd = PendingEnd.None;
    }

    /// <summary>原 <c>UniversalGameManagerPatch.OpenDialogMenu_Prefix</c> 的对话包回填与「最近阅读」记录。</summary>
    public void OnDialogOpened(DialogPackage package)
    {
        if (package is null)
        {
            return;
        }

        if (package.dialogContext is null)
        {
            if (DialogRegistry.ExampleDialog is null)
            {
                DialogRegistry.DumpExampleDialog();
            }
            if (DialogRegistry.ExampleDialog is not null)
            {
                package.dialogContext = DialogRegistry.ExampleDialog.dialogContext;
                Log.Info("Replaced dialogPackage.dialogContext with ExampleDialog.dialogContext");
            }
        }

        StoryReplayRecentHistory.Record(package);
    }

    /// <summary>原 <c>UniversalGameManagerPatch.LoadScene_Prefix</c>。</summary>
    public void OnSceneChanging(Scene scene)
    {
        GameFlow.BeforeSceneLoad(scene);
        Log.Info($"LoadScene called, scene {scene}");
    }

    /// <summary>原 <c>DaySceneManagerPatch.OnFirstEnterDaySceneFinish_Postfix</c>。</summary>
    public void OnDayFirstEntered() => GameFlow.OnCharactersReady(Scene.DayScene);

    /// <summary>原 <c>DaySceneManagerPatch.SwapMap_Prefix</c> 追加的换图后回调。</summary>
    public void OnDayMapEntered()
    {
        SpecialGuestRegistry.RefreshAllDayNpcs(); // TODO: 以更优雅的方式实现 Day NPC 刷新

        if (GameFlow.LocalScene != Scene.DayScene || !GameFlow.CharactersReady)
        {
            return;
        }

        PlayerManager.RefreshCharacters();
        PlayerProfile.SendMotion();
    }

    /// <summary>原 <c>DaySceneManagerPatch.OnDayOver_Prefix</c>。</summary>
    public void OnDayEnded()
    {
        if (s_ending)
        {
            return; // 模组自己发起的结束，不再重入。
        }

        if (!GameSession.IsInRoom)
        {
            PlayerManager.LocalIsDayOver = true;
            s_pendingEnd = PendingEnd.Vanilla;
            return;
        }

        if (DayDestinationManager.ReplayingBusiness)
        {
            s_pendingEnd = PendingEnd.Cooperative;
            return;
        }

        DayDestinationManager.Submit(DayDestination.Business, RequestDayOver);
    }

    /// <summary>
    /// 原 <c>DaySceneManagerPatch.OnDayOver</c>：多人协作下发邀请列表并关闭活动面板后结束白天。
    /// 真正的结束动作在下一个 <see cref="Update"/> 内执行。
    /// </summary>
    internal static void RequestDayOver() => s_pendingEnd = PendingEnd.Cooperative;

    private static void ApplyInputGates(IDaySceneServices services)
    {
        // 原补丁在控制台打开时跳过冲刺与互动；白天结束时（有房友）跳过互动。
        var consoleOpen = InGameConsole.IsOpen;
        services.Input.SetSprintEnabled(!consoleOpen);
        services.Input.SetInteractEnabled(!consoleOpen
            && !(GameSession.HasRoomPeers && (PlayerManager.LocalIsDayOver || DayDestinationManager.IsStoryLocked)));
    }

    private static void RunPendingEnd(IDaySceneServices services)
    {
        var pending = s_pendingEnd;
        if (pending == PendingEnd.None)
        {
            return;
        }
        s_pendingEnd = PendingEnd.None;

        if (pending == PendingEnd.Cooperative)
        {
            if (GameSession.IsRoomClient)
            {
                GuestInviteMessage.Send(StatusTracker.Instance?.InvitedGuests.ToManagedList());
            }
            Panel.CloseActivePanelsBeforeSceneTransit();
        }

        s_ending = true;
        try
        {
            services.Schedule.End();
        }
        finally
        {
            s_ending = false;
        }
    }
}
