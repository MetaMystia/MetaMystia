using MemoryPack;

namespace MetaMystia.Network;

// public enum Scene
// {
//     DayScene,
//     MainScene,
//     LoadScene,
//     IzakayaPrepScene,
//     WorkScene,
//     ResultScene,
//     StaffScene,
//     EmptyScene
// }

/// <summary>
/// 所有玩家 → 所有玩家：通告自身 Scene 切换
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class SceneTransitAction : Action
{
    // 走 Schema 的镜像枚举，服务器不引用游戏程序集也能读写该字段。
    public MetaMystia.Schema.Scene Scene { get; set; }
    public long SceneEpoch { get; set; }
    public override void OnReceivedDerived()
    {
        ModPlayerStore.ApplyScene(SenderUid, (Common.UI.Scene)(int)Scene, SceneEpoch);
    }

    public static void Send(Common.UI.Scene scene) =>
        new SceneTransitAction { Scene = (MetaMystia.Schema.Scene)(int)scene, SceneEpoch = GameContext.SceneEpoch }.Enqueue();
}
