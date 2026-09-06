using MemoryPack;

namespace MetaMystia.Network;

/// <summary>双方在 Hello 前交换版本与房间概况；此消息固定占用 ID 0。</summary>
[MemoryPackable]
[AutoLog]
public partial class ConnectionInfoAction : Action
{
    public string GameVersion { get; set; } = "";
    public string ModVersion { get; set; } = "";
    public string ProtocolVersion { get; set; } = "";
    public int MaxPlayers { get; set; }
    public int PlayerCount { get; set; }

    public bool MatchesVersions(string gameVersion, string modVersion, string protocolVersion) =>
        GameVersion == gameVersion && ModVersion == modVersion && ProtocolVersion == protocolVersion;

    public override void OnReceivedDerived() => MpWire.OnConnectionInfoReceived(this);

    public static void Send(int? targetUid = null) =>
        new ConnectionInfoAction
        {
            GameVersion = Plugin.GameVersion,
            ModVersion = Plugin.ModVersion,
            ProtocolVersion = Plugin.ProtocolVersion,
            MaxPlayers = ConfigManager.MaxPlayers.Value,
            PlayerCount = MpManager.AllPlayersCount,
            WireTargetUid = targetUid
        }.Enqueue();
}
