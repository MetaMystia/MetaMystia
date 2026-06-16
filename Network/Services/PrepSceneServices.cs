using MetaMystia.Protocol.Messages.PrepScene;

namespace MetaMystia.Network.Services;

public static class PrepSceneServices
{
    public static void SendPrepAllReady()
    {
        if (!MpManager.IsRoomHost) return;
        MpWire.Send(new PrepAllReadyMessage {PrepTableData = PrepSceneManager.GetLocalPrepTableSnapshot()});
    }

    public static void SendPrepReady()
    {
        MpWire.Send(new PrepReadyMessage());
    }

    public static void SendUpdatePrep()
    {
        MpWire.Send(new UpdatePrepMessage
        {
            PrepTableData = PrepSceneManager.GetLocalPrepTableSnapshot()
        });
    }
}
