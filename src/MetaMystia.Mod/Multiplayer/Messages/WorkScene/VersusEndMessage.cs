using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>房主的料理对决计时结束，客机随之结束本局。</summary>
[MemoryPackable]
public partial class VersusEndMessage : MultiplayerMessage
{
    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => VersusManager.ReceiveEnd();

    public static void Send() => new VersusEndMessage().Enqueue();
}
