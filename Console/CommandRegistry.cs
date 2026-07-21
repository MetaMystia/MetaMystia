#nullable enable
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Linq;

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
    /// Result of GetCompletions: either completable items or a non-completable hint.
    /// </summary>
    public record CompletionResult(List<string> Items, string? Hint)
    {
        public static readonly CompletionResult Empty = new([], null);
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

    /// <summary>
    /// Build a concise usage hint for the matched command when a parse error occurs.
    /// e.g. "  → /skin set ‹characterId› ‹type› ‹skinIndex›"
    /// </summary>
    private static string? FormatUsageHint(ParseResult parseResult)
    {
        var cmd = parseResult.CommandResult.Command;
        if (cmd == _root) return null;

        // Walk up parent chain to build the full command path
        var pathParts = new List<string>();
        SymbolResult? sr = parseResult.CommandResult;
        while (sr is CommandResult cr)
        {
            if (cr.Command != _root)
                pathParts.Insert(0, cr.Command.Name);
            sr = cr.Parent;
        }
        if (pathParts.Count == 0) return null;

        string path = "/" + string.Join(" ", pathParts);
        var usageParts = new List<string>();

        // Add argument names
        foreach (var arg in cmd.Arguments)
        {
            bool optional = arg.Arity.MinimumNumberOfValues == 0;
            usageParts.Add(optional ? $"[{arg.Name}]" : $"<{arg.Name}>");
        }

        // Add subcommand names if no arguments
        if (usageParts.Count == 0)
        {
            var subs = cmd.Children.OfType<Command>().Select(c => c.Name).ToList();
            if (subs.Count > 0)
                usageParts.Add($"<{string.Join("|", subs)}>");
        }

        if (usageParts.Count == 0) return null;

        string argStr = string.Join(" ", usageParts);
        return $"  {ConsoleFormat.Dim("→")} {ConsoleFormat.Cmd(path)} {ConsoleFormat.Arg(argStr)}";
    }

    /// <summary>
    /// Get completion suggestions for the partial input (without leading '/').
    /// Walks the command tree manually to determine exact position,
    /// then returns only relevant completions for that position.
    /// Returns completable items OR a non-completable hint for free-form args.
    /// </summary>
    public static CompletionResult GetCompletions(string partial)
    {
        try
        {
            var tokens = partial.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool hasTrailingSpace = partial.Length > 0 && partial[^1] == ' ';

            // The token currently being typed (empty if cursor is after a space)
            string currentPartial = (!hasTrailingSpace && tokens.Length > 0) ? tokens[^1] : "";
            // Tokens that are fully typed (resolved)
            var resolvedTokens = hasTrailingSpace ? tokens : tokens.Length > 0 ? tokens[..^1] : [];

            // Walk the command tree with resolved tokens
            Command current = _root;
            int tokenIndex = 0;
            while (tokenIndex < resolvedTokens.Length)
            {
                var sub = current.Children.OfType<Command>()
                    .FirstOrDefault(c => c.Name.Equals(resolvedTokens[tokenIndex], StringComparison.OrdinalIgnoreCase));
                if (sub != null)
                {
                    current = sub;
                    tokenIndex++;
                }
                else
                    break;
            }

            int argumentTokensConsumed = resolvedTokens.Length - tokenIndex;
            var subcommands = current.Children.OfType<Command>().Select(c => c.Name).ToList();

            // If there are subcommands and no argument tokens consumed yet, suggest subcommands
            if (subcommands.Count > 0 && argumentTokensConsumed == 0)
                return new CompletionResult(FilterPrefix(subcommands, currentPartial), null);

            // We're at argument position = argumentTokensConsumed
            int argIndex = argumentTokensConsumed;
            string cmdPath = string.Join(" ", resolvedTokens[..tokenIndex]
                .Select(t => t.ToLowerInvariant()));

            // Check for completable values first (static)
            if (_completionMeta.TryGetValue(cmdPath, out var meta))
            {
                var match = meta.FirstOrDefault(m => m.argIndex == argIndex);
                if (match.values != null && match.values.Length > 0)
                    return new CompletionResult(FilterPrefix(match.values.ToList(), currentPartial), null);
            }

            // Check for dynamic completions
            if (_dynamicCompletionMeta.TryGetValue(cmdPath, out var dynamicMeta))
            {
                var dynMatch = dynamicMeta.FirstOrDefault(m => m.argIndex == argIndex);
                if (dynMatch.provider != null)
                {
                    var dynamicValues = dynMatch.provider();
                    if (dynamicValues.Length > 0)
                        return new CompletionResult(FilterPrefix(dynamicValues.ToList(), currentPartial), null);
                }
            }

            // Check for hint (non-completable info)
            if (_hintMeta.TryGetValue(cmdPath, out var hints))
            {
                var hintMatch = hints.FirstOrDefault(h => h.argIndex == argIndex);
                if (hintMatch.hint != null)
                    return new CompletionResult([], hintMatch.hint);
            }

            return CompletionResult.Empty;
        }
        catch
        {
            return CompletionResult.Empty;
        }
    }

    private static List<string> FilterPrefix(List<string> items, string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return items;

        var filtered = items
            .Where(i => i.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // If the only match is an exact match, the token is already fully typed
        if (filtered.Count == 1 &&
            filtered[0].Equals(prefix, StringComparison.OrdinalIgnoreCase))
            return [];

        return filtered;
    }

    /// <summary>
    /// Get all top-level command names (for basic completion when input is empty).
    /// </summary>
    public static List<string> GetTopLevelCommands()
    {
        return _root.Children
            .OfType<Command>()
            .Select(c => c.Name)
            .ToList();
    }

    /// <summary>
    /// Get command name + description pairs for generating help text.
    /// Includes subcommands for commands that have them.
    /// </summary>
    public static List<(string name, string description, List<string> subcommands)> GetCommandInfo()
    {
        return _root.Children
            .OfType<Command>()
            .Select(c => (
                c.Name,
                c.Description ?? "",
                c.Children.OfType<Command>().Select(s => s.Name).ToList()
            ))
            .ToList();
    }
}
