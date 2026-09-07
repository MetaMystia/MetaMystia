using System.CommandLine;
using System.Globalization;

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

        var flowRateCmd = new Command("flowrate", TextId.CheatDescFlowRate.Get());
        var flowRateArg = new Argument<string>("num", () => "1")
        {
            Arity = ArgumentArity.ZeroOrOne
        };
        flowRateCmd.AddArgument(flowRateArg);
        flowRateCmd.SetHandler(ctx =>
        {
            string raw = ctx.ParseResult.GetValueForArgument(flowRateArg);
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float rate)
                || float.IsNaN(rate) || rate < 0f || rate >= 16f)
            {
                ctx.Log(ConsoleFormat.Err(TextId.CheatFlowRateInvalid.Get(raw)));
                return;
            }

            ConfigManager.CheatFlowRate.Value = rate;
            ctx.Log(TextId.CheatFlowRateApplied.Get(rate.ToString("0.###", CultureInfo.InvariantCulture)));
        });
        cheatCmd.AddCommand(flowRateCmd);

        cheatCmd.SetHandler(ctx =>
        {
            ctx.Log(ConsoleFormat.Header(TextId.CmdDescCheat.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/cheat fever", "[on|off]", TextId.CheatDescFever.Get()));
            ctx.Log(ConsoleFormat.SubCmd("/cheat flowrate", "[num]", TextId.CheatDescFlowRate.Get()));
            ctx.Log(ConsoleFormat.Line);
        });
        root.AddCommand(cheatCmd);

        CommandRegistry.RegisterCompletions("cheat fever", 0, "on", "off");
        CommandRegistry.RegisterHint("cheat flowrate", 0, "[0 ≤ num < 16]");
    }
}
