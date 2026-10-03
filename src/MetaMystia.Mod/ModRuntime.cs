using System;

using Mystia;

namespace MetaMystia;

/// <summary>
/// 模组级静态上下文。框架在 <c>IPostInitialize</c> 时注入一次；此前的日志调用走空实现，避免静态构造期崩溃。
/// </summary>
public static class ModRuntime
{
    private static IModContext s_context;

    public static IModContext Context => s_context;

    public static ILog Log => s_context?.Log ?? SilentLog.Instance;

    public static IMainThreadScheduler MainThread => s_context?.MainThread;

    public static IGamePaths Paths => s_context?.Paths;

    public static IIl2CppComponentHost Components => s_context?.Components;

    public static IModCache Cache => s_context?.Cache;

    public static IModConfigSource ConfigSource => s_context?.Config;

    /// <summary>由全局循环在 Setup 时注入的协程调度器。</summary>
    public static ICoroutineDispatcher Coroutines { get; internal set; }

    public static string Id => TmiBuildInfo.ModId;

    public static string Version => TmiBuildInfo.ModVersion;

    public static string TargetGameVersion => TmiBuildInfo.TargetGameVersion;

    /// <summary>当前游戏版本；未进入主场景前为空串。</summary>
    public static string GameVersion => Common.LoadingSceneManager.VersionData ?? string.Empty;

    internal static void Bind(IModContext context) =>
        s_context = context ?? throw new ArgumentNullException(nameof(context));

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
