using Common.UI;
using MetaMystia.Patch;
using MetaMystia.Protocol.Messages.PrepScene;
using MetaMystia.Protocol.Transport;
using MetaMystia.UI;

namespace MetaMystia.Network.Handlers;

[AutoLog]
public static partial class PrepSceneHandlers
{
    public static void Register()
    {
        MessageDispatcher.Register<PrepAllReadyMessage>(HandlePrepAllReady);
        MessageDispatcher.Register<PrepReadyMessage>(HandlePrepReady);
        MessageDispatcher.Register<UpdatePrepMessage>(HandleUpdatePrep);
    }

    [HandlerAttributes.CheckScene(Scene.IzakayaPrepScene)]
    public static void HandlePrepAllReady(PrepAllReadyMessage msg)
    {
        if (msg.SenderUid != MpConstants.HostUid)
        {
            Log.Warning($"PrepAllReady from non-host uid={msg.SenderUid}, ignoring");
            return;
        }

        PrepSceneManager.ApplyHostTable(msg.PrepTableData);
        IzakayaConfigPannelPatch.PrepOver();
    }

    [HandlerAttributes.CheckScene(Scene.IzakayaPrepScene)]
    public static void HandlePrepReady(PrepReadyMessage msg)
    {
        PlayerManager.SetPeerPrepOver(msg.SenderUid);
        MpManager.PrepOver();
        InGameConsole.ShowPassive(TextId.ReadyForWork.Get(LiveModeManager.GetDisplayName(msg.SenderUid)));
    }

    [HandlerAttributes.CheckScene(Scene.IzakayaPrepScene)]
    public static void HandleUpdatePrep(UpdatePrepMessage msg)
    {
        PrepSceneManager.MergeFromPeer(msg.PrepTableData);
    }
}
