using System.Net;

using Common.UI;

using MetaMystia.Network;

namespace MetaMystia.Hosting;

internal static class ServerProbe
{
    internal static async Task Run(string address, int port)
    {
        using var client = new Client();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var connect = client.ConnectAsync(new IPEndPoint(IPAddress.Parse(address), port), new Player
        {
            Name = "probe-" + Guid.NewGuid().ToString("N"), Scene = Scene.MainScene, Stage = GameStage.MainMenu,
            Resources = new() { DlcFlags = DlcPack.Core, PackIds = [] }
        }, deadline.Token);
        while (!connect.IsCompleted)
        {
            client.DispatchPending();
            await Task.Delay(10, deadline.Token);
        }
        await connect;
        Console.WriteLine("TCP handshake OK");
    }
}
