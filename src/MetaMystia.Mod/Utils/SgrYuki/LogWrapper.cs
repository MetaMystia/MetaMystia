using System;
using SgrYuki.Utils;

namespace SgrYuki;

public sealed class LogWrapper
{
    public readonly BepInEx.Logging.ManualLogSource _inner;
    public readonly string _class_tag;

    public LogWrapper(BepInEx.Logging.ManualLogSource inner, string tag)
    {
        _inner = inner;
        _class_tag = tag;
    }

    private static string GetTime() => DateTime.Now.ToString("HH:mm:ss.fff");

    private string TagString(bool withTag) => withTag ? $"[{_class_tag}] " : "";

    public void Debug(string msg, bool withTag = true) => _inner.LogDebug($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Info(string msg, bool withTag = true) => _inner.LogInfo($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Message(string msg, bool withTag = true) => _inner.LogMessage($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Warning(string msg, bool withTag = true) => _inner.LogWarning($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Error(string msg, bool withTag = true) => _inner.LogError($"[{GetTime()}] {TagString(withTag)}{msg}");
    public void Fatal(string msg, bool withTag = true) => _inner.LogFatal($"[{GetTime()}] {TagString(withTag)}{msg}");

    public void DebugCaller(string msg, bool withTag = true) => _inner.LogDebug($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void InfoCaller(string msg, bool withTag = true) => _inner.LogInfo($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void MessageCaller(string msg, bool withTag = true) => _inner.LogMessage($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void WarningCaller(string msg, bool withTag = true) => _inner.LogWarning($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void ErrorCaller(string msg, bool withTag = true) => _inner.LogError($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");
    public void FatalCaller(string msg, bool withTag = true) => _inner.LogFatal($"[{GetTime()}] {TagString(withTag)}[{GetOuterCallerName()}] {msg}");

    public void LogDebug(string msg, bool withTag = true) => Debug(msg, withTag);
    public void LogInfo(string msg, bool withTag = true) => Info(msg, withTag);
    public void LogMessage(string msg, bool withTag = true) => Message(msg, withTag);
    public void LogWarning(string msg, bool withTag = true) => Warning(msg, withTag);
    public void LogError(string msg, bool withTag = true) => Error(msg, withTag);
    public void LogFatal(string msg, bool withTag = true) => Fatal(msg, withTag);

    public void LogStacktrace() => Functional.LogStacktrace(_inner);
    public string GetCallerName([System.Runtime.CompilerServices.CallerMemberName] string caller = null) => caller;
    public string GetOuterCallerName() => Functional.GetCallerName(3);
}
