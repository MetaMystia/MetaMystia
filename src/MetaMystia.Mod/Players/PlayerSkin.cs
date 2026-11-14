using MemoryPack;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Common.CharacterUtility;
using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;

using MetaMystia.ResourceEx.Registries;
using SgrYuki.Utils;

namespace MetaMystia;

[MemoryPackable]
[AutoLog]
public partial class PlayerSkin
{
    public int CharacterId = -1; // -1 means Mystia
    public CharacterSkinSets.SelectedType SelectedType = CharacterSkinSets.SelectedType.Default;
    public int SkinIndex = 0;

    /// <summary>
    /// 在线皮肤名（皮肤站标识）。非空时优先使用，由 NetSkinManager 负责异步拉取与解析；
    /// 未就绪时返回 Fallback 占位，下载完成后会自动刷新。为空则回落到原有 CharacterId/Type/Index 流程。
    /// </summary>
    public string NetSkinName = null;

    /// <summary>
    /// 旋转覆盖。null = 使用皮肤默认值；true = 强制开启旋转；false = 强制关闭旋转。
    /// </summary>
    public bool? RotateOverride = null;

    [MemoryPackIgnore]
    private CharacterSpriteSetCompact rotationSource;
    [MemoryPackIgnore]
    private CharacterSpriteSetCompact rotationSkin;

    /// <summary>
    /// 解析 CharacterSpriteSetCompact
    /// </summary>
    public CharacterSpriteSetCompact ResolveSkin()
    {
        if (!string.IsNullOrEmpty(NetSkinName))
        {
            if (NetSkinManager.TryGet(NetSkinName, out var net))
                return net;
            // 未就绪：触发异步加载，先返回 Fallback 占位
            NetSkinManager.RequestSkin(NetSkinName);
            return DataBaseCharacter.FallbackFullPixel;
        }

        if (CharacterId == -1)
        {
            return ResolveSkin(DataBaseCharacter.SelfSpriteSet, SelectedType, SkinIndex);
        }

        if (DataBaseCharacter.SpecialGuestVisual.ContainsKey(CharacterId))
        {
            return ResolveSkin(DataBaseCharacter.SpecialGuestVisual[CharacterId]?.CharacterPixel, SelectedType, SkinIndex);
        }

        Log.Warning($"CharacterId {CharacterId} not found in SpecialGuestVisual, returning Fallback skin");
        return DataBaseCharacter.FallbackFullPixel;
    }

    private static CharacterSpriteSetCompact ResolveSkin(
        CharacterSkinSets skinSets, CharacterSkinSets.SelectedType type, int index)
    {
        if (skinSets == null) return DataBaseCharacter.FallbackFullPixel;

        var skin = type switch
        {
            CharacterSkinSets.SelectedType.Default => skinSets.defaultSkin,
            CharacterSkinSets.SelectedType.Explicit => (index >= 0 && index < skinSets.explicits?.Length)
                ? skinSets.explicits[index] : skinSets.defaultSkin,
            CharacterSkinSets.SelectedType.DLC => (index >= 0 && index < skinSets.dlcs?.Length)
                ? skinSets.dlcs[index] : skinSets.defaultSkin,
            _ => skinSets.defaultSkin
        };
        return skin != null ? skin : skinSets.GetDefaultPixel(false);
    }

    /// <summary>
    /// 解析当前皮肤对应的 CharacterPortrayal（立绘配置），专门用于 SpecialGuest
    /// </summary>
    public CharacterPortrayal ResolveSpecialPortrait()
    {
        if (DataBaseCharacter.SpecialGuestVisual.ContainsKey(CharacterId))
        {
            return DataBaseCharacter.SpecialGuestVisual[CharacterId]?.CharacterPortrayal?.defaultPortrayal;
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
    public Sprite ResolvePortraitSprite()
    {
        if (CharacterId == -1)
        {
            return ResolvePortraitFromSelf(SelectedType, SkinIndex);
        }

        var portrayal = ResolveSpecialPortrait();
        if (portrayal == null) return null;

        // 优先：ResourceEx 自定义立绘
        if (SpecialGuestRegistry.TryGetSpecialGuestCustomPortrayal(portrayal, out var customSprites, out var faceInNoteBook))
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
            Log.Warning($"Failed to load portrait sprite: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 设定皮肤
    /// </summary>
    /// <param name="characterId"></param>
    /// <param name="selectedType"></param>
    /// <param name="skinIndex"></param>
    public void SetSkin(int characterId, CharacterSkinSets.SelectedType selectedType, int skinIndex)
    {
        CharacterId = characterId;
        SelectedType = selectedType;
        SkinIndex = skinIndex;
        NetSkinName = null;
    }

    /// <summary>
    /// 设定在线皮肤名。非空时由 NetSkinManager 异步拉取与解析。
    /// </summary>
    public void SetNetSkin(string name)
    {
        NetSkinName = string.IsNullOrEmpty(name) ? null : name;
    }

    private CharacterSpriteSetCompact ResolveSkinForUnit()
    {
        var source = ResolveSkin();
        if (source == null || !RotateOverride.HasValue)
        {
            ReleaseRuntimeSkin();
            return source;
        }
        if (rotationSkin != null && rotationSource == source && rotationSkin.IsHina == RotateOverride.Value)
            return rotationSkin;

        ReleaseRuntimeSkin();
        // Unity 克隆保留原生派生类型及全部图层，不依赖 Interop 包装类型判断。
        rotationSkin = Object.Instantiate(source);
        rotationSkin.hideFlags = HideFlags.HideAndDontSave;
        rotationSkin.isHina = RotateOverride.Value;
        rotationSkin.rotatePerTime = 0.15f;
        rotationSource = source;
        return rotationSkin;
    }

    internal void ReleaseRuntimeSkin()
    {
        if (rotationSkin != null) Object.Destroy(rotationSkin);
        rotationSkin = null;
        rotationSource = null;
    }

    public void ApplyToUnit(CharacterControllerUnit unit)
    {
        if (unit == null) return;
        var skin = ResolveSkinForUnit();
        if (skin == null) return;
        if (unit.m_CurrentVisual == skin)
        {
            if (!skin.IsHina || unit.animator == null || unit.animator.hinaRotateCoroutine != null) return;
            unit.m_CurrentVisual = null;
        }
        StopRotation(unit.animator);
        unit.UpdateCharacterSprite(skin);
    }

    internal static void StopRotation(CharacterAnimator animator)
    {
        if (animator == null || animator.hinaRotateCoroutine == null) return;
        animator.StopCoroutine(animator.hinaRotateCoroutine);
        animator.hinaRotateCoroutine = null;
    }

    /// <summary>
    /// 获取全部可用皮肤的表格字符串，格式为 "name: CharacterId SelectedType SkinIndex"
    /// </summary>
    /// <returns></returns>
    public static string GetAllSkinsTable()
    {
        var table = new StringBuilder();
        foreach (var skin in ListAllSkins())
        {
            var displayName = GetSkinDisplayName(skin.skin.CharacterId, skin.name);
            table.AppendLine($"{displayName}: {skin.skin.CharacterId} {skin.skin.SelectedType} {skin.skin.SkinIndex}");
        }
        return table.ToString();
    }

    // The base game's built-in skin asset names contain two Chinese typos.
    // Keep the correction local to `/skin list` instead of mutating shared assets.
    private static string GetSkinDisplayName(int characterId, string name)
    {
        return (characterId, name) switch
        {
            (2003, "古地名觉") => "古明地觉",
            (2006, "古地名恋") => "古明地恋",
            _ => name
        };
    }

    /// <summary>
    /// 列举全部可用皮肤
    /// </summary>
    /// <returns></returns>
    private static List<(PlayerSkin skin, string name)> ListAllSkins()
    {
        List<(PlayerSkin, string)> skins = [];
        skins.AddRange(ListSkinsFromSets(DataBaseCharacter.SelfSpriteSet, -1));
        foreach (int characterId in DataBaseCharacter.SpecialGuestVisual.Keys)
        {
            skins.AddRange(ListSkinsFromSets(DataBaseCharacter.SpecialGuestVisual[characterId]?.CharacterPixel, characterId));
        }

        return skins;
    }

    private static List<(PlayerSkin skin, string name)> ListSkinsFromSets(CharacterSkinSets skinSets, int characterId)
    {
        if (skinSets is null) return [];
        List<(PlayerSkin, string)> skins = [];

        skins.Add((new PlayerSkin
        {
            CharacterId = characterId,
            SelectedType = CharacterSkinSets.SelectedType.Default,
        }, skinSets.defaultSkin?.name ?? "Default"));

        for (var i = 0; i < skinSets.explicits?.Length; i++)
        {
            var skin = skinSets.explicits[i];
            skins.Add((new PlayerSkin
            {
                CharacterId = characterId,
                SelectedType = CharacterSkinSets.SelectedType.Explicit,
                SkinIndex = i
            }, skin?.name ?? $"Explicit_{i}"));
        }

        for (var i = 0; i < skinSets.dlcs?.Length; i++)
        {
            var skin = skinSets.dlcs[i];
            skins.Add((new PlayerSkin
            {
                CharacterId = characterId,
                SelectedType = CharacterSkinSets.SelectedType.DLC,
                SkinIndex = i
            }, skin?.name ?? $"DLC_{i}"));
        }

        return skins;
    }
}
