using System.CommandLine;
using System.CommandLine.Invocation;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class DebugCommands
{
    public static void Register(RootCommand root)
    {
        var debugCmd = new Command("debug", TextId.CmdDescDebug.Get());

        // /debug kill <id>
        var killCmd = new Command("kill", TextId.DebugDescKill.Get());
        var guestIdArg = new Argument<int>("id", TextId.ArgDescGuestId.Get());
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
    }
}
