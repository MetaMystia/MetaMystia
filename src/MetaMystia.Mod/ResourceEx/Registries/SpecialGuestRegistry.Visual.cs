using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;

using Mystia.Assets;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// 资源包角色立绘查询。
/// 迁移后 <c>DataBaseCharacter.SpecialGuestVisual</c> 由框架按 <c>SpecialGuestData.Portraits</c> 建立
/// （游戏侧四个 <c>CharacterPortrayal.Load*</c> 由框架接管），模组不再自建 <see cref="CharacterPortrayal"/> 实例，
/// 因此这里改为「查这个 portrayal 属于哪个资源包角色」，再交出包内立绘句柄，供玩家皮肤等取用。
/// </summary>
public static partial class SpecialGuestRegistry
{
    // TODO: 目前只能支持单套自定义立绘
    private static readonly Dictionary<CharacterConfig, SpriteHandle?[]> LoadedSpritesCache = [];

    public static bool TryGetSpecialGuestCustomPortrayal(CharacterPortrayal portrayal, [NotNullWhen(true)] out SpriteHandle[] portrayalSprite) =>
        TryGetSpecialGuestCustomPortrayal(portrayal, out portrayalSprite, out _);

    /// <summary>
    /// 尝试把一个角色立绘映射到资源包内的自定义立绘
    /// </summary>
    /// <param name="portrayal">角色立绘</param>
    /// <param name="portrayalSprite">如果方法返回 <see langword="true"/>，则包含映射到的自定义立绘句柄；否则为 <see langword="null"/>。</param>
    /// <param name="faceInNoteBook">Config 中的默认立绘下标，未指定时为 0</param>
    /// <returns>成功映射到资源包立绘时为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
    /// <remarks> 如果 Config 中部分立绘提供了无效的路径，则返回数组的对应 Index 位为 null </remarks>
    public static bool TryGetSpecialGuestCustomPortrayal(CharacterPortrayal portrayal, [NotNullWhen(true)] out SpriteHandle[] portrayalSprite, out int faceInNoteBook)
    {
        portrayalSprite = null;
        faceInNoteBook = 0;
        if (portrayal is null)
            return false;

        var config = FindConfig(portrayal);
        if (config is null)
            return false;

        faceInNoteBook = config.faceInNoteBook ?? 0;

        if (LoadedSpritesCache.TryGetValue(config, out var cached))
        {
            portrayalSprite = cached;
            return cached.Length > 0;
        }

        if (config.portraits is null || config.portraits.Count == 0)
            return false;

        var portraits = new SpriteHandle?[config.portraits.Count];
        var any = false;
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
            if (RexAssetRegistry.TryGetSprite(portraitConfig.path, out var sprite))
            {
                portraits[index] = sprite;
                any = true;
            }
        }

        LoadedSpritesCache[config] = portraits;
        portrayalSprite = portraits;
        return any;
    }

    /// <summary>
    /// 反查 portrayal 属于哪个资源包角色：框架建立的 portrayal 就放在游戏表
    /// <c>DataBaseCharacter.SpecialGuestVisual</c> 的 <c>GuestProfilePair</c> 里，比对对象指针即可。
    /// </summary>
    private static CharacterConfig FindConfig(CharacterPortrayal portrayal)
    {
        var visual = DataBaseCharacter.SpecialGuestVisual;
        if (visual is null)
            return null;

        foreach (var config in GetAllCharacterConfigs())
        {
            if (string.IsNullOrEmpty(config.label) || !visual.TryGetValue(config.id, out var pair) || pair is null)
                continue;
            if (pair.CharacterPortrayal?.defaultPortrayal?.Pointer == portrayal.Pointer)
                return config;
        }

        return null;
    }
}
