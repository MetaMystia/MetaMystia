using System;
using System.Runtime.CompilerServices;

using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;

namespace MetaMystia;

/// <summary>标签取调用处的源文件名，到第一个 '.' 为止。级别未被监听时不求值插值表达式。</summary>
public static class Log
{
    public static void Debug(BepInExDebugLogInterpolatedStringHandler message, [CallerFilePath] string file = "") => Write(LogLevel.Debug, message, file);
    public static void Debug(string message, [CallerFilePath] string file = "") => Write(LogLevel.Debug, message, file);
    public static void Info(BepInExInfoLogInterpolatedStringHandler message, [CallerFilePath] string file = "") => Write(LogLevel.Info, message, file);
    public static void Info(string message, [CallerFilePath] string file = "") => Write(LogLevel.Info, message, file);
    public static void Message(BepInExMessageLogInterpolatedStringHandler message, [CallerFilePath] string file = "") => Write(LogLevel.Message, message, file);
    public static void Message(string message, [CallerFilePath] string file = "") => Write(LogLevel.Message, message, file);
    public static void Warning(BepInExWarningLogInterpolatedStringHandler message, [CallerFilePath] string file = "") => Write(LogLevel.Warning, message, file);
    public static void Warning(string message, [CallerFilePath] string file = "") => Write(LogLevel.Warning, message, file);
    public static void Error(BepInExErrorLogInterpolatedStringHandler message, [CallerFilePath] string file = "") => Write(LogLevel.Error, message, file);
    public static void Error(string message, [CallerFilePath] string file = "") => Write(LogLevel.Error, message, file);
    public static void Fatal(BepInExFatalLogInterpolatedStringHandler message, [CallerFilePath] string file = "") => Write(LogLevel.Fatal, message, file);
    public static void Fatal(string message, [CallerFilePath] string file = "") => Write(LogLevel.Fatal, message, file);

    public static void Write(LogLevel level, [InterpolatedStringHandlerArgument(nameof(level))] BepInExLogInterpolatedStringHandler message, [CallerFilePath] string file = "")
    {
        if (message.Enabled) Write(level, message.ToString(), file);
    }

    private static void Write(LogLevel level, string message, string file)
    {
        if ((Logger.ListenedLogLevels & level) == 0) return;
        int start = file.LastIndexOfAny(['/', '\\']) + 1;
        int end = file.IndexOf('.', start);
        Plugin.Instance.Log.Log(level, (object)$"[{DateTime.Now:HH:mm:ss.fff}] [{file[start..end]}] {message}");
    }
}
