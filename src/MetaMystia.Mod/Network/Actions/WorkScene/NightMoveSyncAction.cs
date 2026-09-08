using MemoryPack;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：夜间角色移动同步
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

    public static void Send()
    {
        if (!MpManager.CanSeeOnlinePlayers || !MpManager.IsConnected || MpManager.LocalScene != Common.UI.Scene.WorkScene) return;
        if (!PlayerManager.CharacterSpawnedAndInitialized) return;
        var inputDirection = PlayerManager.LocalInputDirection;
        var position = PlayerManager.LocalPosition;
        new NightMoveSyncAction
        {
            Vx = inputDirection.x,
            Vy = inputDirection.y,
            Px = position.x,
            Py = position.y,
            Speed = PlayerManager.Local.Speed
        }.Enqueue();
    }
}
