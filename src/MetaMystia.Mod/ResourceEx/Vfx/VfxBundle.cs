using System.Collections;
using System.Collections.Generic;

using Mystia;
using Mystia.Assets;
using Mystia.Scenes;

using MetaMystia.ResourceEx.AssetManagement;

using NumericsVector3 = Mystia.Numerics.Vector3;

namespace MetaMystia.ResourceEx.Vfx;

/// <summary>
/// 一个特效 AssetBundle：同步加载并按名称缓存 prefab；播放、全屏遮罩与结束都交给框架的
/// <see cref="IPresentationServices"/>，本类只把「包的 URI + 预制件名」映射成框架资产键。
/// 由资源包的 assetBundles 声明，启动时经 AssetBundleRegistry 预加载，任何功能按 URI 取用。
/// <para>
/// 包的字节由框架的 <c>IAssetFactory.TryOpenBundle</c> 打开成一个不透明句柄：包里的预制件名单在返回时
/// 已完整，登记给表现面按名字进行，本类因此不再持有任何引擎对象，也不再需要 Unity 或互操作类型。
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
    /// 查的是包自身的清单（开包时就已完整读好，不等待），不是框架的 <c>IAssetLocator.IsRegistered</c>：
    /// 预制件要到第一次播放才登记进框架资产表，登记之前那个键必然查不到，启动期的依赖检查会全部落空。
    /// 语义与迁移前逐字一致。
    /// </remarks>
    public bool Contains(string name) => _bundle.ContainsPrefab(name);

    /// <summary>持续特效，需用 <see cref="Stop"/> 结束。</summary>
    public IVfxHandle? Play(IPresentationServices services, string name, NumericsVector3? position = null)
    {
        if (!TryPrepare(name, out var key, out var own))
            return null;

        // 不给位置时用预制体自身的坐标：迁移前是实例化后根本不写位置，而框架的 PlayVfx 总要写位置。
        return services.PlayVfx(key, position ?? own);
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
        if (!TryPrepare(name, out var key, out _))
            return null;
        return services.PlayScreenOverlay(key);
    }

    /// <summary>结束特效：遮罩淡出后销毁；粒子停止发射，已发出的部分排水后销毁。重复停止无副作用。</summary>
    public void Stop(IVfxHandle? handle) => handle?.Stop();

    /// <summary>预制件的框架资产键：资产键是全进程共用的，故带上本包的 URI 以免撞名。</summary>
    private string KeyOf(string name) => $"{_uri}/{name}";

    /// <summary>包句柄：预制件名单与登记都经它，本类不持有任何引擎对象；包与流由框架持有到进程结束。</summary>
    private readonly AssetBundleHandle _bundle;

    private VfxBundle(string uri, AssetBundleHandle bundle)
    {
        _uri = uri;
        _bundle = bundle;
    }

    public static VfxBundle? Load(string uri)
    {
        if (!ModRuntime.CommonServices.Assets.TryOpenBundle(RexAssetRegistry.Assets[uri].Bytes, out var bundle))
        {
            Log.LogError($"AssetBundle 加载失败: {uri}");
            return null;
        }

        Log.LogInfo($"{uri}: 已加载 {bundle.PrefabNames.Count} 个特效 prefab");
        return new VfxBundle(uri, bundle);
    }

    /// <summary>
    /// 取预制件自身的位置，并确保它已交进框架资产表（框架克隆一份、隐藏后登记）。
    /// 登记被拒（键不合规或资产表拒绝）时与原「找不到特效」一样不播放，只是日志不同。
    /// </summary>
    private bool TryPrepare(string name, out string key, out NumericsVector3 position)
    {
        key = KeyOf(name);
        if (!_bundle.TryGetPrefabPosition(name, out position))
        {
            Log.LogWarning($"找不到特效 {name}");
            return false;
        }
        if (_registered.Contains(key))
            return true;

        if (!_bundle.TryRegisterPrefab(key, name))
        {
            Log.LogWarning($"特效 {name} 未能登记进框架资产表");
            return false;
        }
        _registered.Add(key);
        return true;
    }
}
