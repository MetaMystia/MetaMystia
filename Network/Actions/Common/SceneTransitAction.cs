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
[PublicRelay]
public partial class SceneTransitAction : Action
{

    [MemoryPackAllowSerialize]
    public Common.UI.Scene Scene { get; set; }
    public override void OnReceivedDerived()
    {
        MpManager.PeerScene = Scene;
        return;
    }

    public static void Send(Common.UI.Scene scene) =>
        new SceneTransitAction { Scene = scene }.Enqueue();
}
