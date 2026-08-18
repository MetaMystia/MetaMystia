#nullable enable
using System;
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

    /// <summary>
    /// When set to true by a command handler, the console UI will close after execution.
    /// </summary>
    public bool CloseConsole { get; set; }

    public ConsoleContext(Action<string> logToConsole)
    {
        _logToConsole = logToConsole;
        Out = new ConsoleWriter(logToConsole);
        Error = new ConsoleWriter(logToConsole);
    }

    public IStandardStreamWriter Out { get; }
    public IStandardStreamWriter Error { get; }
    public bool IsOutputRedirected => true;
    public bool IsErrorRedirected => true;
    public bool IsInputRedirected => true;

    public void Log(string message) => _logToConsole(message);

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
