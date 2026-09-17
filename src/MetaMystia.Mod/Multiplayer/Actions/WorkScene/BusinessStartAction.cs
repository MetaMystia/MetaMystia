using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

[MemoryPackable]
[AutoLog]
public partial class BusinessStartAction : Action
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

    public static void Send(bool start) => new BusinessStartAction { Start = start }.Enqueue();
}
