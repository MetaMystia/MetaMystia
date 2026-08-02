using UnityEngine;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Linq;
using System.Text;

using Common.CharacterUtility;
using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;

using MetaMystia.Network;
using SgrYuki.Utils;

namespace MetaMystia;

// 行为半边（mod）：皮肤解析、立绘、应用到 unit 等游戏逻辑（Addressables/Sprite/CharacterControllerUnit）。
// 数据半边（序列化字段 + SetNetSkin/SetRotate）见 src/MetaMystia.Protocol/Dtos/PlayerSkinData.cs。
// 边界：游戏枚举 CharacterSkinSets.SelectedType 与协议层 WireSkinType 在此互转（WireEnumMaps）。

public static class PlayerSkin
{
    private sealed class RotatedSkinCache
    {
        public CharacterSpriteSetCompact Skin;
        public CharacterSpriteSetCompact Source;
        public bool? Rotate;
    }

    private static readonly ConditionalWeakTable<PlayerSkinData, RotatedSkinCache> RotatedSkinCaches = new();

    public static void InvalidateRotatedSkinCache(this PlayerSkinData playerSkin)
    {
        if (playerSkin != null)
            RotatedSkinCaches.Remove(playerSkin);
    }

    /// <summary>
    /// 解析 CharacterSpriteSetCompact
    /// </summary>
    public static CharacterSpriteSetCompact ResolveSkin(this PlayerSkinData playerSkin)
    {
        if (!string.IsNullOrEmpty(playerSkin.NetSkinName))
        {
            if (NetSkinManager.TryGet(playerSkin.NetSkinName, out var net))
                return net;
            // 未就绪：触发异步加载，先返回 Fallback 占位
            NetSkinManager.RequestSkin(playerSkin.NetSkinName);
            return DataBaseCharacter.FallbackFullPixel;
        }

        if (playerSkin.CharacterId == -1)
        {
            return ResolveSkin(DataBaseCharacter.SelfSpriteSet, playerSkin.SelectedType.ToGameSkinType(), playerSkin.SkinIndex);
        }

        if (DataBaseCharacter.SpecialGuestVisual.ContainsKey(playerSkin.CharacterId))
        {
            return ResolveSkin(DataBaseCharacter.SpecialGuestVisual[playerSkin.CharacterId]?.CharacterPixel, playerSkin.SelectedType.ToGameSkinType(), playerSkin.SkinIndex);
        }

        Plugin.Instance?.Log.LogWarning(
            $"CharacterId {playerSkin.CharacterId} not found in SpecialGuestVisual, returning Fallback skin");
        return DataBaseCharacter.FallbackFullPixel;
    }

    private static CharacterSpriteSetCompact ResolveSkin(
        CharacterSkinSets skinSets, CharacterSkinSets.SelectedType type, int index)
    {
        if (skinSets is null) return null;

        return type switch
        {
            CharacterSkinSets.SelectedType.Default => skinSets.defaultSkin,
            CharacterSkinSets.SelectedType.Explicit => (index >= 0 && index < skinSets.explicits.Length)
                ? skinSets.explicits[index] : skinSets.defaultSkin,
            CharacterSkinSets.SelectedType.DLC => (index >= 0 && index < skinSets.dlcs.Length)
                ? skinSets.dlcs[index] : skinSets.defaultSkin,
            _ => skinSets.defaultSkin
        };
    }

    /// <summary>
    /// 解析当前皮肤对应的 CharacterPortrayal（立绘配置），专门用于 SpecialGuest
    /// </summary>
    public static CharacterPortrayal ResolveSpecialPortrait(this PlayerSkinData playerSkin)
    {
        if (DataBaseCharacter.SpecialGuestVisual.ContainsKey(playerSkin.CharacterId))
        {
            return DataBaseCharacter.SpecialGuestVisual[playerSkin.CharacterId]?.CharacterPortrayal?.defaultPortrayal;
        }

        return DataBaseCharacter.FallbackPortrayal;
    }

    private static Sprite ResolvePortraitFromSelf(CharacterSkinSets.SelectedType type, int index)
    {
        if (type == CharacterSkinSets.SelectedType.Default)
        {
            return DataBaseCharacter.SelfPortrayalSet?.defaultPortrayal.m_VisualAssetAtlasReference[0]?.Asset
                ?.TryCast<Sprite>();
        }

        return DataBaseCore.Clothes
            .ToList()
            .Where(c => c.Value.skinIndex.index == index && c.Value.skinIndex.selectedType == type)
            .Select(c => ResolveSelfPortrayalFromClothes(c.Value))
            .FirstOrDefault() ?? ResolvePortraitFromSelf(CharacterSkinSets.SelectedType.Default, 0);
    }

    private static Sprite ResolveSelfPortrayalFromClothes(ClothesProfile.Clothes clothes)
    {
        if (!clothes.IsValidVisual)
            return null;

        var assetRef = clothes.m_OverrideVisualAsset;
        var sprite = assetRef.Asset?.TryCast<Sprite>();

        if (sprite == null)
        {
            var handle = assetRef.LoadAssetAsync();
            sprite = handle.WaitForCompletion();
        }

        return sprite;
    }

    /// <summary>
    /// 获取当前皮肤的立绘 Sprite（使用默认表情，索引 0）
    /// 优先级: ResourceEx 自定义立绘 > 已加载的 Addressable 资源 > 同步加载 Addressable
    /// </summary>
    public static Sprite ResolvePortraitSprite(this PlayerSkinData playerSkin)
    {
        if (playerSkin.CharacterId == -1)
        {
            return ResolvePortraitFromSelf(playerSkin.SelectedType.ToGameSkinType(), playerSkin.SkinIndex);
        }

        var portrayal = playerSkin.ResolveSpecialPortrait();
        if (portrayal == null) return null;

        // 优先：ResourceEx 自定义立绘
        if (ResourceExManager.TryGetSpecialGuestCustomPortrayal(portrayal, out var customSprites, out var faceInNoteBook))
        {
            var index = (faceInNoteBook >= 0 && faceInNoteBook < customSprites.Length) ? faceInNoteBook : 0;
            return customSprites[index];
        }

        var refs = portrayal.m_VisualAssetAtlasReference;
        if (refs == null || refs.Length == 0) return null;


        var assetRef = (portrayal.faceInNoteBook >= 0 && portrayal.faceInNoteBook < refs.Length)
            ? refs[portrayal.faceInNoteBook]
            : refs[0];
        if (assetRef == null) return null;

        var sprite = assetRef.Asset?.TryCast<Sprite>();
        if (sprite != null) return sprite;

        try
        {
            var handle = assetRef.LoadAssetAsync<Sprite>();
            sprite = handle.WaitForCompletion();
            return sprite;
        }
        catch (System.Exception e)
        {
            Plugin.Instance?.Log.LogWarning($"Failed to load portrait sprite: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 设定皮肤
    /// </summary>
    public static void SetSkin(this PlayerSkinData playerSkin, int characterId, CharacterSkinSets.SelectedType selectedType, int skinIndex)
    {
        playerSkin.CharacterId = characterId;
        playerSkin.SelectedType = selectedType.ToWire();
        playerSkin.SkinIndex = skinIndex;
        playerSkin.NetSkinName = null;
        playerSkin.InvalidateRotatedSkinCache();
    }

    private static CharacterSpriteSetCompact ResolveSkinForUnit(this PlayerSkinData playerSkin)
    {
        var baseSkin = playerSkin.ResolveSkin();
        if (baseSkin == null || !playerSkin.RotateOverride.HasValue)
            return baseSkin;

        var cache = RotatedSkinCaches.GetOrCreateValue(playerSkin);
        if (cache.Skin != null
            && cache.Source == baseSkin
            && cache.Rotate == playerSkin.RotateOverride)
            return cache.Skin;

        cache.Source = baseSkin;
        cache.Rotate = playerSkin.RotateOverride;
        cache.Skin = CloneWithRotationOverride(baseSkin, playerSkin.RotateOverride.Value, 0.15f);
        return cache.Skin;
    }

    private static CharacterSpriteSetCompact CloneWithRotationOverride(
        CharacterSpriteSetCompact source, bool isHina, float rotatePerTime)
    {
        if (source is CharacterSpriteSetFull fullSource)
            return CloneFullWithRotation(fullSource, isHina, rotatePerTime);
        return CloneCompactWithRotation(source, isHina, rotatePerTime);
    }

    private static CharacterSpriteSetCompact CloneCompactWithRotation(
        CharacterSpriteSetCompact source, bool isHina, float rotatePerTime)
    {
        var clone = ScriptableObject.CreateInstance<CharacterSpriteSetCompact>();
        clone.Initialize(
            source.MainSprite,
            source.DoNotUseEyeSprite,
            source.EyeSprite,
            source.HasPrebakedShadow,
            source.AnimationSpeedMultiplier,
            source.ExtraYOffset,
            isHina,
            rotatePerTime,
            source.DoNotHaveStepVFX,
            source.MoveSpeedMultiplier,
            source.RemovableTrims,
            source.TrimSpritesDisplayFront,
            source.TrimSpritesDisplayBack,
            source.TrimFrontSpriteFrameSpeed,
            source.TrimBackSpriteFrameSpeed);
        clone.name = source.name + "_playerRot";
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    private static CharacterSpriteSetFull CloneFullWithRotation(
        CharacterSpriteSetFull source, bool isHina, float rotatePerTime)
    {
        var clone = ScriptableObject.CreateInstance<CharacterSpriteSetFull>();
        clone.Initialize(
            source.MainSprite,
            source.DoNotUseEyeSprite,
            source.EyeSprite,
            source.HairSprite,
            source.BackSprite,
            source.HasPrebakedShadow,
            source.AnimationSpeedMultiplier,
            source.ExtraYOffset,
            isHina,
            rotatePerTime,
            source.DoNotHaveStepVFX,
            source.MoveSpeedMultiplier,
            source.RemovableTrims,
            source.TrimSpritesDisplayFront,
            source.TrimSpritesDisplayBack,
            source.TrimFrontSpriteFrameSpeed,
            source.TrimBackSpriteFrameSpeed);
        clone.name = source.name + "_playerRot";
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    /// <summary>
    /// 将当前皮肤应用到指定 unit 上
    /// </summary>
    public static void ApplyToUnit(this PlayerSkinData playerSkin, CharacterControllerUnit unit)
    {
        if (unit == null) return;
        var skin = playerSkin.ResolveSkinForUnit();
        if (skin != null && playerSkin.RotateOverride.HasValue)
        {
            if (!playerSkin.RotateOverride.Value)
                unit.animator?.StopAllCoroutines();
            unit.m_CurrentVisual = null;
        }
        unit.UpdateCharacterSprite(skin);
    }


    /// <summary>
    /// 获取全部可用皮肤的表格字符串，格式为 "name: CharacterId SelectedType SkinIndex"
    /// </summary>
    public static string GetAllSkinsTable()
    {
        var table = new StringBuilder();
        foreach (var skin in ListAllSkins())
        {
            table.AppendLine($"{skin.name}: {skin.skin.CharacterId} {skin.skin.SelectedType} {skin.skin.SkinIndex}");
        }
        return table.ToString();
    }

    /// <summary>
    /// 列举全部可用皮肤
    /// </summary>
    private static List<(PlayerSkinData skin, string name)> ListAllSkins()
    {
        List<(PlayerSkinData, string)> skins = [];
        skins.AddRange(ListSkinsFromSets(DataBaseCharacter.SelfSpriteSet, -1));
        foreach (int characterId in DataBaseCharacter.SpecialGuestVisual.Keys)
        {
            skins.AddRange(ListSkinsFromSets(DataBaseCharacter.SpecialGuestVisual[characterId]?.CharacterPixel, characterId));
        }

        return skins;
    }

    private static List<(PlayerSkinData skin, string name)> ListSkinsFromSets(CharacterSkinSets skinSets, int characterId)
    {
        if (skinSets is null) return [];
        List<(PlayerSkinData, string)> skins = [];

        skins.Add((new PlayerSkinData
        {
            CharacterId = characterId,
            SelectedType = WireSkinType.Default,
        }, skinSets.defaultSkin?.name ?? "Default"));

        for (var i = 0; i < skinSets.explicits?.Length; i++)
        {
            var skin = skinSets.explicits[i];
            skins.Add((new PlayerSkinData
            {
                CharacterId = characterId,
                SelectedType = WireSkinType.Explicit,
                SkinIndex = i
            }, skin?.name ?? $"Explicit_{i}"));
        }

        for (var i = 0; i < skinSets.dlcs?.Length; i++)
        {
            var skin = skinSets.dlcs[i];
            skins.Add((new PlayerSkinData
            {
                CharacterId = characterId,
                SelectedType = WireSkinType.DLC,
                SkinIndex = i
            }, skin?.name ?? $"DLC_{i}"));
        }

        return skins;
    }
}
