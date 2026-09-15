using System.Net;

using MetaMystia.Network;

var port = args.Length > 0 ? int.Parse(args[0]) : 40815;
var limit = args.Length > 1 ? int.Parse(args[1]) : 16;
await using var server = new Server(new ServerOptions { Address = IPAddress.Any, Port = port, MaxPlayers = limit, Messages = GameMessageRules.Create() });
server.CallbackError += error => Console.Error.WriteLine(error);
await server.StartAsync();
Console.WriteLine($"TCP {server.Endpoint}; World 上限 {limit}; {Versions.Current}");
var done = new TaskCompletionSource<bool>();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(true); };
Console.WriteLine("按 Enter 或 Ctrl+C 关闭。");
await Task.WhenAny(done.Task, Task.Run(Console.ReadLine));
