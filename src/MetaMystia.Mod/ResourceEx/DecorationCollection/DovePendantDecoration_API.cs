using Il2CppInterop.Runtime.Injection;
using UnityEngine;

using GameData.Core.Collections;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;
using GameData.Profile;

using MetaMystia.ResourceEx.DecorationCollection;

namespace MetaMystia;

public static partial class ResourceExManager
{
    /// <summary>
    /// 小鸽子挂坠的装饰 id；落在 9000+ 段以避让原生占用区（原生 8 个装饰为低值 id，符卡已占用 9001-9005）。
    /// </summary>
    private const int DovePendantDecorationId = 9006;

    /// <summary>
    /// 小鸽子挂坠在展示柜中显示的图标占位路径；正式美术资源待补，先以占位 Sprite 跑通注册链路。
    /// </summary>
    private const string DovePendantSpriteUri = "rex://ResourceExample/assets/Decoration/9006.png";

    /// <summary>
    /// il2cpp 类型注入幂等标记：同一进程生命周期内只注入一次，重复注入会抛异常。
    /// </summary>
    private static bool _dovePendantTypeInjected;

    /// <summary>
    /// 注册小鸽子挂坠装饰，使其出现在展示柜并被勾选生效。
    /// 同一 Decoration 实例须同时写入 Items 与 Decorations 两字典，以满足展示柜列举与 IsDecoration/RefDecorations 的要求。
    /// </summary>
    public static void RegisterDovePendantDecoration()
    {
        if (!_dovePendantTypeInjected)
        {
            ClassInjector.RegisterTypeInIl2Cpp<DovePendantDecoration>();
            _dovePendantTypeInjected = true;
        }

        var specialBuff = ScriptableObject.CreateInstance<DovePendantDecoration>();
        TryGetSprite(DovePendantSpriteUri, out var overrideSprite);
        if (overrideSprite == null)
        {
            Log.Warning($"[DovePendant] 图标资源加载失败，使用空占位：{DovePendantSpriteUri}");
        }

        var decoration = new Decoration(
            DovePendantDecorationId,
            overrideSprite,
            specialBuff,
            Decoration.DecorationType.Outdoor,
            System.Array.Empty<int>());

        DataBaseCore.Items[DovePendantDecorationId] = decoration;
        DataBaseCore.Decorations[DovePendantDecorationId] = decoration;

        Log.Info($"[DovePendant] 已注册小鸽子挂坠（id={DovePendantDecorationId}）");
    }

    /// <summary>
    /// 注册小鸽子挂坠的展示文案（名称与描述），写入 DataBaseLanguage.Items 供展示柜读取。
    /// </summary>
    public static void RegisterDovePendantDecorationLanguage()
    {
        TryGetSprite(DovePendantSpriteUri, out var sprite);
        DataBaseLanguage.Items[DovePendantDecorationId] = new ObjectLanguageBase(
            name: "小鸽子挂坠",
            Description: "激活后，白天时玩家移动速度增加0.12；夜间营业时移动速度增加20%，并增加20%的伙伴工作效率与伙伴移动速度。",
            visual: sprite);
        Log.Info($"[DovePendant] 已注册文案（id={DovePendantDecorationId}）");
    }
}
