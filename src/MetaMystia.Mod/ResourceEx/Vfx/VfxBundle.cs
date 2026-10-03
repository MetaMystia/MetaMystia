using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.UI;

using NightScene.EventUtility;

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

    /// <summary>资源包保持加载（不调用 Unload），prefab 依赖其中的贴图与材质。</summary>
    private VfxBundle(AssetBundle bundle)
    {
        if (bundle == null)
            return;

        // 泛型 LoadAllAssets<T> 依赖游戏未实例化的 ConvertObjects<GameObject>，会 unstripping 失败。
        foreach (var obj in bundle.LoadAllAssets(Il2CppType.Of<GameObject>()))
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
        var bundle = AssetBundle.LoadFromMemory(RexAssetRegistry.Assets[uri].Bytes);
        if (bundle == null)
        {
            Log.LogError($"AssetBundle 加载失败: {uri}");
            return null;
        }

        var vfx = new VfxBundle(bundle);
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

        ModRuntime.Coroutines.StartOn(EventManager.Instance, _ => Fade(group, 1f));
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
            ModRuntime.Coroutines.StartOn(EventManager.Instance, _ => Fade(group, 0f, destroyAfter: true));
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
