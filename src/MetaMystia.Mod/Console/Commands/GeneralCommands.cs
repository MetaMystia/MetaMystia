using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class GeneralCommands
{
    public static void Register(RootCommand root)
    {
        var helpCmd = new Command("help", "Show available commands");
        var helpPath = new Argument<string[]>("command", () => []) { Arity = ArgumentArity.ZeroOrMore };
        helpCmd.AddArgument(helpPath);
        helpCmd.SetHandler(ctx => HelpHandler(ctx, ctx.ParseResult.GetValueForArgument(helpPath)));
        root.AddCommand(helpCmd);
        CommandRegistry.RegisterDynamicCompletions("help", 0,
            () => CommandRegistry.TopLevelCommands.Select(c => c.Name).ToArray());

        var consoleCmd = new Command("console", "Console layout");
        var fontCmd = new Command("font", "Set console font size");
        var fontSize = new Argument<int>("size");
        fontCmd.AddArgument(fontSize);
        fontCmd.SetHandler(ctx => InGameConsole.SetFontSize(ctx.ParseResult.GetValueForArgument(fontSize)));
        consoleCmd.AddCommand(fontCmd);
        var resetCmd = new Command("reset", "Reset console position, size and font");
        resetCmd.SetHandler(InGameConsole.ResetLayout);
        consoleCmd.AddCommand(resetCmd);
        root.AddCommand(consoleCmd);

        var clearCmd = new Command("clear", "Clear console logs and input history");
        clearCmd.SetHandler(ClearHandler);
        root.AddCommand(clearCmd);

        var whereAmICmd = new Command("whereami", "Show current map and position");
        whereAmICmd.SetHandler(WhereAmIHandler);
        root.AddCommand(whereAmICmd);

        var enableBepinCmd = new Command("enable_bepin_console", "Enable BepInEx debug console");
        enableBepinCmd.SetHandler(EnableBepInConsoleHandler);
        root.AddCommand(enableBepinCmd);
    }

    private static void ClearHandler(InvocationContext ctx)
    {
        InGameConsole.ClearLogs();
    }

    private static void WhereAmIHandler(InvocationContext ctx)
    {
        if (GameFlow.LocalScene != Common.UI.Scene.DayScene)
        {
            ctx.Log(TextId.NotInDayScene.Get());
            return;
        }
        ctx.Log(TextId.MapInfoDisplay.Get(PlayerManager.LocalMapLabel.GetDisplayName(), PlayerManager.LocalPosition));
    }

    private static void EnableBepInConsoleHandler(InvocationContext ctx)
    {
        BepInEx.ConsoleManager.CreateConsole();
        ctx.Log(TextId.BepInExConsoleEnabled.Get());
        System.Console.OutputEncoding = System.Text.Encoding.UTF8;
    }

    private static void HelpHandler(InvocationContext ctx, string[] path)
    {
        if (path.Length == 0)
        {
            ctx.Log(ConsoleFormat.Header(TextId.HelpHeader.Get()));
            foreach (var cmd in CommandRegistry.TopLevelCommands)
                ctx.Log(ConsoleFormat.SubCmd("/" + cmd.Name, "", CommandRegistry.Describe(cmd)));
            ctx.Log(ConsoleFormat.Dim("  /help ‹command›"));
            return;
        }

        string joined = string.Join(" ", path).TrimStart('/');
        var command = CommandRegistry.FindCommand(joined);
        if (command == null)
        {
            ctx.Log(ConsoleFormat.Err("/" + joined + " ?"));
            return;
        }

        ctx.Log(ConsoleFormat.Header("/" + joined));
        string desc = CommandRegistry.Describe(command);
        if (desc.Length > 0) ctx.Log("  " + desc);
        ctx.Log("  " + CommandRegistry.FormatUsage(joined, command));
        foreach (var sub in command.Children.OfType<Command>())
            ctx.Log(ConsoleFormat.SubCmd(sub.Name, "", CommandRegistry.Describe(sub)));
    }
}
