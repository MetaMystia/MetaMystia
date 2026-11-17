#nullable enable
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Invocation;

namespace MetaMystia.ConsoleSystem;

/// <summary>
/// IConsole adapter redirecting System.CommandLine output to InGameConsole.LogToConsole().
/// </summary>
public class ConsoleContext : IConsole
{
    private readonly Action<string> _logToConsole;
    private List<string>? _group;
    private int _groupThread;

    /// <summary>
    /// When set to true by a command handler, the console UI will close after execution.
    /// </summary>
    public bool CloseConsole { get; set; }

    public ConsoleContext(Action<string> logToConsole)
    {
        _logToConsole = logToConsole;
        Out = new ConsoleWriter(Log);
        Error = new ConsoleWriter(Log);
    }

    public IStandardStreamWriter Out { get; }
    public IStandardStreamWriter Error { get; }
    public bool IsOutputRedirected => true;
    public bool IsErrorRedirected => true;
    public bool IsInputRedirected => true;

    public void Log(string message)
    {
        if (_group != null && Environment.CurrentManagedThreadId == _groupThread) _group.Add(message);
        else _logToConsole(message);
    }

    /// <summary>此后本线程的输出合并为一条消息，直到 <see cref="EndGroup"/>；命令结束后的异步输出仍各自成条。</summary>
    public void BeginGroup()
    {
        _group = [];
        _groupThread = Environment.CurrentManagedThreadId;
    }

    public void EndGroup()
    {
        var lines = _group;
        _group = null;
        if (lines is { Count: > 0 }) _logToConsole(string.Join("\n", lines));
    }

    private class ConsoleWriter : IStandardStreamWriter
    {
        private readonly Action<string> _log;
        public ConsoleWriter(Action<string> log) => _log = log;
        public void Write(string? value)
        {
            if (!string.IsNullOrEmpty(value))
                _log(value.TrimEnd('\n', '\r'));
        }
    }
}

public static class InvocationContextExtensions
{
    public static void Log(this InvocationContext ctx, string message)
        => ((ConsoleContext)ctx.Console).Log(message);

    public static void RequestCloseConsole(this InvocationContext ctx)
        => ((ConsoleContext)ctx.Console).CloseConsole = true;
}
