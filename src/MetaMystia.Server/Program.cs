using System.Net;
using System.Text;

using MetaMystia.Hosting;
using MetaMystia.Network;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
var port = args.Length > 0 ? int.Parse(args[0]) : 40815;
var limit = args.Length > 1 ? int.Parse(args[1]) : 16;
var config = ServerConfiguration.Load(AppContext.BaseDirectory);
using var log = new ServerConsole(Path.Combine(AppContext.BaseDirectory, "logs"));
await using var server = new Server(new ServerOptions
{
    Address = IPAddress.Any, Port = port, MaxPlayers = limit, Messages = GameMessageRules.Create(),
    ChatFilter = config.ChatFilter, LogChat = config.LogChat, WelcomeMessages = config.WelcomeMessages
});
server.Logged += log.Write;
server.CallbackError += error => log.Write(ServerLogLevel.Error, $"服务端回调异常：{ServerLogEntry.Quote(error.ToString())}");
var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; done.TrySetResult(true); };
Console.CancelKeyPress += cancel;
await server.StartAsync();
log.Write(ServerLogLevel.Info, $"服务端启动，监听 {server.Endpoint}，人数上限 {limit}；{Versions.Current}");
log.Write(ServerLogLevel.Info, $"聊天过滤{(config.ChatFilter.Enabled ? "启用" : "关闭")}，有效词条 {config.ChatFilter.Words.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().Count()}；聊天正文日志{(config.LogChat ? "开启" : "关闭")}");
log.Write(ServerLogLevel.Info, "输入 help 查看命令；回车执行命令；stop 或 Ctrl+C 关闭。");
while (!done.Task.IsCompleted)
{
    var input = Task.Run(Console.ReadLine);
    if (await Task.WhenAny(done.Task, input) == done.Task) break;
    var line = await input;
    if (line == null)
    {
        log.Write(ServerLogLevel.Info, "标准输入已关闭，服务端继续运行，可用 Ctrl+C 关闭。");
        await done.Task;
        break;
    }
    if (!await ServerCommands.Execute(line, server, log.Write)) break;
}
log.Write(ServerLogLevel.Info, "服务端正在关闭。");
await server.StopAsync();
Console.CancelKeyPress -= cancel;
log.Write(ServerLogLevel.Info, "服务端已停止。");
