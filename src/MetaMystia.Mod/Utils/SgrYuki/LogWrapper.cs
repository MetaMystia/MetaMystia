using System;
using System.Runtime.CompilerServices;

using Mystia;

using SgrYuki.Utils;

namespace SgrYuki;

/// <summary>
/// 模组日志入口的薄封装：加时间戳与类标签，实际输出交给框架的 <see cref="ILog"/>。
/// 由 <c>MetaMystia.Generators</c> 为每个 <c>[AutoLog]</c> 类生成使用点。
/// </summary>
public sealed class LogWrapper
{
    public readonly ILog Inner;
    public readonly string ClassTag;

    public LogWrapper(ILog inner, string tag)
    {
        Inner = inner;
        ClassTag = tag;
    }

    private static string GetTime() => DateTime.Now.ToString("HH:mm:ss.fff");

    private string TagString(bool withTag) => withTag ? $"[{ClassTag}] " : "";

    public void Debug(string msg, bool withTag = true) => Inner.Debug($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Info(string msg, bool withTag = true) => Inner.Info($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Message(string msg, bool withTag = true) => Inner.Message($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Warning(string msg, bool withTag = true) => Inner.Warning($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Error(string msg, bool withTag = true) => Inner.Error($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Fatal(string msg, bool withTag = true) => Inner.Fatal($"[{GetTime()}] {TagString(withTag)}{msg}");

    public void DebugCaller(string msg, bool withTag = true) => Inner.Debug($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void InfoCaller(string msg, bool withTag = true) => Inner.Info($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void MessageCaller(string msg, bool withTag = true) => Inner.Message($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void WarningCaller(string msg, bool withTag = true) => Inner.Warning($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void ErrorCaller(string msg, bool withTag = true) => Inner.Error($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void FatalCaller(string msg, bool withTag = true) => Inner.Fatal($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");

    public void LogDebug(string msg, bool withTag = true) => Debug(msg, withTag);
    public void LogInfo(string msg, bool withTag = true) => Info(msg, withTag);
    public void LogMessage(string msg, bool withTag = true) => Message(msg, withTag);
    public void LogWarning(string msg, bool withTag = true) => Warning(msg, withTag);
    public void LogError(string msg, bool withTag = true) => Error(msg, withTag);
    public void LogFatal(string msg, bool withTag = true) => Fatal(msg, withTag);

    public void LogStacktrace() => Functional.LogStacktrace(Inner);

    public string GetCallerName([CallerMemberName] string caller = null) => caller;

    public string GetOuterCallerName() => Functional.GetCallerName(3);
}
