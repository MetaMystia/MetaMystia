using System.Collections;
using System.Collections.Generic;

using Il2CppInterop.Runtime;
using Mystia;
using Mystia.Scenes;
using UnityEngine;

using MetaMystia.ResourceEx.AssetManagement;

using NumericsVector3 = Mystia.Numerics.Vector3;

namespace MetaMystia.ResourceEx.Vfx;

/// <summary>
/// 一个特效 AssetBundle：同步加载并按名称缓存 prefab；播放、全屏遮罩与结束都交给框架的
/// <see cref="IPresentationServices"/>，本类只把「包的 URI + 预制件名」映射成框架资产键。
/// 由资源包的 assetBundles 声明，启动时经 AssetBundleRegistry 预加载，任何功能按 URI 取用。
/// <para>
/// <b>保留段</b>（见文件末尾「保留段」）：AssetBundle 的加载、预制体本身的读取，以及预制体到框架资产键的
/// 桥接，仍直接使用 UnityEngine 与互操作类型——框架没有 AssetBundle 面（全仓没有任何 AssetBundle 入口），
/// 这一段无法迁移。请不要为它造兼容层、加 <c>#pragma</c> 或改用反射；这段里的诊断是既定保留项，不是待办。
/// </para>
/// </summary>
[AutoLog]
public sealed partial class VfxBundle
{
    /// <summary>
    /// 一次性特效的默认存活时间：到点调 <see cref="Stop"/>。
    /// 迁移前这个 6 秒是「延时销毁引擎对象」的延时；框架的句柄没有「到期自毁」参数，
    /// 改由模组的进程级调度器延时停止（按游戏时间计时，与迁移前的延时口径一致）。
    /// </summary>
    private const float OneShotLifetimeSeconds = 6f;

    /// <summary>该 AssetBundle 的 URI，用来组交进框架资产表的预制件键。</summary>
    private readonly string _uri;

    /// <summary>已交进框架资产表的预制件键：重复登记会替换旧的并上报，故每个键只登记一次。</summary>
    private readonly HashSet<string> _registered = [];

    /// <summary>该包是否声明了名为 <paramref name="name"/> 的预制件。</summary>
    /// <remarks>
    /// 查的是包自身的清单，不是框架的 <c>IAssetLocator.IsRegistered</c>：预制件要到第一次播放才登记进框架
    /// 资产表（<c>TryRegisterPrefab</c> 要求处于场景服务窗口内，而 <see cref="Load"/> 跑在数据表注入期、
    /// 窗口之外），登记之前那个键必然查不到，启动期的依赖检查会全部落空。语义与迁移前逐字一致。
    /// </remarks>
    public bool Contains(string name) => _prefabs.ContainsKey(name);

    /// <summary>持续特效，需用 <see cref="Stop"/> 结束。</summary>
    public IVfxHandle? Play(IPresentationServices services, string name, NumericsVector3? position = null)
    {
        if (!TryPrepare(services, name, out var key))
            return null;

        // 不给位置时用预制体自身的坐标：迁移前是实例化后根本不写位置，而框架的 PlayVfx 总要写位置。
        return services.PlayVfx(key, position ?? PrefabPosition(name));
    }

    /// <summary>一次性特效：<paramref name="lifetime"/> 秒后停止发射，已发出的粒子排水消散后销毁。</summary>
    public IVfxHandle? PlayOneShot(
        IPresentationServices services,
        string name,
        NumericsVector3 position,
        float lifetime = OneShotLifetimeSeconds)
    {
        var handle = Play(services, name, position);
        if (handle is null)
            return null;

        var coroutines = ModRuntime.Coroutines;
        if (coroutines is null)
        {
            Log.LogWarning($"特效 {name} 没有可用的协程调度器，不会自动结束");
            return handle;
        }
        coroutines.StartOn(coroutines.Owner, dispatcher => StopLater(dispatcher, handle, lifetime));
        return handle;

        // 框架的 Stop 自带淡出与 6 秒排水，所以这里只负责到点调它一次。
        static IEnumerator StopLater(ICoroutineDispatcher dispatcher, IVfxHandle target, float seconds)
        {
            yield return dispatcher.AfterSeconds(seconds);
            target.Stop();
        }
    }

    /// <summary>
    /// 全屏遮罩，需用 <see cref="Stop"/> 结束。预制件里的各遮罩层按排序铺成 Overlay 画布上的全屏图片
    /// 并淡入，这项工作交给框架的 <c>PlayScreenOverlay</c>（同样的 order 7999、0.8 秒淡入、
    /// <c>_TintColor</c> × 顶点色混合）。
    /// </summary>
    public IVfxHandle? PlayScreenOverlay(IPresentationServices services, string name)
    {
        if (!TryPrepare(services, name, out var key))
            return null;
        return services.PlayScreenOverlay(key);
    }

    /// <summary>结束特效：遮罩淡出后销毁；粒子停止发射，已发出的部分排水后销毁。重复停止无副作用。</summary>
    public void Stop(IVfxHandle? handle) => handle?.Stop();

    /// <summary>预制件的框架资产键：资产键是全进程共用的，故带上本包的 URI 以免撞名。</summary>
    private string KeyOf(string name) => $"{_uri}/{name}";

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // 保留段：AssetBundle、预制体，以及预制体到框架资产键的桥接。
    // 框架没有 AssetBundle 面，这一段仍直接持有 UnityEngine/互操作类型；这里的诊断是保留项，不是待办。
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>按名称缓存的预制件。</summary>
    private readonly Dictionary<string, GameObject> _prefabs = [];

    /// <summary><see cref="AssetBundle.LoadFromStream"/> 要求流的存活期长于 AssetBundle，故持有到进程结束。</summary>
    private readonly Il2CppSystem.IO.MemoryStream _stream;

    /// <summary>资源包保持加载（不调用 Unload），prefab 依赖其中的贴图与材质。</summary>
    private VfxBundle(string uri, AssetBundle bundle, Il2CppSystem.IO.MemoryStream stream)
    {
        _uri = uri;
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

    public static VfxBundle? Load(string uri)
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

        var vfx = new VfxBundle(uri, bundle, stream);
        Log.LogInfo($"{uri}: 已加载 {vfx._prefabs.Count} 个特效 prefab");
        return vfx;
    }

    /// <summary>取预制件并确保它已交进框架资产表（框架克隆一份、隐藏后登记），返回它的框架资产键。</summary>
    private bool TryPrepare(IPresentationServices services, string name, out string key)
    {
        key = KeyOf(name);
        if (!_prefabs.TryGetValue(name, out var prefab))
        {
            Log.LogWarning($"找不到特效 {name}");
            return false;
        }
        if (_registered.Contains(key))
            return true;

        // 登记被拒（键不合规或资产表拒绝）时与原「找不到特效」一样不播放，只是日志不同。
        if (!services.TryRegisterPrefab(key, prefab))
        {
            Log.LogWarning($"特效 {name} 未能登记进框架资产表");
            return false;
        }
        _registered.Add(key);
        return true;
    }

    /// <summary>预制体自身的世界坐标：不给位置时用它保持预制体的原位置。</summary>
    private NumericsVector3 PrefabPosition(string name)
    {
        var position = _prefabs[name].transform.position;
        return new NumericsVector3(position.x, position.y, position.z);
    }
}
