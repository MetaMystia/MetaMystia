using MemoryPack;
using System.Linq;

using MetaMystia.Patch;
using MetaMystia.UI;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 所有客机：广播打烊
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class IzakayaCloseAction : Action
{

    /// <summary>
    /// 客机收到主机广播的打烊命令 → 设置允许打烊标志并直接触发打烊流程
    /// </summary>
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        Log.Message($"Received close command from host");
        InGameConsole.ShowPassive(TextId.PeerClosedIzakaya.Get(PlayerManager.GetPeerName(SenderUid)));
        var eventManager = EventManager.Instance;
        if (eventManager == null)
        {
            Log.Warning("EventManager is null when replaying host close.");
            return;
        }

        NightSceneEventManagerPatch.HostCloseReplay.Grant();
        NightSceneEventManagerPatch.StopInstantiationLoopAndCloseIzakaya_ReversePatch(eventManager);
        UnblockClientCloseWait(eventManager);
        NightSceneEventManagerPatch.HostCloseReplay.Reset();
    }

    /// <summary>
    /// 主机 → 所有客机：广播打烊命令
    /// </summary>
    public static void Send()
    {
        if (!MpManager.IsRoomHost) return;
        new IzakayaCloseAction().Enqueue();
    }

    /// <summary>
    /// 控制台强制打烊：走完整 StopInstantiationLoop 路径，并清空可能卡住
    /// <see cref="GuestsManager.OnWaitForAllGuestToLeave"/> 的 occupiedDesks。
    /// </summary>
    public static bool TryForceLocalClose()
    {
        if (MpManager.LocalScene != Common.UI.Scene.WorkScene) return false;

        var eventManager = EventManager.Instance;
        if (eventManager == null) return false;

        NightSceneEventManagerPatch.HostCloseReplay.Grant();
        try
        {
            NightSceneEventManagerPatch.StopInstantiationLoopAndCloseIzakaya_ReversePatch(eventManager);
            ForceUnblockClientCloseWait(eventManager);
            if (MpManager.IsRoomHost) Send();
        }
        finally
        {
            NightSceneEventManagerPatch.HostCloseReplay.Reset();
        }

        return true;
    }

    /// <summary>
    /// 客机 <see cref="GuestsManager.OnWaitForAllGuestToLeave"/> 依赖 occupiedDesks 清空且 CanCloseIzakaya 为真。
    /// 联机下 occupiedDesks 由 ReplayTrySendToSeat 写入，但 LeaveFromDesk 平时被 Prefix 跳过，desync 后会残留幽灵占桌。
    /// </summary>
    private static void UnblockClientCloseWait(EventManager eventManager)
    {
        if (!MpManager.IsRoomClient) return;
        PrepareClientCloseWait(eventManager, forceClearOccupiedDesks: false);
    }

    private static void ForceUnblockClientCloseWait(EventManager eventManager)
    {
        PrepareClientCloseWait(eventManager, forceClearOccupiedDesks: true);
    }

    private static void PrepareClientCloseWait(EventManager eventManager, bool forceClearOccupiedDesks)
    {
        eventManager.RegisteredDoNotCloseIzakayaStatus = 0;

        var guestsManager = GuestsManager.Instance;
        if (guestsManager == null) return;

        guestsManager.TryRepellAllQueuedGuestControllers();

        var occupiedDesks = guestsManager.occupiedDesks;
        if (occupiedDesks == null) return;

        if (forceClearOccupiedDesks)
        {
            occupiedDesks.Clear();
            return;
        }

        foreach (var deskCode in occupiedDesks.ToArray())
        {
            var guest = guestsManager.GetInDeskGuest(deskCode);
            if (guest == null || !guest.HaveNotLeft())
            {
                occupiedDesks.Remove(deskCode);
            }
        }
    }
}
