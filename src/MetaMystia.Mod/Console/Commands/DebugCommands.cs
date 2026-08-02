using System.CommandLine;
using System.CommandLine.Invocation;

using MetaMystia.UI;

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
            ctx.Log(MpManager.DebugText);
        });
        root.AddCommand(debugCmd);

        CommandRegistry.RegisterCompletions("debug", 0, "kill");
        CommandRegistry.RegisterHint("debug kill", 0, "<guest id>");

        // /webdebug start <key>
        var webDebugCmd = new Command("webdebug", "Web debugger management");
        var startCmd = new Command("start", "Start the web debugger");
        var keyArg = new Argument<string>("key", "Security confirmation key");
        startCmd.AddArgument(keyArg);
        startCmd.SetHandler(ctx =>
        {
            string key = ctx.ParseResult.GetValueForArgument(keyArg);
            if (key != "我已知晓风险并同意启动Web调试器")
            {
                ctx.Log(TextId.InvalidWebDebuggerKey.Get());
                return;
            }
            PluginManager.Debugger ??= new Debugger.WebDebugger();
            PluginManager.Debugger?.Start();
            ctx.Log(TextId.WebDebuggerStarted.Get());
        });
        webDebugCmd.AddCommand(startCmd);

        // Default handler for /webdebug without subcommand
        webDebugCmd.SetHandler(ctx =>
        {
            ctx.Log($"{ConsoleFormat.Cmd("/webdebug start")} {ConsoleFormat.Arg("<key>")}  {ConsoleFormat.Dim("Start web debugger")}");
        });

        root.AddCommand(webDebugCmd);

        CommandRegistry.RegisterCompletions("webdebug", 0, "start");
        CommandRegistry.RegisterHint("webdebug start", 0, "<security key>");
    }
}
