using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;

using MetaMystia.ResourceEx.AssetManagement;
using Object = UnityEngine.Object;

namespace MetaMystia.ResourceEx.Vfx;

/// <summary>
/// 一个特效 AssetBundle：同步加载并按名称缓存 prefab，负责播放、全屏遮罩与结束。
/// 由资源包的 assetBundles 声明，启动时经 AssetBundleRegistry 预加载，任何功能按 URI 取用。
/// </summary>
[AutoLog]
public sealed partial class VfxBundle
{
    /// <summary>全屏遮罩：高于夜间 HUD（Canvas 3000），低于操作面板 UIPannelRoot（8000）与 RootCanvas。</summary>
    private const int OverlaySortingOrder = 7999;
    private const float OverlayFadeSeconds = 0.8f;
    /// <summary>停止发射后，等待已发出的粒子消散的时间。</summary>
    private const float ParticleDrainSeconds = 6f;

    private readonly Dictionary<string, GameObject> _prefabs = [];

    /// <summary><see cref="AssetBundle.LoadFromStream"/> 要求流的存活期长于 AssetBundle，故持有到进程结束。</summary>
    private readonly Il2CppSystem.IO.MemoryStream _stream;

    /// <summary>资源包保持加载（不调用 Unload），prefab 依赖其中的贴图与材质。</summary>
    private VfxBundle(AssetBundle bundle, Il2CppSystem.IO.MemoryStream stream)
    {
        _stream = stream;
        if (bundle == null)
            return;

        // 互操作里 LoadAllAssets 被裁掉（游戏未调用），只剩异步变体；读取尚未完成的 allAssets
        // 会阻塞到加载结束，因此这里仍是启动期同步载入，与原来的 LoadAllAssets 等价。
        // 仍用 Type 重载而非泛型 LoadAllAssetsAsync<T>：泛型要走游戏未实例化的 ConvertObjects<GameObject>。
        foreach (var obj in bundle.LoadAllAssetsAsync(Il2CppType.Of<GameObject>()).allAssets)
        {
            var prefab = obj.TryCast<GameObject>();
            if (prefab == null)
                continue;
            // 托管引用挡不住切场景时的 Resources.UnloadUnusedAssets，须显式标记。
            prefab.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _prefabs[prefab.name] = prefab;
        }
    }

    public static VfxBundle Load(string uri)
    {
        // 互操作里没有 AssetBundle.LoadFromMemory，改用等价的同步 LoadFromStream；
        // 它要的是游戏的 System.IO.Stream，所以用 il2cpp 侧的 MemoryStream 包一层。
        var stream = new Il2CppSystem.IO.MemoryStream(RexAssetRegistry.Assets[uri].Bytes);
        var bundle = AssetBundle.LoadFromStream(stream, 0);
        if (bundle == null)
        {
            Log.LogError($"AssetBundle 加载失败: {uri}");
            return null;
        }

        var vfx = new VfxBundle(bundle, stream);
        Log.LogInfo($"{uri}: 已加载 {vfx._prefabs.Count} 个特效 prefab");
        return vfx;
    }

    public bool Contains(string name) => _prefabs.ContainsKey(name);

    /// <summary>持续特效，需用 <see cref="Stop"/> 结束。</summary>
    public GameObject Play(string name, Vector3? position = null)
    {
        if (!_prefabs.TryGetValue(name, out var prefab))
        {
            Log.LogWarning($"找不到特效 {name}");
            return null;
        }

        var instance = Object.Instantiate(prefab);
        if (position.HasValue)
            instance.transform.position = position.Value;
        return instance;
    }

    /// <summary>一次性特效，<paramref name="lifetime"/> 秒后销毁。</summary>
    public GameObject PlayOneShot(string name, Vector3 position, float lifetime = ParticleDrainSeconds)
    {
        var instance = Play(name, position);
        if (instance != null)
            Object.Destroy(instance, lifetime);
        return instance;
    }

    /// <summary>
    /// 全屏遮罩。世界空间渲染器盖不住 Screen Space Overlay 画布，因此把 prefab 中的各遮罩层
    /// 按排序转成 Overlay 画布上的全屏 RawImage，并淡入。需用 <see cref="Stop"/> 结束。
    /// </summary>
    public GameObject PlayScreenOverlay(string name)
    {
        if (!_prefabs.TryGetValue(name, out var prefab))
        {
            Log.LogWarning($"找不到特效 {name}");
            return null;
        }

        var root = new GameObject(name);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;
        var group = root.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true).OrderBy(r => r.sortingOrder))
        {
            var layer = new GameObject(renderer.name);
            layer.transform.SetParent(root.transform, false);

            var image = layer.AddComponent<RawImage>();
            image.texture = renderer.sprite.texture;
            // 与 SpellVfx/AlphaParticle 着色器一致：贴图 × _TintColor × 顶点色。
            image.color = renderer.sharedMaterial.GetColor("_TintColor") * renderer.color;
            image.raycastTarget = false;

            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 挂在进程级的 owner 上：淡入不再随场景销毁而停止（遮罩本身随场景销毁，
        // Fade 里的 null 检查随即结束协程）。
        var coroutines = ModRuntime.Coroutines;
        coroutines.StartOn(coroutines.Owner, _ => Fade(group, 1f));
        return root;
    }

    /// <summary>结束特效：遮罩淡出后销毁；粒子停止发射，已发出的部分消散后销毁。</summary>
    public void Stop(GameObject instance)
    {
        if (instance == null)
            return;

        var group = instance.GetComponent<CanvasGroup>();
        if (group != null)
        {
            // 同上：进程级 owner，淡出不再随场景销毁而停止；group 消失时 Fade 自行结束。
            var coroutines = ModRuntime.Coroutines;
            coroutines.StartOn(coroutines.Owner, _ => Fade(group, 0f, destroyAfter: true));
            return;
        }

        foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Object.Destroy(instance, ParticleDrainSeconds);
    }

    /// <summary>按游戏时间渐变遮罩透明度，暂停时随之停止；遮罩随场景销毁时直接结束。</summary>
    private static IEnumerator Fade(CanvasGroup group, float to, bool destroyAfter = false)
    {
        var from = group.alpha;
        for (var t = 0f; t < OverlayFadeSeconds; t += Time.deltaTime)
        {
            if (group == null)
                yield break;
            group.alpha = Mathf.Lerp(from, to, t / OverlayFadeSeconds);
            yield return null;
        }
        if (group == null)
            yield break;

        group.alpha = to;
        if (destroyAfter)
            Object.Destroy(group.gameObject);
    }
}
