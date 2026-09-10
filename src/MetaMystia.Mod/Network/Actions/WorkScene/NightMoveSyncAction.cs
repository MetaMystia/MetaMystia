using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 同房成员：夜间角色移动同步
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class NightMoveSyncAction : Action
{
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

    public static NightMoveSyncAction Capture() => new()
    {
        Vx = PlayerManager.LocalInputDirection.x,
        Vy = PlayerManager.LocalInputDirection.y,
        Px = PlayerManager.LocalPosition.x,
        Py = PlayerManager.LocalPosition.y,
        Speed = PlayerManager.Local.Speed
    };
}
