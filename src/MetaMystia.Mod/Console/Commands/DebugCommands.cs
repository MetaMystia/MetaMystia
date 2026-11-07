using System.CommandLine;
using System.CommandLine.Invocation;

namespace MetaMystia.ConsoleSystem.Commands;

public static class DebugCommands
{
    public static void Register(RootCommand root)
    {
        var debugCmd = new Command("debug", "Multiplayer debug commands");

        // /debug kill <id>
        var killCmd = new Command("kill", "Kill a guest by runtime ID");
        var guestIdArg = new Argument<int>("id", "Guest runtime ID");
        killCmd.AddArgument(guestIdArg);
        killCmd.SetHandler(ctx =>
        {
            int id = ctx.ParseResult.GetValueForArgument(guestIdArg);
            var fsm = GuestsMap.GetGuestFsm(id);
            if (fsm == null)
            {
                ctx.Log(ConsoleFormat.Err($"Guest #{id} not found"));
                return;
            }
            ctx.Log($"Killing guest #{id} ({fsm.CurrentState})");
            fsm.Kill();
        });
        debugCmd.AddCommand(killCmd);

        debugCmd.SetHandler(ctx =>
        {
            ctx.Log(MetaMystia.UI.MultiplayerStatus.DebugText);
        });
        root.AddCommand(debugCmd);

        CommandRegistry.RegisterCompletions("debug", 0, "kill");
        CommandRegistry.RegisterHint("debug kill", 0, "<guest id>");
    }
}
