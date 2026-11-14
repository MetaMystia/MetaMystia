using System.Net;
using System.Text.Json;

using MetaMystia.Gateway;

using var host = Host.CreateDefaultBuilder(args).Build();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Gateway");
using var routes = new RouteCache(
    Environment.GetEnvironmentVariable("MM_DIRECTORY_URL") ?? "http://127.0.0.1:40900",
    Environment.GetEnvironmentVariable("MM_INSTANCE_TOKEN") ?? throw new InvalidOperationException("MM_INSTANCE_TOKEN is required"));
await using var gateway = new GatewayServer(new IPEndPoint(
    IPAddress.Parse(Environment.GetEnvironmentVariable("MM_GATEWAY_ADDRESS") ?? "0.0.0.0"),
    int.Parse(Environment.GetEnvironmentVariable("MM_GATEWAY_PORT") ?? "40815")), routes,
    int.Parse(Environment.GetEnvironmentVariable("MM_GATEWAY_MAX_CONNECTIONS") ?? "256"));
gateway.Logged += message => log.LogInformation("{Message}", message);
await host.StartAsync();
await routes.RefreshAsync(lifetime.ApplicationStopping);
gateway.Start();
log.LogInformation("转发入口已监听 {Endpoint}。", gateway.Endpoint);
using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
try
{
    do
    {
        try { await routes.RefreshAsync(lifetime.ApplicationStopping); }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or IOException or InvalidDataException)
        { log.LogWarning("更新路由失败：{Reason}。", error.GetType().Name); }
    } while (await timer.WaitForNextTickAsync(lifetime.ApplicationStopping));
}
catch (OperationCanceledException) when (lifetime.ApplicationStopping.IsCancellationRequested) { }
await host.StopAsync();
