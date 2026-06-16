using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Common.CharacterUtility;
using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;
using MetaMystia.Network.Utilities;
using MetaMystia.Protocol.Data;
using MetaMystia.Protocol.Enums;
using SgrYuki.Utils;

namespace MetaMystia;

[AutoLog]
public static partial class PlayerSkin
{
    public static CharacterSpriteSetCompact ResolveSkin(this PlayerSkinData data)
    {
        if (!string.IsNullOrEmpty(data.NetSkinName))
        {
            if (NetSkinManager.TryGet(data.NetSkinName, out var net))
                return net;
            NetSkinManager.RequestSkin(data.NetSkinName);
            return DataBaseCharacter.FallbackFullPixel;
        }

        if (data.CharacterId == -1)
        {
            return ResolveSkin(DataBaseCharacter.SelfSpriteSet, data.SelectedType, data.SkinIndex);
        }

        if (DataBaseCharacter.SpecialGuestVisual.ContainsKey(data.CharacterId))
        {
            return ResolveSkin(DataBaseCharacter.SpecialGuestVisual[data.CharacterId]?.CharacterPixel, data.SelectedType, data.SkinIndex);
        }

        Log.Warning($"CharacterId {data.CharacterId} not found in SpecialGuestVisual, returning Fallback skin");
        return DataBaseCharacter.FallbackFullPixel;
    }

    private static CharacterSpriteSetCompact ResolveSkin(
        CharacterSkinSets skinSets, SkinSelectedType type, int index)
    {
        if (skinSets is null) return null;

        var gameType = EnumConverter.ToGame(type);
        return gameType switch
        {
            CharacterSkinSets.SelectedType.Default => skinSets.defaultSkin,
            CharacterSkinSets.SelectedType.Explicit => (index >= 0 && index < skinSets.explicits.Length)
                ? skinSets.explicits[index] : skinSets.defaultSkin,
            CharacterSkinSets.SelectedType.DLC => (index >= 0 && index < skinSets.dlcs.Length)
                ? skinSets.dlcs[index] : skinSets.defaultSkin,
            _ => skinSets.defaultSkin
        };
    }

    public static CharacterPortrayal ResolveSpecialPortrait(this PlayerSkinData data)
    {
        if (DataBaseCharacter.SpecialGuestVisual.ContainsKey(data.CharacterId))
        {
            return DataBaseCharacter.SpecialGuestVisual[data.CharacterId]?.CharacterPortrayal?.defaultPortrayal;
        }

        return DataBaseCharacter.FallbackPortrayal;
    }

    private static Sprite ResolvePortraitFromSelf(SkinSelectedType type, int index)
    {
        var gameType = EnumConverter.ToGame(type);
        if (gameType == CharacterSkinSets.SelectedType.Default)
        {
            return DataBaseCharacter.SelfPortrayalSet?.defaultPortrayal.m_VisualAssetAtlasReference[0]?.Asset
                ?.TryCast<Sprite>();
        }

        return DataBaseCore.Clothes
            .ToList()
            .Where(c => c.Value.skinIndex.index == index && c.Value.skinIndex.selectedType == gameType)
            .Select(c => ResolveSelfPortrayalFromClothes(c.Value))
            .FirstOrDefault() ?? ResolvePortraitFromSelf(SkinSelectedType.Default, 0);
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

    extension(PlayerSkinData data)
    {
        public Sprite ResolvePortraitSprite()
        {
            if (data.CharacterId == -1)
            {
                return ResolvePortraitFromSelf(data.SelectedType, data.SkinIndex);
            }

            var portrayal = ResolveSpecialPortrait(data);
            if (portrayal == null) return null;

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
                Log.Warning($"Failed to load portrait sprite: {e.Message}");
                return null;
            }
        }

        public void SetSkin(int characterId, CharacterSkinSets.SelectedType selectedType, int skinIndex)
        {
            data.CharacterId = characterId;
            data.SelectedType = EnumConverter.ToProtocol(selectedType);
            data.SkinIndex = skinIndex;
            data.NetSkinName = null;
        }

        public void SetNetSkin(string name)
        {
            data.NetSkinName = string.IsNullOrEmpty(name) ? null : name;
        }

        public void ApplyToUnit(CharacterControllerUnit unit)
            => unit?.UpdateCharacterSprite(ResolveSkin(data));
    }

    public static string GetAllSkinsTable()
    {
        var table = new StringBuilder();
        foreach (var skin in ListAllSkins())
        {
            table.AppendLine($"{skin.name}: {skin.skin.CharacterId} {skin.skin.SelectedType} {skin.skin.SkinIndex}");
        }
        return table.ToString();
    }

    private static List<(PlayerSkinData skin, string name)> ListAllSkins()
    {
        List<(PlayerSkinData, string)> skins = [];
        skins.AddRange(ListSkinsFromSets(DataBaseCharacter.SelfSpriteSet, -1));
        foreach (var (characterId, value) in DataBaseCharacter.SpecialGuestVisual)
        {
            skins.AddRange(ListSkinsFromSets(value?.CharacterPixel, characterId));
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
            SelectedType = SkinSelectedType.Default,
        }, skinSets.defaultSkin?.name ?? "Default"));

        for (var i = 0; i < skinSets.explicits?.Length; i++)
        {
            var skin = skinSets.explicits[i];
            skins.Add((new PlayerSkinData
            {
                CharacterId = characterId,
                SelectedType = SkinSelectedType.Explicit,
                SkinIndex = i
            }, skin?.name ?? $"Explicit_{i}"));
        }

        for (var i = 0; i < skinSets.dlcs?.Length; i++)
        {
            var skin = skinSets.dlcs[i];
            skins.Add((new PlayerSkinData
            {
                CharacterId = characterId,
                SelectedType = SkinSelectedType.DLC,
                SkinIndex = i
            }, skin?.name ?? $"DLC_{i}"));
        }

        return skins;
    }
}
