using UnityEngine;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class MoveSyncBehavior
{
    public static void Send()
    {
        if (!MpManager.IsConnected)
            return;
        if (MpManager.LocalScene != Common.UI.Scene.DayScene && MpManager.LocalScene != Common.UI.Scene.WorkScene)
            return;
        if (!PlayerManager.CharacterSpawnedAndInitialized)
            return;

        var inputDirection = PlayerManager.LocalInputDirection;
        var position = PlayerManager.LocalPosition;

        if (MpManager.LocalScene == Common.UI.Scene.WorkScene)
        {
            NightMoveSyncBehavior.Send();
            return;
        }

        var action = new MoveSyncAction
        {
            IsSprinting = PlayerManager.LocalIsSprinting,
            Speed = PlayerManager.Local.Speed,
            Vx = inputDirection.x,
            Vy = inputDirection.y,
            MapLabel = PlayerManager.LocalMapLabel,
            Px = position.x,
            Py = position.y
        };
        action.Enqueue(lowPriority: true);
    }

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<MoveSyncAction>(Handle,
            scene: Common.UI.Scene.DayScene);
    }

    private static void Handle(MoveSyncAction action)
    {
        if (PlayerManager.TryGetVisiblePeer(action.SenderUid, out var peer))
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
