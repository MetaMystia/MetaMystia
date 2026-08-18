namespace MetaMystia.Network;

public enum HandshakeRejectReason : ushort
{
    Unknown,
    ModVersionMismatch,
    GameVersionMismatch,
    GameResourcesNotLoaded,
    PrepWorkReconnectBlocked,
    ServerFull,
    InvalidPlayerId,
    DuplicatePlayerId,
    UnsupportedServerMode,
}
