using System.Linq;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.UI;

namespace MetaMystia;

public static class PlayerProfile
{
    public static Player Capture() => new()
    {
        Name = PlayerIdentity.Name,
        Skin = CaptureSkin(),
        Scene = GameFlow.LocalScene,
        Stage = GameFlow.Stage,
        Resources = PlayerManager.Local.IncrementalDataBase.ToNetwork()
    };

    public static Skin CaptureSkin()
    {
        var skin = PlayerManager.Local.Skin;
        var pack = ResourceExManager.LoadedPackages.FirstOrDefault(p => p.Config?.characters?.Any(c => c.id == skin.CharacterId) == true);
        return new()
        {
            CharacterId = skin.CharacterId, SelectedType = skin.SelectedType, SkinIndex = skin.SkinIndex,
            NetSkinName = skin.NetSkinName ?? "", RotateOverride = skin.RotateOverride,
            ResourcePackId = pack?.PackageLabel ?? ""
        };
    }

    public static PlayerSkin ReadSkin(Skin skin) => new()
    {
        CharacterId = skin.CharacterId, SelectedType = skin.SelectedType, SkinIndex = skin.SkinIndex,
        NetSkinName = skin.NetSkinName, RotateOverride = skin.RotateOverride,
        ResourcePackId = skin.ResourcePackId
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
