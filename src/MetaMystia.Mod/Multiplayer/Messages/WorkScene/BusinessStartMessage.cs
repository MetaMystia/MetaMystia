using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class BusinessStartMessage : MultiplayerMessage
{
    public bool Start { get; set; }

    public override void OnReceivedDerived()
    {
        if (Start)
        {
            if (SenderUid == GameSession.Room?.Host) BusinessStart.ApplyStart();
        }
        else BusinessStart.ReceiveReady(SenderUid);
    }

    public static void Send(bool start) => new BusinessStartMessage { Start = start }.Enqueue();
}
