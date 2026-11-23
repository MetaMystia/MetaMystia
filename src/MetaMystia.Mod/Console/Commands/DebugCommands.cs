using System.CommandLine;
using System.CommandLine.Invocation;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Guests;
using MetaMystia.Multiplayer.Messages;
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
            var guest = GuestRegistry.Current.Find(id);
            if (guest?.Controller == null || guest.IsLeft)
            {
                ctx.Log(ConsoleFormat.Err($"Guest #{id} not found"));
                return;
            }
            ctx.Log($"Killing guest {guest}");
            if (GameSession.IsRoomHost) GuestLeaveMessage.Send(guest, LeaveReason.Force, GuestGroupController.LeaveType.Fading);
            guest.State = GuestPhase.Left;
            GuestReplay.ForceCleanup(guest.Controller);
        });
        debugCmd.AddCommand(killCmd);

        debugCmd.SetHandler(ctx =>
        {
            ctx.Log(MetaMystia.UI.MultiplayerStatus.DebugText);
        });
        root.AddCommand(debugCmd);
    }
}
