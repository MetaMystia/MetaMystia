using MemoryPack;

using MetaMystia.Patch;
using MetaMystia.UI;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 主机 → 所有客机：打烊与最终关店。打烊后主机可能因符卡继续接待（蕾米莉亚、露易兹、黑谷山女），
/// 客机不自行关店，等主机最终关店。
/// </summary>
[MemoryPackable]
public partial class IzakayaCloseMessage : MultiplayerMessage
{
    /// <summary>客机：已随主机打烊，用一次延长营业计数等待最终关店。</summary>
    private sealed class Hold : IScopeEnd
    {
        public bool Active;

        // 断线后按原版在本地顾客离开后关店。
        public void OnScopeEnd(bool resume)
        {
            if (Active && resume) EventManager.Instance.RegisteredDoNotCloseIzakayaStatus--;
        }
    }

    public bool Final { get; set; }

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var eventManager = EventManager.Instance;
        if (eventManager == null)
        {
            Log.Warning("EventManager is null when replaying host close.");
            return;
        }
        var hold = Scopes.Scene.Get<Hold>();
        if (Final)
        {
            hold.Active = false;
            UnblockCloseWait(eventManager);
            return;
        }

        Log.Message("Received close command from host");
        InGameConsole.ShowPassive(TextId.PeerClosedIzakaya.Get(PlayerManager.GetPeerName(SenderUid)));
        using (Bypass.Enter(eventManager))
        {
            NightSceneEventManagerPatch.StopInstantiationLoopAndCloseIzakaya_ReversePatch(eventManager);
        }
        if (hold.Active) return;
        hold.Active = true;
        eventManager.RegisteredDoNotCloseIzakayaStatus++;
    }

    public static void Send(bool final = false)
    {
        if (!GameSession.IsRoomHost) return;
        new IzakayaCloseMessage { Final = final }.Enqueue();
    }

    /// <summary>
    /// 控制台强制打烊：走完整 StopInstantiationLoop 路径，并清空可能卡住
    /// <see cref="GuestsManager.OnWaitForAllGuestToLeave"/> 的 occupiedDesks。
    /// </summary>
    public static bool TryForceLocalClose()
    {
        if (GameFlow.LocalScene != Common.UI.Scene.WorkScene) return false;

        var eventManager = EventManager.Instance;
        if (eventManager == null) return false;

        using (Bypass.Enter(eventManager))
        {
            NightSceneEventManagerPatch.StopInstantiationLoopAndCloseIzakaya_ReversePatch(eventManager);
            UnblockCloseWait(eventManager);
            if (GameSession.IsRoomHost) Send();
        }

        return true;
    }

    /// <summary>
    /// <see cref="GuestsManager.OnWaitForAllGuestToLeave"/> 依赖 occupiedDesks 清空且 CanCloseIzakaya 为真。
    /// 客机的延长营业计数与占桌可能与主机不同，最终关店时清除。
    /// </summary>
    private static void UnblockCloseWait(EventManager eventManager)
    {
        eventManager.RegisteredDoNotCloseIzakayaStatus = 0;

        var guestsManager = GuestsManager.Instance;
        if (guestsManager == null) return;

        guestsManager.TryRepellAllQueuedGuestControllers();
        guestsManager.occupiedDesks?.Clear();
    }
}
