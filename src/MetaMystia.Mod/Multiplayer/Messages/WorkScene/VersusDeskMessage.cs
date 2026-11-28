using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>房主裁定的料理对决占桌：<see cref="VersusTeam.None"/> 为释放。</summary>
[MemoryPackable]
public partial class VersusDeskMessage : MultiplayerMessage
{
    public int Desk { get; set; }
    public VersusTeam Team { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => VersusMatch.ApplyDesk(Desk, Team);

    public static void Send(int desk, VersusTeam team) => new VersusDeskMessage { Desk = desk, Team = team }.Enqueue();
}
