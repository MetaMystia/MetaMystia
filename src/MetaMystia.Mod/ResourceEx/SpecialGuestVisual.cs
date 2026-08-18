using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using GameData.Profile;
using MetaMystia.ResourceEx.Models;
using UnityEngine;

namespace MetaMystia;

public static partial class ResourceExManager
{
    // TODO: 目前只能支持单套自定义立绘
    private static readonly Dictionary<CharacterPortrayal, CharacterConfig> CustomSpecialGuestPortrayalToConfig = [];

    private static void RegisterSpecialGuestPortrayal(CharacterPortrayal portrayal, CharacterConfig characterConfig)
    {
        if (CustomSpecialGuestPortrayalToConfig.TryGetValue(portrayal, out var existingConfig))
        {
            Log.LogWarning(
                $"Duplicate registration for portrayal '{portrayal}'. " +
                $"Existing characterId: {existingConfig?.id}, new characterId: {characterConfig?.id}. " +
                "Overwriting existing mapping.");
        }

        CustomSpecialGuestPortrayalToConfig[portrayal] = characterConfig;
    }

    private static readonly Dictionary<CharacterConfig, Sprite[]> LoadedSpritesCache = [];

    public static bool TryGetSpecialGuestCustomPortrayal(CharacterPortrayal portrayal, [NotNullWhen(true)] out Sprite[] portrayalSprite)
    {
        return TryGetSpecialGuestCustomPortrayal(portrayal, out portrayalSprite, out _);
    }

    /// <summary>
    /// 尝试将一个角色立绘映射到自定义立绘资源
    /// </summary>
    /// <param name="portrayal">角色立绘</param>
    /// <param name="portrayalSprite">如果方法返回 <see langword="true"/>，则包含映射到的自定义立绘资源；否则为 <see langword="null"/>。</param>
    /// <param name="faceInNoteBook">Config中的默认立绘ID，如果未指定则为0</param>
    /// <returns>如果方法返回 <see langword="true"/>，则表示成功映射到自定义立绘资源；否则为 <see langword="false"/>。</returns>
    /// <remarks> 如果Config中部分立绘提供了无效的路径，则返回数组的对应Index位为null </remarks>
    public static bool TryGetSpecialGuestCustomPortrayal(CharacterPortrayal portrayal, [NotNullWhen(true)] out Sprite[] portrayalSprite, out int faceInNoteBook)
    {
        portrayalSprite = null;
        faceInNoteBook = 0;
        if (!CustomSpecialGuestPortrayalToConfig.TryGetValue(portrayal, out var config))
        {
            return false;
        }

        faceInNoteBook = config.faceInNoteBook ?? 0;

        if (LoadedSpritesCache.TryGetValue(config, out var sprites))
        {
            portrayalSprite = sprites;
            return true;
        }
        if (config.portraits is null) return false;
        if (config.portraits.Count == 0) return false;

        var portraits = new Sprite[config.portraits.Count];
        for (var index = 0; index < config.portraits.Count; index++)
        {
            var portraitConfig = config.portraits[index];
            if (string.IsNullOrEmpty(portraitConfig.path))
            {
                Log.LogError($"Portrait config for characterId {config.id}, pid {portraitConfig.pid} has empty path, skipping.");
                portraits[index] = null;
                continue;
            }

            Log.LogInfo(
                $"Getting portrait sprite for characterId {config.id}, pid {portraitConfig.pid} from path {portraitConfig.path}");
            if (ResourceExManager.TryGetSprite(portraitConfig.path, out var sprite))
            {
                portraits[index] = sprite;
            }
        }

        LoadedSpritesCache[config] = portraits;
        portrayalSprite = portraits;
        return true;
    }
}
