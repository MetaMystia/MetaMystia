using System.Collections.Generic;
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
        helpCmd.SetHandler(HelpHandler);
        root.AddCommand(helpCmd);

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
        if (MpManager.LocalScene != Common.UI.Scene.DayScene)
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

    private static readonly Dictionary<string, TextId> _cmdDescriptions = new()
    {
        ["help"] = TextId.CmdDescHelp,
        ["clear"] = TextId.CmdDescClear,
        ["get"] = TextId.CmdDescGet,
        ["mp"] = TextId.CmdDescMp,
        ["call"] = TextId.CmdDescCall,
        ["skin"] = TextId.CmdDescSkin,
        ["debug"] = TextId.CmdDescDebug,
        ["webdebug"] = TextId.CmdDescWebdebug,
        ["whereami"] = TextId.CmdDescWhereami,
        ["enable_bepin_console"] = TextId.CmdDescEnableBepinConsole,
        ["link"] = TextId.CmdDescLink,
        ["resourceex"] = TextId.CmdDescResourceEx,
        ["live"] = TextId.CmdDescLive,
    };

    private static void HelpHandler(InvocationContext ctx)
    {
        ctx.Log(ConsoleFormat.Header(TextId.HelpHeader.Get()));
        foreach (var (name, _, subs) in CommandRegistry.GetCommandInfo())
        {
            string desc = _cmdDescriptions.TryGetValue(name, out var textId) ? textId.Get() : "";
            string line = $"  {ConsoleFormat.Cmd("/" + name)}";
            if (subs.Count > 0)
                line += $" {ConsoleFormat.Arg("<sub>")}  {ConsoleFormat.Dim(desc + " ─ " + string.Join("|", subs))}";
            else
                line += $"  {ConsoleFormat.Dim(desc)}";
            ctx.Log(line);
        }
        ctx.Log(ConsoleFormat.Line);
    }
}
