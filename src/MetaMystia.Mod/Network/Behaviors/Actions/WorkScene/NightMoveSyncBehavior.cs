using UnityEngine;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class NightMoveSyncBehavior
{
    public static void Send()
    {
        if (!MpManager.IsRoomConnected || MpManager.LocalScene != Common.UI.Scene.WorkScene)
            return;
        if (!PlayerManager.CharacterSpawnedAndInitialized)
            return;

        var inputDirection = PlayerManager.LocalInputDirection;
        var position = PlayerManager.LocalPosition;
        new NightMoveSyncAction
        {
            Vx = inputDirection.x,
            Vy = inputDirection.y,
            Px = position.x,
            Py = position.y,
            Speed = PlayerManager.Local.Speed
        }.Enqueue(lowPriority: true);
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<NightMoveSyncAction>(Handle,
            scene: Common.UI.Scene.WorkScene);
    }

    private static void Handle(NightMoveSyncAction action)
    {
        if (PlayerManager.TryGetRoomPeer(action.SenderUid, out var peer))
        {
            peer.NightSyncFromPeer(
                action.Speed,
                new Vector2(action.Vx, action.Vy),
                new Vector2(action.Px, action.Py));
        }
    }
}
