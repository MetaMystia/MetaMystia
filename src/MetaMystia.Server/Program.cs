using System.Net;
using System.Text;

using MetaMystia.Hosting;
using MetaMystia.Network;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
if (args.Length == 3 && args[0] == "--probe")
{
    await ServerProbe.Run(args[1], int.Parse(args[2]));
    return;
}
var port = args.Length > 0 ? int.Parse(args[0]) : 40815;
var limit = args.Length > 1 ? int.Parse(args[1]) : 16;
var adminOptions = ServerAdminOptions.FromEnvironment();
if (adminOptions != null && !await ServerAdministration.CanStartAsync(adminOptions))
{
    await ServerAdministration.ReportStoppedAsync(adminOptions);
    return;
}
var config = ServerConfiguration.Load(AppContext.BaseDirectory);
using var log = new ServerConsole(Path.Combine(AppContext.BaseDirectory, "logs"));
await using var server = new Server(new ServerOptions
{
    Address = IPAddress.Parse(Environment.GetEnvironmentVariable("MM_GAME_ADDRESS") ?? "0.0.0.0"),
    Port = port, MaxPlayers = limit, Messages = GameMessageRules.Create(),
    ChatFilter = config.ChatFilter, LogChat = config.LogChat, WelcomeMessages = config.WelcomeMessages
});
server.Logged += log.Write;
server.CallbackError += error => log.Write(ServerLogLevel.Error, "[系统] 服务端执行回调时发生异常：\n"
    + string.Join("\n", error.ToString().Split('\n').Select(line => "    " + ServerLogEntry.Quote(line.TrimEnd('\r')))));
var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; done.TrySetResult(true); };
Console.CancelKeyPress += cancel;
await server.StartAsync();
await using var admin = adminOptions == null ? null : ServerAdministration.Create(server, adminOptions);
if (admin != null) await admin.StartAsync();
using var adminStopping = admin?.Lifetime.ApplicationStopping.Register(() => done.TrySetResult(true));
var ending = Task.WhenAny(done.Task, server.Drained);
log.Write(ServerLogLevel.Info, $"[系统] 服务端已在 {server.Endpoint} 启动，最多允许 {limit} 名玩家连接。");
log.Write(ServerLogLevel.Info, $"[系统] 当前模组版本为 {Versions.Current.Mod}，游戏版本为 {Versions.Current.Game}，协议版本为 {Versions.Current.Protocol}。");
log.Write(ServerLogLevel.Info, $"[系统] 服务端已{(config.ChatFilter.Enabled ? "启用" : "关闭")}敏感词过滤，共配置 {config.ChatFilter.Words.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().Count()} 个有效词条，聊天正文记录已{(config.LogChat ? "开启" : "关闭")}。");
log.Write(ServerLogLevel.Info, "[系统] 输入 help 查看命令；回车执行命令；stop 或 Ctrl+C 关闭。");
while (!ending.IsCompleted)
{
    var input = Task.Run(Console.ReadLine);
    if (await Task.WhenAny(ending, input) == ending) break;
    var line = await input;
    if (line == null)
    {
        log.Write(ServerLogLevel.Info, "[系统] 控制台输入已关闭，服务端继续运行。");
        await ending;
        break;
    }
    if (!await ServerCommands.Execute(line, server, log.Write)) break;
}
log.Write(ServerLogLevel.Info, "[系统] 服务端正在关闭。");
if (admin != null) await admin.StopAsync();
await server.StopAsync();
if (server.Drained.IsCompleted && adminOptions != null)
{
    try { await ServerAdministration.ReportStoppedAsync(adminOptions); }
    catch (Exception error) when (error is HttpRequestException or OperationCanceledException)
    { log.Write(ServerLogLevel.Warning, $"[系统] 服务端无法完成退出登记，服务目录仍保留退役状态。原因：{error.GetType().Name}。"); }
}
Console.CancelKeyPress -= cancel;
log.Write(ServerLogLevel.Info, "[系统] 服务端已停止。");
