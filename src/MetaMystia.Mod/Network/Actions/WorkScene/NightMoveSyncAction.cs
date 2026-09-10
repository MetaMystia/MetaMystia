using System;

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 同房成员：夜间角色移动同步
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class NightMoveSyncAction : Action
{
    private const long KeepaliveMs = 500;
    private static NightMoveSyncAction _lastSent;
    private static long _lastSentAt;
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Px { get; set; }
    public float Py { get; set; }
    public float Speed { get; set; }

    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Debug;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Debug;

    public override void OnReceivedDerived()
    {
        ModPlayerStore.ApplyNightMotion(SenderUid, new(Px, Py, Vx, Vy, Speed, false));
    }

    public static void Send()
    {
        if (!MpManager.IsConnected) return;
        var inputDirection = PlayerManager.LocalInputDirection;
        var position = PlayerManager.LocalPosition;
        var action = new NightMoveSyncAction
        {
            Vx = inputDirection.x,
            Vy = inputDirection.y,
            Px = position.x,
            Py = position.y,
            Speed = PlayerManager.Local.Speed
        };
        if (!Changed(action)) return;
        action.Enqueue();
        _lastSent = action;
        _lastSentAt = MpWire.NowMs;
    }

    private static bool Changed(NightMoveSyncAction action) =>
        _lastSent == null || MpWire.NowMs - _lastSentAt >= KeepaliveMs
        || action.Speed != _lastSent.Speed || action.Vx != _lastSent.Vx || action.Vy != _lastSent.Vy
        || Math.Abs(action.Px - _lastSent.Px) > 0.001f || Math.Abs(action.Py - _lastSent.Py) > 0.001f;
}
