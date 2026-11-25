#nullable enable
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem;

public readonly record struct Suggestion(string Label, string? Detail);

/// <param name="Start">被补全片段在输入中的起始位置（含开头的 '/'）。</param>
/// <param name="Usage">当前命令用法，正在输入的参数已高亮；不在命令参数位置时为 null。</param>
public sealed record CompletionResult(int Start, List<Suggestion> Items, string? Usage);

public static class CommandRegistry
{
    private static RootCommand _root = null!;
    private static Parser _parser = null!;
    private static Language? _language;

    // 描述在构建时取当前语言，语言变化后重建。
    private static void EnsureBuilt()
    {
        if (_language == L10n.Language) return;
        _language = L10n.Language;
        _root = new RootCommand();
        Commands.GeneralCommands.Register(_root);
        Commands.GetCommands.Register(_root);
        Commands.MpCommands.Register(_root);
        Commands.SkinCommands.Register(_root);
        Commands.DebugCommands.Register(_root);
        Commands.LinkCommands.Register(_root);
        Commands.ResourceExCommands.Register(_root);
        Commands.LiveCommands.Register(_root);
        Commands.EmoteCommands.Register(_root);
        Commands.CheatCommands.Register(_root);
        AddHelpHandlers(_root);
        _root.SetHandler(ctx => WriteHelp(ctx, _root));
        _parser = new CommandLineBuilder(_root).UseLocalizationResources(new ConsoleErrors()).Build();
    }

    private static void AddHelpHandlers(Command command)
    {
        foreach (var sub in command.Subcommands)
        {
            if (sub.Handler == null && sub.Subcommands.Count > 0)
                sub.SetHandler(ctx => WriteHelp(ctx, sub));
            AddHelpHandlers(sub);
        }
    }

    /// <summary>执行不含开头 '/' 的命令，返回是否关闭控制台。</summary>
    public static bool Execute(string input, ConsoleContext context)
    {
        EnsureBuilt();
        context.CloseConsole = false;
        try
        {
            var result = _parser.Parse(input);
            if (result.Errors.Count > 0)
            {
                context.Log(ConsoleFormat.Err(string.Join("；", result.Errors.Select(e => e.Message).Distinct())));
                if (result.CommandResult.Command != _root)
                    context.Log("  " + ConsoleFormat.Dim(TextId.ConsoleUsage.Get(Usage(result.CommandResult.Command))));
                return false;
            }
            _parser.Invoke(input, context);
        }
        catch (Exception ex)
        {
            context.Log(ConsoleFormat.Err(ex.Message));
            Log.Error($"Command execution error: {ex}");
        }
        return context.CloseConsole;
    }

    /// <summary>补全光标处的片段。text 含开头的 '/'，光标位于末尾。</summary>
    public static CompletionResult? Complete(string text)
    {
        if (!text.StartsWith('/')) return null;
        EnsureBuilt();
        int start = text.LastIndexOf(' ') + 1;
        if (start == 0) start = 1;
        string partial = text[start..];
        var words = text[1..start].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Command command = _root;
        int depth = 0;
        while (depth < words.Length && FindSubcommand(command, words[depth]) is { } sub)
        {
            command = sub;
            depth++;
        }
        if (command == _root && depth < words.Length) return null;

        int argIndex = words.Length - depth;
        var candidates = new List<Suggestion>();
        if (argIndex == 0)
            candidates.AddRange(Visible(command).Select(c => new Suggestion(c.Name, c.Description)));
        var argument = ArgumentAt(command, argIndex);
        if (argument != null)
            candidates.AddRange(argument.GetCompletions().Select(c => new Suggestion(c.Label, null)));
        string? usage = command != _root && (argument != null || command.Subcommands.Count == 0)
            ? Usage(command, argIndex) + (string.IsNullOrEmpty(argument?.Description) ? "" : ConsoleFormat.Dim("  " + argument.Description))
            : null;
        return new CompletionResult(start, Filter(candidates, partial), usage);
    }

    private static List<Suggestion> Filter(List<Suggestion> candidates, string partial)
    {
        var distinct = candidates.GroupBy(s => s.Label, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        var prefix = distinct.Where(s => s.Label.StartsWith(partial, StringComparison.OrdinalIgnoreCase));
        var contains = distinct.Where(s => !s.Label.StartsWith(partial, StringComparison.OrdinalIgnoreCase)
            && s.Label.Contains(partial, StringComparison.OrdinalIgnoreCase));
        var items = prefix.Concat(contains).ToList();
        return items.Count == 1 && items[0].Label.Equals(partial, StringComparison.OrdinalIgnoreCase) ? [] : items;
    }

    private static Command? FindSubcommand(Command command, string name)
        => command.Subcommands.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<Command> Visible(Command command) => command.Subcommands.Where(c => !c.IsHidden);

    private static Argument? ArgumentAt(Command command, int index)
    {
        var arguments = command.Arguments;
        if (index < arguments.Count) return arguments[index];
        return arguments.Count > 0 && arguments[^1].Arity.MaximumNumberOfValues > 1 ? arguments[^1] : null;
    }

    private static string Path(Command command)
    {
        var names = new List<string>();
        for (Symbol? s = command; s is Command c && c != _root; s = c.Parents.FirstOrDefault())
            names.Insert(0, c.Name);
        return "/" + string.Join(" ", names);
    }

    private static string Signature(Command command, int highlight = -1)
    {
        var arguments = command.Arguments;
        if (arguments.Count == 0)
        {
            var subs = Visible(command).Select(c => c.Name).ToList();
            return subs.Count == 0 ? "" : ConsoleFormat.Arg($"<{string.Join("|", subs)}>");
        }
        return string.Join(" ", arguments.Select((a, i) =>
        {
            string text = a.Arity.MinimumNumberOfValues == 0 ? $"[{a.Name}]" : $"<{a.Name}>";
            return i == highlight || (i == arguments.Count - 1 && highlight > i && a.Arity.MaximumNumberOfValues > 1)
                ? ConsoleFormat.Current(text) : ConsoleFormat.Arg(text);
        }));
    }

    private static string Usage(Command command, int highlight = -1)
    {
        string signature = Signature(command, highlight);
        return signature.Length == 0 ? ConsoleFormat.Cmd(Path(command)) : $"{ConsoleFormat.Cmd(Path(command))} {signature}";
    }

    /// <summary>输出命令帮助；path 为空时列出全部顶级命令。</summary>
    public static void WriteHelp(InvocationContext ctx, IReadOnlyList<string> path)
    {
        EnsureBuilt();
        Command command = _root;
        foreach (var name in path)
        {
            if (FindSubcommand(command, name) is not { } sub)
            {
                ctx.Log(ConsoleFormat.Err(TextId.ConsoleUnknownCommand.Get(ConsoleFormat.Plain("/" + string.Join(" ", path)))));
                return;
            }
            command = sub;
        }
        WriteHelp(ctx, command);
    }

    private static void WriteHelp(InvocationContext ctx, Command command)
    {
        if (command == _root)
        {
            ctx.Log(ConsoleFormat.Header(TextId.HelpHeader.Get()));
            foreach (var sub in Visible(command))
                ctx.Log(ConsoleFormat.SubCmd(Path(sub), sub.Arguments.Count == 0 && sub.Subcommands.Count > 0 ? ConsoleFormat.Arg("<…>") : Signature(sub), sub.Description));
            ctx.Log(ConsoleFormat.Dim(TextId.ConsoleHelpHint.Get()));
            return;
        }
        ctx.Log(ConsoleFormat.Header(Path(command)) + "  " + ConsoleFormat.Dim(command.Description ?? ""));
        if (command.Arguments.Count > 0)
        {
            ctx.Log("  " + TextId.ConsoleUsage.Get(Usage(command)));
            foreach (var argument in command.Arguments.Where(a => !string.IsNullOrEmpty(a.Description)))
                ctx.Log($"    {ConsoleFormat.Arg(argument.Name)} {ConsoleFormat.Dim("— " + argument.Description)}");
        }
        foreach (var sub in Visible(command))
            ctx.Log(ConsoleFormat.SubCmd(Path(sub), Signature(sub), sub.Description));
    }

    public static IEnumerable<string> TopLevelNames()
    {
        EnsureBuilt();
        return Visible(_root).Select(c => c.Name);
    }

    private sealed class ConsoleErrors : LocalizationResources
    {
        public override string UnrecognizedCommandOrArgument(string arg) => TextId.ConsoleErrUnrecognized.Get(ConsoleFormat.Plain(arg));
        public override string UnrecognizedArgument(string unrecognizedArg, IReadOnlyCollection<string> allowedValues)
            => TextId.ConsoleErrInvalidValue.Get(ConsoleFormat.Plain(unrecognizedArg), string.Join(", ", allowedValues));
        public override string RequiredArgumentMissing(SymbolResult symbolResult) => TextId.ConsoleErrMissingArgument.Get();
        public override string RequiredCommandWasNotProvided() => TextId.ConsoleErrMissingSubcommand.Get();
        public override string ExpectsOneArgument(SymbolResult symbolResult) => TextId.ConsoleErrMissingArgument.Get();
        public override string ExpectsFewerArguments(Token token, int providedNumberOfValues, int maximumNumberOfValues)
            => TextId.ConsoleErrExtraArgument.Get(ConsoleFormat.Plain(token.Value));
        public override string ArgumentConversionCannotParse(string value, Type expectedType) => TextId.ConsoleErrBadFormat.Get(ConsoleFormat.Plain(value));
        public override string ArgumentConversionCannotParseForCommand(string value, string commandAlias, Type expectedType)
            => TextId.ConsoleErrBadFormat.Get(ConsoleFormat.Plain(value));
    }
}
