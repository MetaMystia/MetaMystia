using System.Linq;

using MetaMystia.Patch;
using MetaMystia.UI;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class IzakayaCloseBehavior
{
    /// <summary>
    /// 主机 → 所有客机：广播打烊命令。
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

    internal static void UnblockClientCloseWait(EventManager eventManager)
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
                occupiedDesks.Remove(deskCode);
        }
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<IzakayaCloseAction>(Handle,
            scene: Common.UI.Scene.WorkScene);
    }

    private static void Handle(IzakayaCloseAction action)
    {
        Plugin.Instance?.Log.LogMessage("Received close command from host");
        InGameConsole.ShowPassive(TextId.PeerClosedIzakaya.Get(PlayerManager.GetPeerName(action.SenderUid)));
        var eventManager = EventManager.Instance;
        if (eventManager == null)
        {
            Plugin.Instance?.Log.LogWarning("EventManager is null when replaying host close.");
            return;
        }

        NightSceneEventManagerPatch.HostCloseReplay.Grant();
        NightSceneEventManagerPatch.StopInstantiationLoopAndCloseIzakaya_ReversePatch(eventManager);
        IzakayaCloseBehavior.UnblockClientCloseWait(eventManager);
        NightSceneEventManagerPatch.HostCloseReplay.Reset();
    }
}
