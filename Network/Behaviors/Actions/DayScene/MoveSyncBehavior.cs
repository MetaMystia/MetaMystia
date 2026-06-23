using UnityEngine;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class MoveSyncBehavior
{
    // Also sends NightMoveSync while in WorkScene.
    public static void Send()
    {
        // MoveSync 是 PublicRelay：公域/房间均可发送。CanSeeOnlinePlayers 已涵盖 IsInRoom || IsInPublicScope。
        if (!MpManager.CanSeeOnlinePlayers)
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
        // MoveSync 是 PublicRelay：公域玩家也可见，不能只查 Peers（房间索引）。
        if (PlayerManager.TryGetVisiblePeer(action.SenderUid, out var peer))
        {
            peer.SyncFromPeer(
                action.MapLabel,
                action.IsSprinting,
                action.Speed,
                new Vector2(action.Vx, action.Vy),
                new Vector2(action.Px, action.Py));
        }
    }
}
