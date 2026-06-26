namespace MetaMystia.Network;

[NetActionBehavior]
internal static class SceneTransitBehavior
{
    public static void Send(Common.UI.Scene scene) =>
        new SceneTransitAction { Scene = scene.ToWire() }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<SceneTransitAction>(Handle);
    }

    private static void Handle(SceneTransitAction action)
    {
        MpManager.PeerScene = action.Scene.ToGame();
        if (PlayerManager.TryGetVisiblePeer(action.SenderUid, out var peer))
            peer.Scene = action.Scene.ToGame();
    }
}
