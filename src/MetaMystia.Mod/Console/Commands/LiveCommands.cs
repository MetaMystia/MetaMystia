using System.CommandLine;
using System.CommandLine.Invocation;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class LiveCommands
{
    public static void Register(RootCommand root)
    {
        var liveCmd = new Command("live", "Toggle live streaming privacy mode");
        var modeArg = new Argument<string>("mode", "off, partial, full (omit for emergency full)")
        {
            Arity = ArgumentArity.ZeroOrOne
        };
        liveCmd.AddArgument(modeArg);
        liveCmd.SetHandler(ctx =>
        {
            string mode = ctx.ParseResult.GetValueForArgument(modeArg);
            if (string.IsNullOrEmpty(mode))
            {
                LiveModeManager.ApplyMode(LiveMode.Full);
                ctx.Log(TextId.LiveEmergencyFull.Get());
                ctx.Log(TextId.LiveUsage.Get());
                return;
            }

            if (!TryParseMode(mode, out var parsed))
            {
                ctx.Log(ConsoleFormat.Err(TextId.LiveInvalidMode.Get(mode)));
                ctx.Log(TextId.LiveUsage.Get());
                return;
            }

            LiveModeManager.ApplyMode(parsed);
            ctx.Log(GetModeSetMessage(parsed));
        });
        root.AddCommand(liveCmd);

        CommandRegistry.RegisterCompletions("live", 0, "off", "partial", "full");
    }

    private static bool TryParseMode(string value, out LiveMode mode)
    {
        switch (value.ToLowerInvariant())
        {
            case "off":
                mode = LiveMode.Off;
                return true;
            case "partial":
                mode = LiveMode.Partial;
                return true;
            case "full":
                mode = LiveMode.Full;
                return true;
            default:
                mode = LiveMode.Off;
                return false;
        }
    }

    private static string GetModeSetMessage(LiveMode mode) => mode switch
    {
        LiveMode.Off => TextId.LiveModeOff.Get(),
        LiveMode.Partial => TextId.LiveModePartial.Get(),
        LiveMode.Full => TextId.LiveModeFull.Get(),
        _ => TextId.LiveModeOff.Get()
    };
}
