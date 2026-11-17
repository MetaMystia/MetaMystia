using System;
using System.CommandLine;
using System.CommandLine.Invocation;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class GeneralCommands
{
    public static void Register(RootCommand root)
    {
        var helpCmd = new Command("help", TextId.CmdDescHelp.Get());
        var pathArg = new Argument<string[]>("command", Array.Empty<string>, TextId.ArgDescCommand.Get()) { Arity = ArgumentArity.ZeroOrMore }
            .AddCompletions(_ => CommandRegistry.TopLevelNames());
        helpCmd.AddArgument(pathArg);
        helpCmd.SetHandler(ctx => CommandRegistry.WriteHelp(ctx, ctx.ParseResult.GetValueForArgument(pathArg)));
        root.AddCommand(helpCmd);

        var clearCmd = new Command("clear", TextId.CmdDescClear.Get());
        clearCmd.SetHandler(InGameConsole.ClearLogs);
        root.AddCommand(clearCmd);

        var whereAmICmd = new Command("whereami", TextId.CmdDescWhereami.Get());
        whereAmICmd.SetHandler(WhereAmIHandler);
        root.AddCommand(whereAmICmd);
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
}
