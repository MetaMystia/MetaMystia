using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
public partial class VersusChoiceMessage : MultiplayerMessage
{
    public int Invite { get; set; }
    public VersusTeam Team { get; set; }
    public bool HasDlc3 { get; set; }

    [HostOnlyReceive]
    public override void OnReceivedDerived() => VersusManager.ReceiveChoice(SenderUid, Invite, Team, HasDlc3);

    public static void Send(int invite, VersusTeam team, bool hasDlc3) =>
        new VersusChoiceMessage { Invite = invite, Team = team, HasDlc3 = hasDlc3 }.Enqueue();
}
