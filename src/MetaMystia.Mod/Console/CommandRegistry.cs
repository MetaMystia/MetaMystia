#nullable enable
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Linq;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem;

[AutoLog]
public static partial class CommandRegistry
{
    private static RootCommand _root = null!;
    private static Parser _parser = null!;

    /// <summary>
    /// Completion metadata: maps "command path" → list of (argIndex, allowedValues).
    /// e.g. "mp start" → [(0, ["server","client"])]
    /// </summary>
    private static readonly Dictionary<string, List<(int argIndex, string[] values)>> _completionMeta = new();

    /// <summary>
    /// Hint metadata for free-form arguments: maps "command path" → list of (argIndex, hintText).
    /// e.g. "mp connect" → [(0, "IP address or IP:port")]
    /// Hints are shown in the dropdown but NOT selectable by Tab.
    /// </summary>
    private static readonly Dictionary<string, List<(int argIndex, string hint)>> _hintMeta = new();

    /// <summary>
    /// Dynamic completion metadata: maps "command path" → list of (argIndex, provider).
    /// The provider is called each time completions are requested, enabling real-time data.
    /// </summary>
    private static readonly Dictionary<string, List<(int argIndex, Func<string[]> provider)>> _dynamicCompletionMeta = new();

    public static void Initialize()
    {
        _root = new RootCommand("MetaMystia Console");

        Commands.GeneralCommands.Register(_root);
        Commands.GetCommands.Register(_root);
        Commands.MpCommands.Register(_root);
        Commands.CallCommands.Register(_root);
        Commands.SkinCommands.Register(_root);
        Commands.DebugCommands.Register(_root);
        Commands.LinkCommands.Register(_root);
        Commands.ResourceExCommands.Register(_root);
        Commands.LiveCommands.Register(_root);
        Commands.EmoteCommands.Register(_root);
        Commands.CheatCommands.Register(_root);

        // Build parser without default help/version or verbose error reporting
        _parser = new CommandLineBuilder(_root)
            .Build();

        Log.Info("CommandRegistry initialized");
    }

    /// <summary>
    /// Register completion values for a specific argument position of a command.
    /// Call this after adding the command to the tree.
    /// commandPath: space-separated command names, e.g. "mp start", "skin set", "get"
    /// </summary>
    public static void RegisterCompletions(string commandPath, int argIndex, params string[] values)
    {
        if (!_completionMeta.TryGetValue(commandPath, out var list))
        {
            list = [];
            _completionMeta[commandPath] = list;
        }
        list.Add((argIndex, values));
    }

    /// <summary>
    /// Register a hint for a free-form argument position. Shown in dropdown but not Tab-completable.
    /// commandPath: space-separated command names, e.g. "mp connect"
    /// </summary>
    public static void RegisterHint(string commandPath, int argIndex, string hint)
    {
        if (!_hintMeta.TryGetValue(commandPath, out var list))
        {
            list = [];
            _hintMeta[commandPath] = list;
        }
        list.Add((argIndex, hint));
    }

    /// <summary>
    /// Register a dynamic completion provider for a specific argument position.
    /// The provider Func is called each time completions are requested.
    /// commandPath: space-separated command names, e.g. "mp kick id"
    /// </summary>
    public static void RegisterDynamicCompletions(string commandPath, int argIndex, Func<string[]> provider)
    {
        if (!_dynamicCompletionMeta.TryGetValue(commandPath, out var list))
        {
            list = [];
            _dynamicCompletionMeta[commandPath] = list;
        }
        list.Add((argIndex, provider));
    }

    /// <summary>
    /// Execute a slash command (input should NOT include the leading '/').
    /// Returns true if the console should close after execution.
    /// </summary>
    public static bool Execute(string input, ConsoleContext context)
    {
        context.CloseConsole = false;
        try
        {
            var parseResult = _parser.Parse(input);
            if (parseResult.Errors.Count > 0)
            {
                context.Log(ConsoleFormat.Err(parseResult.Errors[0].Message));
                var hint = FormatUsageHint(parseResult);
                if (hint != null) context.Log(hint);
                return false;
            }
            _parser.Invoke(input, context);
        }
        catch (Exception ex)
        {
            context.Log(ConsoleFormat.Err($"Command error: {ex.Message}"));
            Log.Error($"Command execution error: {ex}");
        }
        return context.CloseConsole;
    }

    /// <summary>解析出错时给出匹配命令的用法，如 "→ /skin set ‹characterId› ‹type› ‹skinIndex›"。</summary>
    private static string? FormatUsageHint(ParseResult parseResult)
    {
        var cmd = parseResult.CommandResult.Command;
        if (cmd == _root) return null;

        var pathParts = new List<string>();
        SymbolResult? sr = parseResult.CommandResult;
        while (sr is CommandResult cr)
        {
            if (cr.Command != _root)
                pathParts.Insert(0, cr.Command.Name);
            sr = cr.Parent;
        }
        if (pathParts.Count == 0) return null;
        return $"  {ConsoleFormat.Dim("→")} {FormatUsage(string.Join(" ", pathParts), cmd)}";
    }

    private static readonly Dictionary<string, TextId> _topLevelDescriptions = new()
    {
        ["help"] = TextId.CmdDescHelp,
        ["clear"] = TextId.CmdDescClear,
        ["get"] = TextId.CmdDescGet,
        ["mp"] = TextId.CmdDescMp,
        ["call"] = TextId.CmdDescCall,
        ["skin"] = TextId.CmdDescSkin,
        ["debug"] = TextId.CmdDescDebug,
        ["whereami"] = TextId.CmdDescWhereami,
        ["enable_bepin_console"] = TextId.CmdDescEnableBepinConsole,
        ["link"] = TextId.CmdDescLink,
        ["resourceex"] = TextId.CmdDescResourceEx,
        ["live"] = TextId.CmdDescLive,
        ["cheat"] = TextId.CmdDescCheat,
        ["console"] = TextId.CmdDescConsole,
    };

    /// <summary>命令说明：顶层命令用本地化文本，其余用注册时的描述。</summary>
    public static string Describe(Command command)
    {
        if (command.Parents.Contains(_root) && _topLevelDescriptions.TryGetValue(command.Name, out var id))
            return id.Get();
        return command.Description ?? "";
    }

    public static IEnumerable<Command> TopLevelCommands => _root.Children.OfType<Command>();

    /// <summary>按空格分隔的路径查找命令，如 "mp connect"。找不到返回 null。</summary>
    public static Command? FindCommand(string path)
    {
        Command current = _root;
        foreach (var token in path.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var sub = current.Children.OfType<Command>()
                .FirstOrDefault(c => c.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
            if (sub == null) return null;
            current = sub;
        }
        return current == _root ? null : current;
    }

    /// <summary>命令用法，如 "/skin set ‹characterId› ‹type› ‹skinIndex›"；activeArg 指定高亮的参数。</summary>
    public static string FormatUsage(string path, Command command, int activeArg = -1)
    {
        var sb = new System.Text.StringBuilder(ConsoleFormat.Cmd("/" + path));
        int i = 0;
        foreach (var arg in command.Arguments)
        {
            bool optional = arg.Arity.MinimumNumberOfValues == 0;
            string label = optional ? $"[{arg.Name}]" : $"‹{arg.Name}›";
            sb.Append(' ').Append(i == activeArg ? $"<b>{ConsoleFormat.Arg(label)}</b>" : ConsoleFormat.Dim(label));
            i++;
        }
        if (i == 0 && command.Children.OfType<Command>().Any())
            sb.Append(' ').Append(ConsoleFormat.Dim("‹" + string.Join("|", command.Children.OfType<Command>().Select(c => c.Name)) + "›"));
        return sb.ToString();
    }

    /// <summary>
    /// 补全查询（不含开头的 '/'）。沿命令树定位光标所在位置，返回候选值与当前命令的参数签名。
    /// </summary>
    public static CompletionQuery GetCompletions(string partial)
    {
        try
        {
            var tokens = partial.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool hasTrailingSpace = partial.Length > 0 && partial[^1] == ' ';
            string currentPartial = (!hasTrailingSpace && tokens.Length > 0) ? tokens[^1] : "";
            var resolved = hasTrailingSpace ? tokens : tokens.Length > 0 ? tokens[..^1] : [];

            Command current = _root;
            int depth = 0;
            while (depth < resolved.Length)
            {
                var sub = current.Children.OfType<Command>()
                    .FirstOrDefault(c => c.Name.Equals(resolved[depth], StringComparison.OrdinalIgnoreCase));
                if (sub == null) break;
                current = sub;
                depth++;
            }

            int argIndex = resolved.Length - depth;
            string cmdPath = string.Join(" ", resolved[..depth].Select(t => t.ToLowerInvariant()));
            string? signature = current == _root ? null : FormatUsage(cmdPath, current, argIndex);

            var subcommands = current.Children.OfType<Command>().ToList();
            if (subcommands.Count > 0 && argIndex == 0)
            {
                var items = subcommands
                    .Where(c => c.Name.StartsWith(currentPartial, StringComparison.OrdinalIgnoreCase))
                    .Select(c => new CompletionItem(c.Name, Describe(c)))
                    .ToList();
                return new CompletionQuery(DropExactOnly(items, currentPartial), current.Arguments.Any() ? signature : null);
            }

            string? hint = null;
            if (_hintMeta.TryGetValue(cmdPath, out var hints))
                hint = hints.FirstOrDefault(h => h.argIndex == argIndex).hint;
            if (hint != null && signature != null)
                signature += "  " + ConsoleFormat.Dim(hint);

            string[]? values = null;
            if (_completionMeta.TryGetValue(cmdPath, out var meta))
                values = meta.FirstOrDefault(m => m.argIndex == argIndex).values;
            if ((values == null || values.Length == 0) && _dynamicCompletionMeta.TryGetValue(cmdPath, out var dynamicMeta))
                values = dynamicMeta.FirstOrDefault(m => m.argIndex == argIndex).provider?.Invoke();

            var candidates = (values ?? [])
                .Where(v => v.StartsWith(currentPartial, StringComparison.OrdinalIgnoreCase))
                .Select(v => new CompletionItem(v, ""))
                .ToList();
            return new CompletionQuery(DropExactOnly(candidates, currentPartial), signature);
        }
        catch (Exception ex)
        {
            Log.Debug($"Completion failed: {ex.Message}");
            return CompletionQuery.Empty;
        }
    }

    /// <summary>唯一候选恰好等于已输入内容时，视为已完成，不再弹出。</summary>
    private static List<CompletionItem> DropExactOnly(List<CompletionItem> items, string partial) =>
        items.Count == 1 && items[0].Value.Equals(partial, StringComparison.OrdinalIgnoreCase) ? [] : items;
}
