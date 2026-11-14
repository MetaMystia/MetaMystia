using System.Net;
using System.Text.Json;

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

internal sealed record PlayerInfo(int Uid, string Name, string? Room, string Scene, string Stage, string Map,
    DateTime ConnectedAt, string Dlc, string[] PackIds);
internal sealed record RoomInfo(string Id, int Host, string? HostName, int Count, int MaxPlayers, bool Joinable);
internal sealed record RetirementInfo(string NoticeId, string TargetModVersion);
internal sealed record SettingsInfo(int ChatFilterWords, bool LogChat, int WelcomeMessages);
internal sealed record InstanceDetails(string InstanceId, string GameVersion, string ModVersion, int Protocol, int Port, string State,
    int MaxPlayers, PlayerInfo[] Players, RoomInfo[] Rooms, RetirementInfo? Retirement, SettingsInfo Settings, ProcessInfo Process);
internal sealed record ActionError(string Error, string? Message = null);
internal sealed record LimitRequest(int Value);
internal sealed record SayRequest(string Message);

public static class ServerAdministration
{
    /// <summary>测试与嵌入用：日志只收集服务端事件，配置从程序目录重载。</summary>
    public static WebApplication Create(Server server, ServerAdminOptions options, Versions? versions = null)
    {
        var logs = new LogBuffer();
        server.Logged += entry => logs.Add(Level(entry.Level), entry.Message);
        return Create(server, options, versions, logs, AppContext.BaseDirectory);
    }

    internal static string Level(ServerLogLevel level) => level.ToString().ToLowerInvariant();

    internal static WebApplication Create(Server server, ServerAdminOptions options, Versions? versions, LogBuffer logs, string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(options.InstanceId) || string.IsNullOrWhiteSpace(options.Token))
            throw new ArgumentException("Instance ID and token are required");
        var address = new Uri(options.Url);
        if (address.Scheme != "http" || !IPAddress.TryParse(address.Host.Trim('[', ']'), out var ip) || !IPAddress.IsLoopback(ip))
            throw new ArgumentException("Management HTTP must bind to loopback");
        var version = versions ?? Versions.Current;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ApplicationName = typeof(ServerAdministration).Assembly.FullName });
        builder.WebHost.UseUrls(options.Url);
        // 面板会轮询，ASP.NET 自带的逐请求日志会刷满 journald。
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
        var app = builder.Build();
        var cpu = new CpuMeter();
        var history = new MetricHistory(["players", "rooms", "cpu", "memoryMb"], async () =>
        {
            var status = await server.GetStatusAsync();
            return [status.Players.Length, status.Rooms.Length, cpu.Next(), Math.Round(Environment.WorkingSet / 1048576.0, 1)];
        }, TimeSpan.FromMinutes(1), 24 * 60);
        app.Lifetime.ApplicationStopping.Register(history.Dispose);
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.Authorization != "Bearer " + options.Token)
            { context.Response.StatusCode = 401; return; }
            context.Response.Headers.CacheControl = "no-store";
            try { await next(context); }
            catch (InvalidOperationException) { context.Response.StatusCode = 503; }
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
        app.MapGet("/admin/details", async () => await Details());
        app.MapGet("/admin/logs", (long? after, int? limit) => logs.Read(after, limit));
        app.MapGet("/admin/history", () => history.Snapshot());
        app.MapPost("/admin/players/{uid:int}/kick", async (int uid) => Result(await server.ManageAsync(ServerCommand.Kick, uid)));
        app.MapPost("/admin/players/{uid:int}/leave", async (int uid) => Result(await server.ManageAsync(ServerCommand.Leave, uid)));
        app.MapPut("/admin/max-players", async (LimitRequest request) => Result(await server.ManageAsync(ServerCommand.MaxPlayers, request.Value)));
        app.MapPost("/admin/say", async (SayRequest request) => Result(await server.SayAsync(request.Message ?? "")));
        app.MapPost("/admin/reload", async () =>
        {
            ServerTextSettings settings;
            try { settings = ServerConfiguration.Load(configDirectory); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidDataException)
            {
                logs.Add("warning", $"[管理] 服务端无法重新加载配置，将继续使用原配置。原因：{ServerLogEntry.Quote(e.Message)}");
                return Results.BadRequest(new ActionError("ConfigInvalid", e.Message));
            }
            return Result(await server.ReloadTextSettingsAsync(settings));
        });
        return app;

        static IResult Result(NetworkErrorCode code) => code switch
        {
            NetworkErrorCode.None => Results.NoContent(),
            NetworkErrorCode.ServerStopped => Results.StatusCode(503),
            _ => Results.Conflict(new ActionError(code.ToString())),
        };

        async Task<InstanceStatus> Status()
        {
            var status = await server.GetStatusAsync();
            return new(options.InstanceId, version.Game, version.Mod, server.Endpoint.Port,
                status.Retirement == null ? "running" : "draining", status.Players.Length,
                status.Retirement?.NoticeId, status.Retirement?.TargetModVersion);
        }

        async Task<InstanceDetails> Details()
        {
            var status = await server.GetStatusAsync();
            var names = status.Players.ToDictionary(p => p.Uid, p => p.Name);
            return new(options.InstanceId, version.Game, version.Mod, version.Protocol, server.Endpoint.Port,
                status.Retirement == null ? "running" : "draining", status.MaxPlayers,
                status.Players.OrderBy(p => p.Uid).Select(p => new PlayerInfo(p.Uid, p.Name, p.Room == 0 ? null : RoomCode.Format(p.Room),
                    p.Scene.ToString(), p.Stage.ToString(), p.Map.ToString(), DateTime.SpecifyKind(p.ConnectedAt, DateTimeKind.Utc),
                    p.DlcFlags.ToString(), p.PackIds)).ToArray(),
                status.Rooms.OrderBy(r => r.Id).Select(r => new RoomInfo(RoomCode.Format(r.Id), r.Host, names.GetValueOrDefault(r.Host),
                    r.Count, r.MaxPlayers, r.Joinable)).ToArray(),
                status.Retirement == null ? null : new(status.Retirement.NoticeId, status.Retirement.TargetModVersion),
                new(status.ChatFilterWords, status.LogChat, status.WelcomeMessages),
                ProcessMetrics.Snapshot());
        }
    }
}
