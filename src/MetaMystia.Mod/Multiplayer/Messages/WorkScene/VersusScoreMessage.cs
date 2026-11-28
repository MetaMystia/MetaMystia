using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>房主的料理对决比分：红、蓝得分与连击；Final 为结算前的最终比分。</summary>
[MemoryPackable]
public partial class VersusScoreMessage : MultiplayerMessage
{
    public int[] Score { get; set; }
    public bool Final { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => VersusMatch.ReceiveScore(Score, Final);

    public static void Send(int[] score, bool final) => new VersusScoreMessage { Score = score, Final = final }.Enqueue();
}
