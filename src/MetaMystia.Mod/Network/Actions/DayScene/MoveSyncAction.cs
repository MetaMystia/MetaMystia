using System;

using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：白天角色移动同步
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class MoveSyncAction : Action
{
    private const long KeepaliveMs = 500;
    private static MoveSyncAction _lastSent;
    private static long _lastSentAt;
    public float Vx { get; set; }
    public float Vy { get; set; }
    public float Px { get; set; }
    public float Py { get; set; }
    public bool IsSprinting { get; set; }
    public float Speed { get; set; }
    public MapLabel MapLabel { get; set; }
    public long SceneEpoch { get; set; }

    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Debug;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Debug;

    public override void OnReceivedDerived()
    {
        ModPlayerStore.ApplyDayMotion(SenderUid, SceneEpoch, MapLabel, new(Px, Py, Vx, Vy, Speed, IsSprinting));
    }

    public static void Send()
    {
        var inputDirection = PlayerManager.LocalInputDirection;
        var position = PlayerManager.LocalPosition;

        var action = new MoveSyncAction
        {
            IsSprinting = PlayerManager.LocalIsSprinting,
            Speed = PlayerManager.Local.Speed,
            Vx = inputDirection.x,
            Vy = inputDirection.y,
            MapLabel = PlayerManager.LocalMapLabel,
            SceneEpoch = GameContext.SceneEpoch,
            Px = position.x,
            Py = position.y
        };
        if (!Changed(action)) return;
        action.Enqueue();
        _lastSent = action;
        _lastSentAt = MpWire.NowMs;
    }

    private static bool Changed(MoveSyncAction action) =>
        _lastSent == null || MpWire.NowMs - _lastSentAt >= KeepaliveMs
        || action.IsSprinting != _lastSent.IsSprinting || action.Speed != _lastSent.Speed
        || action.MapLabel != _lastSent.MapLabel || action.SceneEpoch != _lastSent.SceneEpoch
        || action.Vx != _lastSent.Vx || action.Vy != _lastSent.Vy
        || Math.Abs(action.Px - _lastSent.Px) > 0.001f || Math.Abs(action.Py - _lastSent.Py) > 0.001f;
}
