using System.CommandLine;

using MetaMystia.UI;

namespace MetaMystia.ConsoleSystem.Commands;

public static class CheatCommands
{
    public static void Register(RootCommand root)
    {
        var cheatCmd = new Command("cheat", TextId.CmdDescCheat.Get());
        var feverCmd = new Command("fever", TextId.CheatDescFever.Get());
        var modeArg = new Argument<string>("mode", () => "on")
        {
            Arity = ArgumentArity.ZeroOrOne
        };
        feverCmd.AddArgument(modeArg);
        feverCmd.SetHandler(ctx =>
        {
            string mode = ctx.ParseResult.GetValueForArgument(modeArg).ToLowerInvariant();
            if (mode is not "on" and not "off")
            {
                ctx.Log(ConsoleFormat.Err(TextId.CheatInvalidMode.Get(mode)));
                return;
            }

            ConfigManager.CheatFever.Value = mode == "on";
            if (!ConfigManager.CheatFever.Value)
            {
                // TODO: 是否移除本晚已有 Buff，待确定。
                ctx.Log(TextId.CheatFeverDisabled.Get());
                return;
            }

            ctx.Log(CheatManager.TryApplyFever()
                ? TextId.CheatFeverApplied.Get()
                : TextId.CheatFeverPending.Get());
        });
        cheatCmd.AddCommand(feverCmd);
        cheatCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.Header(TextId.CmdDescCheat.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/cheat fever", "[on|off]", TextId.CheatDescFever.Get()));
            ctx.Log(ConsoleFormat.Line);
        });
        root.AddCommand(cheatCmd);

        CommandRegistry.RegisterCompletions("cheat fever", 0, "on", "off");
    }
}
