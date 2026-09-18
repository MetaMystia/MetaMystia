using System.Linq;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

public static class PlayerProfile
{
    public static Player Capture()
    {
        var resources = PlayerManager.Local.IncrementalDataBase.Copy();
        resources.PackIds = ResourceExManager.LoadedPackages.Select(p => p.PackageLabel).OrderBy(p => p).ToArray();
        return new()
        {
            Name = PlayerIdentity.Name,
            Skin = CaptureSkin(),
            Scene = GameFlow.LocalScene,
            Stage = GameFlow.Stage,
            Resources = resources
        };
    }

    public static Skin CaptureSkin()
    {
        var skin = PlayerManager.Local.Skin;
        return new()
        {
            CharacterId = skin.CharacterId, SelectedType = skin.SelectedType, SkinIndex = skin.SkinIndex,
            NetSkinName = skin.NetSkinName ?? "", RotateOverride = skin.RotateOverride
        };
    }

    public static PlayerSkin ReadSkin(Skin skin) => new()
    {
        CharacterId = skin.CharacterId, SelectedType = skin.SelectedType, SkinIndex = skin.SkinIndex,
        NetSkinName = skin.NetSkinName, RotateOverride = skin.RotateOverride
    };

    public static void SendProfile()
    {
        PlayerManager.Local.Id = PlayerIdentity.Name;
        if (!GameSession.IsOnline) return;
        GameSession.Client.SetProfile(PlayerIdentity.Name, CaptureSkin(), GameFlow.LocalScene, GameFlow.Stage);
        if (GameFlow.CharactersReady && PlayerManager.Local.unit != null)
            FloatingTextHelper.SetPlayerLabel(PlayerManager.Local.Uid, LiveModeManager.GetDisplayName(PlayerManager.Local.Uid), PlayerManager.Local.unit.transform);
    }

    public static void SendMotion()
    {
        if (!GameSession.IsOnline || !GameFlow.CharactersReady || GameFlow.LocalScene is not Scene.DayScene and not Scene.WorkScene
            || !PlayerManager.CharacterSpawnedAndInitialized) return;
        if (GameFlow.LocalScene == Scene.DayScene && DayScene.SceneManager.Instance.IsMapSwapping) return;
        var direction = PlayerManager.LocalInputDirection;
        var position = PlayerManager.LocalPosition;
        GameSession.Client.SendMotion(new()
        {
            X = position.x, Y = position.y, DirectionX = direction.x, DirectionY = direction.y,
            Speed = PlayerManager.Local.Speed, Sprinting = PlayerManager.LocalIsSprinting,
            Map = PlayerManager.LocalMapLabel
        });
    }
}
