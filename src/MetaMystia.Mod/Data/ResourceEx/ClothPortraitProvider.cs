using System.Diagnostics.CodeAnalysis;

using Mystia.Assets;
using Mystia.Listeners;

using MetaMystia.ResourceEx.Registries;

namespace MetaMystia.Data.ResourceEx;

/// <summary>
/// 运行时装扮立绘提供者：取代原 <c>DataBaseCharacter.SetupPortrayalVisual</c> 前缀补丁。
/// 框架在 <c>SetupPortrayalVisual</c> 前询问每个 <see cref="IPortraitProvider"/>，命中的精灵写进
/// <c>Image.overrideSprite</c>（原方法照常执行，原版立绘与动画协程不受影响）。
///
/// 优先级与迁移前一致：
/// 1. <c>/skin</c> 自定义皮肤覆盖（<c>PlayerManager.Local.IsCustomSkinOverride</c>）；
/// 2. ResourceEx 服装立绘（<c>rex://</c> 精灵句柄，取自 <see cref="ClothRegistry"/>）；
/// 3. 未命中则原版逻辑照常。
///
/// 迁移前该前缀会跳过原方法，现在原方法照常执行而 <c>overrideSprite</c> 优先，
/// 可见结果相同；笔记本档案页由 <c>ConfigManager.NoteBookSkinPortrait</c> 单独把关
/// （框架把「哪个面板在要立绘」作为 <see cref="PortraitTarget"/> 交给提供者）。
/// </summary>
[AutoLog]
public sealed partial class ClothPortraitProvider : IPortraitProvider
{
    public bool TryResolvePortrait(int clothIndex, PortraitTarget target, [NotNullWhen(true)] out SpriteHandle? portrait)
    {
        portrait = null;

        // 笔记本档案页由 Experiment/NoteBookSkinPortrait 单独把关：关掉时该页仍用游戏自己的立绘。
        // 迁移前这一页由独立补丁承担，开关是它唯一的入口；两页现在共用同一条链，开关因此在这里过滤。
        if (target == PortraitTarget.NoteBook && !ConfigManager.NoteBookSkinPortrait.Value)
            return false;

        // /skin 立绘覆盖
        if (PlayerManager.Local?.IsCustomSkinOverride == true)
        {
            portrait = PlayerManager.Local.Skin.ResolvePortraitSprite();
            if (portrait is not null)
                return true;
            Log.Warning("Custom skin override active but portrait sprite is null, falling back to game logic.");
        }

        // ResourceEx 服装立绘覆盖
        if (!ClothRegistry.IsResourceExCloth(clothIndex))
            return false;

        if (ClothRegistry.TryGetClothPortrait(clothIndex, out portrait))
        {
            Log.Info($"Applied ResourceEx cloth portrait for skin ID {clothIndex}");
            return true;
        }

        Log.Info($"ResourceEx cloth ID {clothIndex} has no portrait configured.");
        return false;
    }
}
