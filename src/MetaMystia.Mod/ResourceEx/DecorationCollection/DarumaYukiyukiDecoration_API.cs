using Il2CppInterop.Runtime.Injection;
using UnityEngine;

using GameData.Core.Collections;
using GameData.CoreLanguage;
using GameData.CoreLanguage.Collections;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.DecorationCollection;

namespace MetaMystia;

public static partial class ResourceExManager
{
    /// <summary>
    /// 达摩雪雪的装饰 id，落在 9000+ 段以避让原生占用区。
    /// </summary>
    private const int DarumaYukiyukiDecorationId = 9007;

    /// <summary>
    /// 达摩雪雪在展示柜中显示的图标资源路径。
    /// </summary>
    private const string DarumaYukiyukiSpriteUri = "rex://ResourceExample/assets/Decoration/9007.png";

    /// <summary>
    /// il2cpp 类型注入幂等标记：同一进程生命周期内只注入一次，重复注入会抛异常。
    /// </summary>
    private static bool _darumaYukiyukiTypeInjected;

    /// <summary>
    /// 注册达摩雪雪装饰，使其出现在展示柜并被勾选生效。
    /// </summary>
    public static void RegisterDarumaYukiyukiDecoration()
    {
        if (!_darumaYukiyukiTypeInjected)
        {
            ClassInjector.RegisterTypeInIl2Cpp<DarumaYukiyukiDecoration>();
            _darumaYukiyukiTypeInjected = true;
        }

        var specialBuff = ScriptableObject.CreateInstance<DarumaYukiyukiDecoration>();
        RexAssetRegistry.TryGetSprite(DarumaYukiyukiSpriteUri, out var overrideSprite);
        if (overrideSprite == null)
        {
            Log.Warning($"[DarumaYukiyuki] 图标资源加载失败，使用空占位：{DarumaYukiyukiSpriteUri}");
        }

        var decoration = new Decoration(
            DarumaYukiyukiDecorationId,
            overrideSprite,
            specialBuff,
            Decoration.DecorationType.Outdoor,
            System.Array.Empty<int>());

        DataBaseCore.Items[DarumaYukiyukiDecorationId] = decoration;
        DataBaseCore.Decorations[DarumaYukiyukiDecorationId] = decoration;

        Log.Info($"[DarumaYukiyuki] 已注册达摩雪雪（id={DarumaYukiyukiDecorationId}）");
    }

    /// <summary>
    /// 注册达摩雪雪的展示文案（名称与描述），写入 DataBaseLanguage.Items 供展示柜读取。
    /// </summary>
    public static void RegisterDarumaYukiyukiDecorationLanguage()
    {
        RexAssetRegistry.TryGetSprite(DarumaYukiyukiSpriteUri, out var sprite);
        DataBaseLanguage.Items[DarumaYukiyukiDecorationId] = new ObjectLanguageBase(
            name: "达摩雪雪",
            Description: "营业时若因任何效果获得黑暗料理，获得30秒「不倒翁七転八起」效果；持续时间内再次获得相同效果时刷新累计时间、不叠加。不倒翁七転八起：制作料理时有20%概率返还食材，且烹饪时间降低20%。",
            visual: sprite);
        Log.Info($"[DarumaYukiyuki] 已注册文案（id={DarumaYukiyukiDecorationId}）");
    }
}
