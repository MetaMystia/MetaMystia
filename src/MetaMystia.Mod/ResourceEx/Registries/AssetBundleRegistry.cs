using System.Collections.Generic;

using MetaMystia.ResourceEx.AssetManagement;
using MetaMystia.ResourceEx.Models;
using MetaMystia.ResourceEx.Vfx;

namespace MetaMystia.ResourceEx.Registries;

/// <summary>
/// AssetBundle 注册器：在启动时同步预加载资源包声明的全部 AssetBundle，
/// 避免首次使用时卡顿；运行时按 rex:// URI 取用。
/// </summary>
[AutoLog]
public static partial class AssetBundleRegistry
{
    private static readonly List<string> BundleUris = [];
    private static readonly Dictionary<string, VfxBundle> Bundles = [];

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.assetBundles == null) return;

        foreach (var bundleConfig in config.assetBundles)
        {
            if (string.IsNullOrWhiteSpace(bundleConfig?.path))
            {
                Log.LogWarning($"[{packageName}] AssetBundle 缺少 path，跳过加载");
                continue;
            }
            BundleUris.Add(bundleConfig.path);
            Log.LogInfo($"[{packageName}] Loaded config for asset bundle {bundleConfig.path}");
        }
    }

    internal static void LoadAll()
    {
        foreach (var uri in BundleUris)
        {
            if (!RexAssetRegistry.Assets.ContainsKey(uri))
            {
                Log.LogError($"资源包中找不到 AssetBundle: {uri}");
                continue;
            }
            if (VfxBundle.Load(uri) is { } bundle)
                Bundles[uri] = bundle;
        }
    }

    /// <summary>查询已预加载的 AssetBundle；未声明或加载失败时返回 false。</summary>
    public static bool TryGet(string uri, out VfxBundle bundle)
    {
        bundle = null;
        return !string.IsNullOrWhiteSpace(uri) && Bundles.TryGetValue(uri, out bundle);
    }
}
