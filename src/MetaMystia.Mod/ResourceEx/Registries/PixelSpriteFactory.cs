using System.Collections.Generic;

using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 像素精灵（CharacterSpriteSetCompact/Full）构建工具：从配置生成游戏像素精灵对象。
/// </summary>
[AutoLog]
public static partial class PixelSpriteFactory
{
    private const int CharacterPixelSpriteSize = 64;
    private static readonly Vector2 CharacterPixelPivot = new Vector2(0.5f, 0.0f);

    private static readonly Dictionary<string, CharacterSpriteSetCompact> _characterSpriteSets = new();

    public static CharacterSpriteSetCompact GetCharacterSpriteSetCompact(string name)
    {
        return _characterSpriteSets.TryGetValue(name, out var spriteSet) ? spriteSet : null;
    }

    public static CharacterSpriteSetCompact MakePixel(CharacterSpriteSetCompactConfig pixelConfig)
    {
        var template = DataBaseCharacter.FallbackCompactPixel;

        var pixel = ScriptableObject.CreateInstance<CharacterSpriteSetCompact>();

        var mainSprites = CopySpriteArray(template.MainSprite);
        var eyeSprites = CopySpriteArray(template.EyeSprite);

        ApplySprites(mainSprites, pixelConfig.mainSprite);
        ApplySprites(eyeSprites, pixelConfig.eyeSprite);

        pixel.Initialize(
            mainSprites,
            template.DoNotUseEyeSprite,
            eyeSprites,
            template.HasPrebakedShadow,
            template.AnimationSpeedMultiplier,
            template.ExtraYOffset,
            template.IsHina,
            template.RotatePerTime,
            template.DoNotHaveStepVFX,
            template.MoveSpeedMultiplier,
            template.RemovableTrims,
            template.TrimSpritesDisplayFront,
            template.TrimSpritesDisplayBack,
            template.TrimFrontSpriteFrameSpeed,
            template.TrimBackSpriteFrameSpeed
        );

        pixel.name = pixelConfig.name;
        _characterSpriteSets[pixelConfig.name] = pixel;
        return pixel;
    }

    public static CharacterSpriteSetFull MakePixelFull(CharacterSpriteSetFullConfig pixelConfig)
    {
        var template = DataBaseCharacter.FallbackFullPixel;

        var mainSprites = CopySpriteArray(template.MainSprite);
        var eyeSprites = CopySpriteArray(template.EyeSprite);
        var hairSprites = CopySpriteArray(template.HairSprite);
        var backSprites = CopySpriteArray(template.BackSprite);

        ApplySprites(mainSprites, pixelConfig.mainSprite);
        ApplySprites(eyeSprites, pixelConfig.eyeSprite);
        ApplySprites(hairSprites, pixelConfig.hairSprite);
        ApplySprites(backSprites, pixelConfig.backSprite);

        var pixelFull = ScriptableObject.CreateInstance<CharacterSpriteSetFull>();
        pixelFull.Initialize(
            mainSprites,
            template.DoNotUseEyeSprite,
            eyeSprites,
            hairSprites,
            backSprites,
            template.HasPrebakedShadow,
            template.AnimationSpeedMultiplier,
            template.ExtraYOffset,
            template.IsHina,
            template.RotatePerTime,
            template.DoNotHaveStepVFX,
            template.MoveSpeedMultiplier,
            template.RemovableTrims,
            template.TrimSpritesDisplayFront,
            template.TrimSpritesDisplayBack,
            0f,
            0f
        );
        return pixelFull;
    }


    private static Il2CppReferenceArray<Sprite> CopySpriteArray(Il2CppReferenceArray<Sprite> source)
    {
        if (source == null) return null;
        var newArray = new Il2CppReferenceArray<Sprite>(source.Length);
        for (int i = 0; i < source.Length; i++)
        {
            newArray[i] = source[i];
        }

        return newArray;
    }

    private static void ApplySprites(Il2CppReferenceArray<Sprite> targetArray, List<string> spriteUris)
    {
        if (spriteUris == null) return;

        if (targetArray == null)
        {
            Log.LogError($"Target array is null but sprite paths were provided. Cannot apply sprites.");
            return;
        }

        if (spriteUris.Count != targetArray.Length)
        {
            Log.LogError(
                $"Sprite count mismatch! Expected {targetArray.Length}, got {spriteUris.Count}. Refusing to load sprites.");
            return;
        }

        for (int i = 0; i < spriteUris.Count; i++)
        {
            string uri = spriteUris[i];
            if (string.IsNullOrEmpty(uri)) continue;

            if (!RexAssetRegistry.TryGetSprite(uri, out var source))
                continue;

            var sprite = CreateCharacterPixelSprite(source);

            if (sprite != null)
            {
                targetArray[i] = sprite;
            }
        }
    }

    private static Sprite CreateCharacterPixelSprite(Sprite source)
    {
        if (source == null || source.texture == null)
            return null;

        var rect = source.rect;
        int sourceWidth = Mathf.RoundToInt(rect.width);
        int sourceHeight = Mathf.RoundToInt(rect.height);
        int copyWidth = Mathf.Min(sourceWidth, CharacterPixelSpriteSize);
        int copyHeight = Mathf.Min(sourceHeight, CharacterPixelSpriteSize);

        var texture = new Texture2D(CharacterPixelSpriteSize, CharacterPixelSpriteSize, TextureFormat.RGBA32, false);
        var clear = new Color[CharacterPixelSpriteSize * CharacterPixelSpriteSize];
        for (int i = 0; i < clear.Length; i++)
            clear[i] = Color.clear;
        texture.SetPixels(clear);

        int dstX = Mathf.Max(0, (CharacterPixelSpriteSize - sourceWidth) / 2);
        int dstY = Mathf.Max(0, (CharacterPixelSpriteSize - sourceHeight) / 2);
        int srcX = Mathf.RoundToInt(rect.x) + Mathf.Max(0, (sourceWidth - CharacterPixelSpriteSize) / 2);
        int srcY = Mathf.RoundToInt(rect.y) + Mathf.Max(0, (sourceHeight - CharacterPixelSpriteSize) / 2);

        texture.SetPixels(dstX, dstY, copyWidth, copyHeight, source.texture.GetPixels(srcX, srcY, copyWidth, copyHeight));
        texture.Apply();
        texture.name = source.name;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.hideFlags = HideFlags.HideAndDontSave;

        var sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, CharacterPixelSpriteSize, CharacterPixelSpriteSize),
            CharacterPixelPivot,
            48f);
        sprite.name = source.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
}
