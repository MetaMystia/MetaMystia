using MemoryPack;

using Common.UI;
using GameData.Core.Collections.CharacterUtility;

namespace MetaMystia;

[MemoryPackable]
public sealed partial record AppearanceSnapshot(int CharacterId, CharacterSkinSets.SelectedType SelectedType,
    int SkinIndex, string NetSkinName, bool? RotateOverride)
{
    public static AppearanceSnapshot Capture(PlayerSkin skin) => new(skin.CharacterId, skin.SelectedType, skin.SkinIndex, skin.NetSkinName, skin.RotateOverride);
}

public sealed record MotionSnapshot(float X, float Y, float Vx, float Vy, float Speed, bool Sprinting);
public sealed record PresenceSnapshot(Scene Scene, long SceneEpoch, MapLabel Map, MotionSnapshot Motion);
