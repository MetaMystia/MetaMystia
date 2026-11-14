using System.Net;

using MetaMystia.Network;

namespace MetaMystia.Hosting;

public sealed record ServerAdminOptions(string InstanceId, string Url, string Token)
{
    public static ServerAdminOptions? FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("MM_ADMIN_URL");
        return string.IsNullOrWhiteSpace(url) ? null : new(
            Environment.GetEnvironmentVariable("MM_INSTANCE_ID") ?? "", url,
            Environment.GetEnvironmentVariable("MM_INSTANCE_TOKEN") ?? "");
    }
}

public static class ServerAdministration
{
    public static WebApplication Create(Server server, ServerAdminOptions options, Versions? versions = null)
    {
        if (string.IsNullOrWhiteSpace(options.InstanceId) || string.IsNullOrWhiteSpace(options.Token))
            throw new ArgumentException("Instance ID and token are required");
        var address = new Uri(options.Url);
        if (address.Scheme != "http" || !IPAddress.TryParse(address.Host.Trim('[', ']'), out var ip) || !IPAddress.IsLoopback(ip))
            throw new ArgumentException("Management HTTP must bind to loopback");
        var version = versions ?? Versions.Current;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ApplicationName = typeof(ServerAdministration).Assembly.FullName });
        builder.WebHost.UseUrls(options.Url);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.Authorization != "Bearer " + options.Token)
            { context.Response.StatusCode = 401; return; }
            context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.MapGet("/admin/status", async () => await Status());
        app.MapPost("/admin/drain", async (DrainRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.NoticeId) || request.NoticeId.Length > 128 ||
                string.IsNullOrWhiteSpace(request.TargetModVersion) || request.TargetModVersion.Length > 64)
                return Results.BadRequest();
            var result = await server.DrainAsync(request.NoticeId, request.TargetModVersion);
            return result == NetworkErrorCode.None ? Results.Accepted(value: await Status()) : Results.StatusCode(503);
        });
        return app;

        async Task<InstanceStatus> Status()
        {
            var status = await server.GetStatusAsync();
            return new(options.InstanceId, version.Game, version.Mod, server.Endpoint.Port,
                status.Retirement == null ? "running" : "draining", status.Players.Length,
                status.Retirement?.NoticeId, status.Retirement?.TargetModVersion);
        }
    }

}
