using MetaMystia.Protocol.Messages.DayScene;
using MetaMystia.Protocol.Messages.WorkScene;

namespace MetaMystia.Network.Services;

public static class DaySceneServices
{
    public static void SendConfirmIzakaya(string mapLabel, int mapLevel)
    {
        if (!MpManager.IsRoomHost) return;
        MpWire.Send(new ConfirmIzakayaMessage
        {
            MapLabel = mapLabel,
            MapLevel = mapLevel
        });
    }

    public static void SendSelectIzakaya(string mapLabel, int level)
    {
        MpWire.Send(new SelectIzakayaMessage
        {
            MapLabel = mapLabel,
            MapLevel = level
        });
    }

    public static void SendMoveSync()
    {
        if (!MpManager.CanSeeOnlinePlayers || !MpManager.IsConnected)
        {
            return;
        }
        if (MpManager.LocalScene != Common.UI.Scene.DayScene && MpManager.LocalScene != Common.UI.Scene.WorkScene)
        {
            return;
        }
        if (!PlayerManager.CharacterSpawnedAndInitialized)
        {
            return;
        }

        var inputDirection = PlayerManager.LocalInputDirection;
        var position = PlayerManager.LocalPosition;

        if (MpManager.LocalScene == Common.UI.Scene.WorkScene)
        {
            MpWire.Send(new NightMoveSyncMessage
            {
                Vx = inputDirection.x,
                Vy = inputDirection.y,
                Px = position.x,
                Py = position.y,
                Speed = PlayerManager.Local.Speed
            }, lowPriority: true);
        }
        else
        {
            MpWire.Send(new MoveSyncMessage
            {
                IsSprinting = PlayerManager.LocalIsSprinting,
                Speed = PlayerManager.Local.Speed,
                Vx = inputDirection.x,
                Vy = inputDirection.y,
                MapLabel = PlayerManager.LocalMapLabel,
                Px = position.x,
                Py = position.y
            }, lowPriority: true);
        }
    }
}
