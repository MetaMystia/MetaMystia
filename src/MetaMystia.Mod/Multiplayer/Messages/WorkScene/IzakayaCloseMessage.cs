using MemoryPack;
using System.Linq;

using Mystia.Scenes;

using MetaMystia.UI;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 主机 → 所有客机：广播打烊
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class IzakayaCloseMessage : MultiplayerMessage
{

    /// <summary>
    /// 客机收到主机广播的打烊命令 → 排队到营业场景循环的服务作用域内重放打烊。
    /// </summary>
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        Log.Message($"Received close command from host");
        InGameConsole.ShowPassive(TextId.PeerClosedIzakaya.Get(PlayerManager.GetPeerName(SenderUid)));
        Listeners.GuestSync.EnqueueReplay("izakaya close", Listeners.GuestSync.ReplayIzakayaClose);
    }

    /// <summary>
    /// 主机 → 所有客机：广播打烊命令
    /// </summary>
    public static void Send()
    {
        if (!GameSession.IsRoomHost) return;
        new IzakayaCloseMessage().Enqueue();
    }

    /// <summary>
    /// 控制台强制打烊：改走服务（<see cref="IWorkSceneIzakaya.Close"/> 内部放行被关掉的关店开关），
    /// 因此排队到营业场景循环内执行，并清空可能卡住 <see cref="GuestsManager.OnWaitForAllGuestToLeave"/>
    /// 的 occupiedDesks。
    /// </summary>
    public static bool TryForceLocalClose()
    {
        if (GameFlow.LocalScene != Common.UI.Scene.WorkScene) return false;
        if (EventManager.Instance == null) return false;

        Listeners.GuestSync.EnqueueReplay("force close", ReplayForceLocalClose);
        return true;
    }

    /// <summary>
    /// 等价原 <c>EventManager.StopInstantiationLoopAndCloseIzakaya</c>（停刷客循环 + 时间耗尽 + 关店），
    /// 并强制清空 occupiedDesks。
    /// </summary>
    private static void ReplayForceLocalClose(IWorkSceneServices services)
    {
        var eventManager = EventManager.Instance;
        if (eventManager == null) return;

        eventManager.StopGuestInstantiateLoop();
        eventManager.SetTimeDepeleted();
        services.Izakaya.Close();
        ForceUnblockClientCloseWait(eventManager);
    }

    /// <summary>
    /// 客机 <see cref="GuestsManager.OnWaitForAllGuestToLeave"/> 依赖 occupiedDesks 清空且 CanCloseIzakaya 为真。
    /// 联机下 occupiedDesks 由重放的入座写入，但 LeaveFromDesk 平时被关掉，desync 后会残留幽灵占桌。
    /// </summary>
    internal static void UnblockClientCloseWait(EventManager eventManager)
    {
        if (!GameSession.IsRoomClient) return;
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
