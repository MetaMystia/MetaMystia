using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：白天角色移动同步
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class MoveSyncAction : Action
{
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

    public static MoveSyncAction Capture() => new()
    {
        IsSprinting = PlayerManager.LocalIsSprinting,
        Speed = PlayerManager.Local.Speed,
        Vx = PlayerManager.LocalInputDirection.x,
        Vy = PlayerManager.LocalInputDirection.y,
        MapLabel = PlayerManager.LocalMapLabel,
        SceneEpoch = GameContext.SceneEpoch,
        Px = PlayerManager.LocalPosition.x,
        Py = PlayerManager.LocalPosition.y
    };
}
