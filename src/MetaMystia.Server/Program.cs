using MetaMystia.Network.Core;

int port = 40815;
if (args.Length > 0 && (!int.TryParse(args[0], out port) || port is < 1 or > 65535))
{
    Console.Error.WriteLine("Usage: MetaMystia.Server [port] [--ipv6]");
    Environment.ExitCode = 1;
    return;
}
using var host = new EndpointHost(port, ipv6: args.Contains("--ipv6"));
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
Console.WriteLine($"MetaMystia server listening on {host.Port}. Ctrl+C to stop.");
while (!stop.IsCancellationRequested && host.ListenerError == null)
{
    host.Pump(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    Thread.Sleep(5);
}
if (host.ListenerError != null)
{
    Console.Error.WriteLine(host.ListenerError);
    Environment.ExitCode = 1;
}
