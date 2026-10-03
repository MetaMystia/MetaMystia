using GameData.Profile;

using Mystia.Listeners;

using UnityEngine;

using MetaMystia.ResourceEx.Registries;

namespace MetaMystia.Data.ResourceEx;

/// <summary>
/// 运行时装扮立绘提供者：取代原 <c>DataBaseCharacter.SetupPortrayalVisual</c> 前缀补丁。
/// 框架在 <c>SetupPortrayalVisual</c> 前询问每个 <see cref="IPortraitProvider"/>，命中的精灵写进
/// <c>Image.overrideSprite</c>（原方法照常执行，原版立绘与动画协程不受影响）。
///
/// 优先级与迁移前一致：
/// 1. <c>/skin</c> 自定义皮肤覆盖（<c>PlayerManager.Local.IsCustomSkinOverride</c>）；
/// 2. ResourceEx 服装立绘（<c>rex://</c> 精灵，取自 <see cref="ClothRegistry"/>）；
/// 3. 未命中则原版逻辑照常。
///
/// 迁移前该前缀会跳过原方法，现在原方法照常执行而 <c>overrideSprite</c> 优先，
/// 可见结果相同；笔记本面板的 <c>ConfigManager.NoteBookSkinPortrait</c> 开关仍由
/// <c>Patches/Compat/NoteBookProfilePannelPatch</c>（缺口）承担。
/// </summary>
[AutoLog]
public sealed partial class ClothPortraitProvider : IPortraitProvider
{
    public bool TryResolvePortrait(ClothesProfile.Clothes clothes, out Sprite sprite)
    {
        sprite = null;
        if (clothes is null)
            return false;

        // /skin 立绘覆盖
        if (PlayerManager.Local?.IsCustomSkinOverride == true)
        {
            sprite = PlayerManager.Local.Skin.ResolvePortraitSprite();
            if (sprite is not null)
                return true;
            Log.Warning("Custom skin override active but portrait sprite is null, falling back to game logic.");
        }

        // ResourceEx 服装立绘覆盖
        if (!ClothRegistry.IsResourceExCloth(clothes.index))
            return false;

        if (ClothRegistry.TryGetClothPortrait(clothes.index, out sprite))
        {
            Log.Info($"Applied ResourceEx cloth portrait for skin ID {clothes.index}");
            return true;
        }

        Log.Info($"ResourceEx cloth ID {clothes.index} has no portrait configured.");
        sprite = null;
        return false;
    }
}
