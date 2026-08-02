using UnityEngine;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class DayMoveSyncBehavior
{
    public static void Send()
    {
        if (!MpManager.IsConnected || MpManager.LocalScene != Common.UI.Scene.DayScene)
            return;
        if (!PlayerManager.CharacterSpawnedAndInitialized)
            return;

        var inputDirection = PlayerManager.LocalInputDirection;
        var position = PlayerManager.LocalPosition;
        new DayMoveSyncAction
        {
            IsSprinting = PlayerManager.LocalIsSprinting,
            Speed = PlayerManager.Local.Speed,
            Vx = inputDirection.x,
            Vy = inputDirection.y,
            MapLabel = PlayerManager.LocalMapLabel,
            Px = position.x,
            Py = position.y
        }.Enqueue(lowPriority: true);
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<DayMoveSyncAction>(Handle,
            scene: Common.UI.Scene.DayScene);
    }

    private static void Handle(DayMoveSyncAction action)
    {
        if (PlayerManager.PlayerTable.TryGetValue(action.SenderUid, out var peer))
        {
            PlayerManager.TryEnsureDayScenePeer(action.SenderUid);
            peer.SyncFromPeer(
                action.MapLabel,
                action.IsSprinting,
                action.Speed,
                new Vector2(action.Vx, action.Vy),
                new Vector2(action.Px, action.Py));
        }
    }
}
