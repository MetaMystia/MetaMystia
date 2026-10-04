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

    // 「Caller」变体多打一个调用者名字。名字由编译器填（[CallerMemberName]），不再走调用栈——
    // 原来的 GetCallerName(3) 取的正是调用本方法的那个成员，结果一致而无需反射。
    public void DebugCaller(string msg, bool withTag = true, [CallerMemberName] string caller = null) => Inner.Debug(CallerText(caller, withTag, msg));
    public void InfoCaller(string msg, bool withTag = true, [CallerMemberName] string caller = null) => Inner.Info(CallerText(caller, withTag, msg));
    public void MessageCaller(string msg, bool withTag = true, [CallerMemberName] string caller = null) => Inner.Message(CallerText(caller, withTag, msg));
    public void WarningCaller(string msg, bool withTag = true, [CallerMemberName] string caller = null) => Inner.Warning(CallerText(caller, withTag, msg));
    public void ErrorCaller(string msg, bool withTag = true, [CallerMemberName] string caller = null) => Inner.Error(CallerText(caller, withTag, msg));
    public void FatalCaller(string msg, bool withTag = true, [CallerMemberName] string caller = null) => Inner.Fatal(CallerText(caller, withTag, msg));

    private string CallerText(string caller, bool withTag, string msg) =>
        $"[{GetTime()}] {TagString(withTag)}[{caller}] {msg}";

    public void LogDebug(string msg, bool withTag = true) => Debug(msg, withTag);
    public void LogInfo(string msg, bool withTag = true) => Info(msg, withTag);
    public void LogMessage(string msg, bool withTag = true) => Message(msg, withTag);
    public void LogWarning(string msg, bool withTag = true) => Warning(msg, withTag);
    public void LogError(string msg, bool withTag = true) => Error(msg, withTag);
    public void LogFatal(string msg, bool withTag = true) => Fatal(msg, withTag);

    /// <summary>把当前调用栈整段写进日志；只在需要排查"谁把它逼到这里"时用（唯一调用点：GuestFSM.Kill）。</summary>
    public void LogStacktrace()
    {
        var lines = Environment.StackTrace.Split([Environment.NewLine], StringSplitOptions.None);
        var text = new System.Text.StringBuilder();
        // 本方法与其直接调用者（LogWrapper）不入日志。
        for (var i = 2; i < lines.Length; i++)
            text.AppendLine(lines[i]);
        Inner.Info(text.ToString());
    }
}
