using System;

using Mystia;
using Mystia.Assets;

namespace MetaMystia;

/// <summary>
/// 模组级静态上下文。框架在 <see cref="IInitialization"/> 时注入一次 <see cref="IMod"/>；
/// 此前的日志调用走空实现，避免静态构造期崩溃。
/// </summary>
public static class ModRuntime
{
    private static IMod s_mod;

    /// <summary>宿主交给本模组的句柄；未绑定前为 null。</summary>
    public static IMod Mod => s_mod;

    public static ILog Log => s_mod?.Log ?? SilentLog.Instance;

    /// <summary>模组存储：配置区（文本流）与缓存区（原始流）。</summary>
    public static IModStorage Storage => s_mod?.Storage;

    public static string Id => s_mod?.Id ?? TmiBuildInfo.ModId;

    public static string Version => s_mod?.Version ?? TmiBuildInfo.ModVersion;

    /// <summary>宿主加载本模组的目录；绑定前退回进程基目录。</summary>
    public static string Directory => s_mod?.Directory ?? AppContext.BaseDirectory;

    public static string TargetGameVersion => TmiBuildInfo.TargetGameVersion;

    /// <summary>当前游戏版本；未进入主场景前为空串。</summary>
    public static string GameVersion => Common.LoadingSceneManager.VersionData ?? string.Empty;

    /// <summary>由全局循环在 <c>Setup</c> 时从 <see cref="ICommonServices"/> 取的常驻协程调度器。</summary>
    public static ICoroutineDispatcher Coroutines { get; internal set; }

    /// <summary>由全局循环在 <c>Setup</c> 时从 <see cref="ICommonServices"/> 取的主线程调度器。</summary>
    public static IMainThreadScheduler MainThread { get; internal set; }

    /// <summary>
    /// 由全局循环在 <c>Setup</c> 时从 <see cref="ICommonServices"/> 取的资产工厂：
    /// 贴图／精灵／音频剪辑／像素缓冲都由框架构建，模组只保管句柄。仅主线程可用。
    /// </summary>
    public static IAssetFactory Assets { get; internal set; }

    /// <summary>由全局循环在 <c>Setup</c> 时取的资产登记表：把框架建好的资产按 key 交进游戏资产管线。</summary>
    public static IAssetLocator Locator { get; internal set; }

    /// <summary>由全局循环在 <c>Setup</c> 时取的白天地图构建器；构建与发布都只能在主线程调用。</summary>
    public static IDayMapBuilder MapBuilder { get; internal set; }

    /// <summary>由全局循环在 <c>Setup</c> 时取的对话包目录：按名字查当前游戏装载的对话包。</summary>
    public static IDialogCatalog Dialogs { get; internal set; }

    /// <summary>初始化失败的原因；为 null 表示可用。失败时联机功能主动拒绝进入。</summary>
    public static Exception Failure { get; internal set; }

    /// <summary>初始化是否成功：失败原因为空。联机入口与状态栏都按它判断本模组是否可用。</summary>
    public static bool Ready => Failure is null;

    internal static void Bind(IMod mod) =>
        s_mod = mod ?? throw new ArgumentNullException(nameof(mod));

    private sealed class SilentLog : ILog
    {
        public static readonly SilentLog Instance = new();

        public string Id => TmiBuildInfo.ModId;

        public string Version => TmiBuildInfo.ModVersion;

        public void Debug(string message) { }

        public void Info(string message) { }

        public void Message(string message) { }

        public void Warning(string message) { }

        public void Error(string message) { }

        public void Fatal(string message) { }

        public void Log(LogLevel level, string message) { }

        public ILog Tag(string tag) => this;
    }
}
