using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using MetaMystia.Discovery;
using MetaMystia.Network;

namespace MetaMystia.Hosting;

public sealed record ServerAdminOptions(string InstanceId, string Url, string Token, string? DirectoryUrl = null)
{
    public static ServerAdminOptions? FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("MM_ADMIN_URL");
        return string.IsNullOrWhiteSpace(url) ? null : new(
            Environment.GetEnvironmentVariable("MM_INSTANCE_ID") ?? "", url,
            Environment.GetEnvironmentVariable("MM_INSTANCE_TOKEN") ?? "",
            Environment.GetEnvironmentVariable("MM_DIRECTORY_URL"));
    }
}

public static class ServerAdministration
{
    public static WebApplication Create(Server server, ServerAdminOptions options, Versions? versions = null)
    {
        if (string.IsNullOrWhiteSpace(options.InstanceId) || string.IsNullOrWhiteSpace(options.Token))
            throw new ArgumentException("Instance ID and token are required");
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

    public static async Task<bool> CanStartAsync(ServerAdminOptions options, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryUrl)) return true;
        using var http = DirectoryClient(options);
        using var response = await http.GetAsync($"internal/instances/{Uri.EscapeDataString(options.InstanceId)}", token);
        if (response.StatusCode == HttpStatusCode.NotFound) return true;
        response.EnsureSuccessStatusCode();
        var record = await response.Content.ReadFromJsonAsync<InstanceRecord>(cancellationToken: token)
            ?? throw new InvalidDataException("Empty instance registration");
        return record.Id == options.InstanceId && record.State is "staged" or "active";
    }

    public static async Task ReportStoppedAsync(ServerAdminOptions options, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(options.DirectoryUrl)) return;
        using var http = DirectoryClient(options);
        using var response = await http.PostAsync($"internal/instances/{Uri.EscapeDataString(options.InstanceId)}/stopped", null, token);
        response.EnsureSuccessStatusCode();
    }

    private static HttpClient DirectoryClient(ServerAdminOptions options)
    {
        var address = new Uri(options.DirectoryUrl!.TrimEnd('/') + "/");
        if (address.Scheme != "https" && !(address.Scheme == "http" && address.IsLoopback))
            throw new ArgumentException("Directory URL requires HTTPS (HTTP is allowed on loopback)");
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        { BaseAddress = address, Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        return client;
    }
}
