using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

using Common.CharacterUtility;
using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;

using MemoryPack;

using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;

using Mystia.Assets;

using SgrYuki.Utils;

namespace MetaMystia;

/// <summary>
/// 一名玩家的皮肤：游戏自带的像素集（<see cref="CharacterId"/>／<see cref="SelectedType"/>／<see cref="SkinIndex"/>）
/// 或在线皮肤名（<see cref="NetSkinName"/>，由 <see cref="NetSkinManager"/> 异步拉取）。
///
/// <para><b>在线皮肤</b>：帧集由 <c>IAssetFactory.TryCreateCharacterSpriteSet</c> 造成游戏的角色像素集，
/// 套用走 <c>IPresentationServices.ApplyCharacterSprite</c>（<c>restart: true</c> 即原实现「先丢掉当前外观、
/// 停掉动画协程再换装」的语义）。旋转覆盖用 <see cref="CharacterSpriteSetStyle"/> 重建一份集，
/// 与原实现克隆 ScriptableObject 并改 <c>IsHina</c> 等价。</para>
///
/// <para><b>游戏自带像素集</b>：这类皮肤仍直接交给角色（框架的 <c>ApplyCharacterSprite</c> 只套用框架自建的
/// 集）。旋转覆盖需要一份重建的集：框架的 <c>IAssetFactory.TryUnwrapCharacterSpriteSet</c> 已能把游戏自带的
/// 集拆成帧与样式，<c>TryCreateCharacterSpriteSet</c> 再按 <see cref="CharacterSpriteSetStyle"/> 重建，但重建
/// 出的集其裁剪（trims）取游戏 fallback 像素集而非该皮肤自己的，因此本轮未接线，先记警告。</para>
/// <para>立绘出入的是框架的不透明句柄（<c>IPortraitProvider</c> 已代理化），游戏自带那张由
/// <c>IAssetFactory.TryWrapSprite</c> 包成句柄。</para>
/// </summary>
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

    // 在线皮肤的框架精灵集缓存：与来源 NetSkin 及旋转覆盖一一对应，任一变化都要重建。
    [MemoryPackIgnore]
    private CharacterSpriteSetHandle? _netSpriteSet;
    [MemoryPackIgnore]
    private NetSkin? _netSpriteSetSource;
    [MemoryPackIgnore]
    private bool? _netSpriteSetRotate;
    [MemoryPackIgnore]
    private bool? _reportedRotationGap;

    private void InvalidateSpriteSetCache()
    {
        _netSpriteSet = null;
        _netSpriteSetSource = null;
        _netSpriteSetRotate = null;
    }

    /// <summary>
    /// 解析游戏自带的 CharacterSpriteSetCompact。
    /// 在线皮肤没有游戏像素集：未就绪时返回 Fallback 占位并触发异步拉取，就绪后由
    /// <see cref="ApplyToUnit"/> 走框架的 <c>ApplyCharacterSprite</c> 套用真正的在线皮肤。
    /// </summary>
    public CharacterSpriteSetCompact ResolveSkin()
    {
        if (!string.IsNullOrEmpty(NetSkinName))
        {
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
    public CharacterPortrayal ResolveSpecialPortrait()
    {
        if (DataBaseCharacter.SpecialGuestVisual.ContainsKey(CharacterId))
        {
            return DataBaseCharacter.SpecialGuestVisual[CharacterId]?.CharacterPortrayal?.defaultPortrayal;
        }

        return DataBaseCharacter.FallbackPortrayal;
    }

    /// <summary>
    /// 获取当前皮肤的立绘句柄（使用默认表情，索引 0）
    /// 优先级: ResourceEx 自定义立绘 &gt; 已加载的 Addressable 资源 &gt; 同步加载 Addressable
    /// <para>
    /// 自建的资源包立绘本来就是句柄；游戏自带的那张（皮肤资源引用里的 Sprite）由框架的
    /// <c>IAssetFactory.TryWrapSprite</c> 包成句柄，出入因此统一是句柄，引擎类型只在这一处出现。
    /// </para>
    /// </summary>
    public SpriteHandle? ResolvePortraitSprite() => ResolvePortraitObject() switch
    {
        SpriteHandle handle => handle,
        { } gameArt when ModRuntime.Assets.TryWrapSprite(gameArt, out var wrapped) => wrapped,
        _ => null,
    };

    /// <summary>立绘的资源对象（引擎 Object，可能是 Sprite 也可能是别的图集资源）；没有则 null。</summary>
    private object ResolvePortraitObject()
    {
        if (CharacterId == -1)
        {
            return ResolveSelfPortrait(SelectedType, SkinIndex);
        }

        if (ResolveSpecialPortrait() is not { } portrayal) return null;

        // 优先：ResourceEx 自定义立绘
        if (SpecialGuestRegistry.TryGetSpecialGuestCustomPortrayal(portrayal, out var customSprites, out var faceInNoteBook))
        {
            var index = (faceInNoteBook >= 0 && faceInNoteBook < customSprites.Length) ? faceInNoteBook : 0;
            return customSprites[index];
        }

        var refs = portrayal.m_VisualAssetAtlasReference;
        if (refs == null || refs.Length == 0) return null;

        var portraitIndex = (portrayal.faceInNoteBook >= 0 && portrayal.faceInNoteBook < refs.Length)
            ? portrayal.faceInNoteBook
            : 0;
        if (refs[portraitIndex] == null) return null;

        // 引擎对象的空判定走 UnityEngine.Object 自己的运算符（已加载/已销毁都能判），此处不写类型名。
        if (refs[portraitIndex].Asset != null) return refs[portraitIndex].Asset;

        try
        {
            return refs[portraitIndex].LoadAssetAsync().WaitForCompletion();
        }
        catch (System.Exception e)
        {
            Log.Warning($"Failed to load portrait sprite: {e.Message}");
            return null;
        }
    }

    private static object ResolveSelfPortrait(CharacterSkinSets.SelectedType type, int index)
    {
        if (type == CharacterSkinSets.SelectedType.Default)
        {
            return DataBaseCharacter.SelfPortrayalSet?.defaultPortrayal.m_VisualAssetAtlasReference[0]?.Asset;
        }

        return DataBaseCore.Clothes
            .ToList()
            .Where(c => c.Value.skinIndex.index == index && c.Value.skinIndex.selectedType == type)
            .Select(c => ResolveClothesPortrait(c.Value))
            .FirstOrDefault() ?? ResolveSelfPortrait(CharacterSkinSets.SelectedType.Default, 0);
    }

    private static object ResolveClothesPortrait(ClothesProfile.Clothes clothes)
    {
        if (clothes is null || !clothes.IsValidVisual)
            return null;

        if (clothes.m_OverrideVisualAsset.Asset != null)
            return clothes.m_OverrideVisualAsset.Asset;

        try
        {
            return clothes.m_OverrideVisualAsset.LoadAssetAsync().WaitForCompletion();
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
        InvalidateSpriteSetCache();
    }

    /// <summary>
    /// 设定在线皮肤名。非空时由 NetSkinManager 异步拉取与解析。
    /// </summary>
    public void SetNetSkin(string name)
    {
        NetSkinName = string.IsNullOrEmpty(name) ? null : name;
        InvalidateSpriteSetCache();
    }

    /// <summary>
    /// 设置旋转覆盖
    /// </summary>
    public void SetRotate(bool? value)
    {
        RotateOverride = value;
        InvalidateSpriteSetCache();
    }

    /// <summary>
    /// 当前在线皮肤的框架精灵集（含旋转覆盖变体）。未就绪时触发异步拉取并返回 false；
    /// 不是在线皮肤时也返回 false。
    /// </summary>
    private bool TryResolveNetSpriteSet([NotNullWhen(true)] out CharacterSpriteSetHandle set)
    {
        set = null;
        if (string.IsNullOrEmpty(NetSkinName)) return false;

        if (!NetSkinManager.TryGet(NetSkinName, out var net))
        {
            NetSkinManager.RequestSkin(NetSkinName);
            return false;
        }

        if (_netSpriteSet is not null
            && ReferenceEquals(_netSpriteSetSource, net)
            && _netSpriteSetRotate == RotateOverride)
        {
            set = _netSpriteSet;
            return true;
        }

        if (!net.TryCreateSet(RotateOverride, out var built))
        {
            Log.Warning($"网络皮肤「{NetSkinName}」的精灵集构建失败（旋转覆盖 {RotateOverride?.ToString() ?? "无"}）");
            return false;
        }

        _netSpriteSetSource = net;
        _netSpriteSetRotate = RotateOverride;
        _netSpriteSet = built;
        set = built;
        return true;
    }

    /// <summary>
    /// 将当前皮肤应用到指定角色。
    /// <para>
    /// 在线皮肤：套用走 <c>IPresentationServices.ApplyCharacterSprite</c>（<c>restart</c> 语义），
    /// 该服务只在场景循环的服务窗口内可用，因此动作排进 <see cref="ScenePresentation"/>，由场景循环逐帧执行。
    /// </para>
    /// <para>
    /// 游戏自带皮肤：框架的 <c>ApplyCharacterSprite</c> 只接受框架自建精灵集，故仍直接交给角色；旋转覆盖
    /// 需要按游戏自带帧重建一份集（框架已具备拆解与重建两个入口，见类注释），本轮未接线，只记一次警告。
    /// </para>
    /// </summary>
    /// <param name="unit"></param>
    public void ApplyToUnit(CharacterControllerUnit unit)
    {
        if (unit == null) return;

        if (!string.IsNullOrEmpty(NetSkinName))
        {
            if (TryResolveNetSpriteSet(out var set))
            {
                var name = NetSkinName;
                ScenePresentation.Enqueue(services =>
                {
                    if (services.BindCharacter(unit) is not { } character)
                    {
                        Log.Warning($"网络皮肤「{name}」套用失败：角色不可用");
                        return;
                    }

                    if (!services.ApplyCharacterSprite(character, set, restart: true))
                        Log.Warning($"网络皮肤「{name}」套用失败");
                });
                return;
            }

            // 未就绪：先用 Fallback 游戏像素集占位（与原实现一致），下载完成后 NetSkinManager 会重新刷新。
        }
        else if (RotateOverride.HasValue && _reportedRotationGap != RotateOverride)
        {
            // 旋转覆盖只对框架自建精灵集可表达（CharacterSpriteSetStyle）；游戏自带像素集没有重建入口。
            _reportedRotationGap = RotateOverride;
            Log.Warning("旋转覆盖需要重建像素集，而游戏自带像素集没有重建入口（框架缺口），本次忽略。");
        }

        if (ResolveSkin() is { } gameSkin)
            unit.UpdateCharacterSprite(gameSkin);
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
