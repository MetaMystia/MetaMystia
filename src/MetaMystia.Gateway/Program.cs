using System.Net;

using MetaMystia.Gateway;

await using var app = GatewayApi.Create(
    Environment.GetEnvironmentVariable("MM_ADMIN_URL") ?? "http://127.0.0.1:40900",
    Environment.GetEnvironmentVariable("MM_GATEWAY_DATA") ?? Path.Combine(AppContext.BaseDirectory, "data", "instances.json"),
    Environment.GetEnvironmentVariable("MM_GATEWAY_TOKEN") ?? "",
    Environment.GetEnvironmentVariable("MM_INSTANCE_TOKEN") ?? "");
await using var gateway = new GatewayServer(new IPEndPoint(
    IPAddress.Parse(Environment.GetEnvironmentVariable("MM_GATEWAY_ADDRESS") ?? "0.0.0.0"),
    int.Parse(Environment.GetEnvironmentVariable("MM_GATEWAY_PORT") ?? "40815")),
    app.Services.GetRequiredService<InstanceRegistry>(),
    int.Parse(Environment.GetEnvironmentVariable("MM_GATEWAY_MAX_CONNECTIONS") ?? "256"));
gateway.Logged += message => app.Logger.LogInformation("{Message}", message);
app.MapGet("/admin/status", () => new { connections = gateway.ConnectionCount });
gateway.Start();
await app.RunAsync();
