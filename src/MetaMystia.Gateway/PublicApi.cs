using System.Text.Json;

using MetaMystia.Hosting;

namespace MetaMystia.Gateway;

public sealed record PublicInstance(string Id, string GameVersion, string ModVersion, string State, string? TargetModVersion,
    bool Reachable, int? Players, int? MaxPlayers, int? Rooms);
public sealed record PublicStatus(DateTimeOffset Time, long Seq, int Connections, int Players, PublicInstance[] Instances);

/// <summary>只读令牌可用的接口：只给汇总数据，不含 IP、UID、昵称与配置。</summary>
public static class PublicApi
{
    private static readonly TimeSpan cacheTime = TimeSpan.FromSeconds(5);
    private const int MaxWait = 60;
    private const int MaxWaitingPerCaller = 4;

    public static void Map(WebApplication app, InstanceRegistry registry, EventLog events, GatewayStats stats)
    {
        var gate = new object();
        Task<PublicStatus>? cached = null;
        var cachedAt = DateTimeOffset.MinValue;
        // 缓存进行中的查询，频繁轮询不会逐次访问实例。
        app.MapGet("/api/v1/status", () =>
        {
            lock (gate)
            {
                if (cached == null || DateTimeOffset.UtcNow - cachedAt > cacheTime)
                { cached = Snapshot(registry, events, stats); cachedAt = DateTimeOffset.UtcNow; }
                return cached;
            }
        });

        var waiting = new Dictionary<string, int>();
        app.MapGet("/api/v1/events", async (HttpContext context, long after, int? wait) =>
        {
            var caller = (Caller)context.Items[nameof(Caller)]!;
            lock (waiting)
            {
                var count = waiting.GetValueOrDefault(caller.Id);
                if (count >= MaxWaitingPerCaller) return Results.StatusCode(429);
                waiting[caller.Id] = count + 1;
            }
            try { return Results.Ok(await events.ReadAsync(after, TimeSpan.FromSeconds(Math.Clamp(wait ?? 30, 0, MaxWait)), context.RequestAborted)); }
            finally { lock (waiting) if (--waiting[caller.Id] == 0) waiting.Remove(caller.Id); }
        });
    }

    private static async Task<PublicStatus> Snapshot(InstanceRegistry registry, EventLog events, GatewayStats stats)
    {
        // 先取序号再取登记：之后的变化一定在 seq 之后，订阅方不会漏掉。
        var seq = events.Seq;
        var instances = await Task.WhenAll(registry.List().Select(async record =>
        {
            InstanceStatus? status = null;
            try { status = await registry.StatusAsync(record, CancellationToken.None); }
            catch (Exception error) when (error is HttpRequestException or TaskCanceledException or InvalidDataException or JsonException) { }
            var r = record.Registration;
            return new PublicInstance(record.Id, r.GameVersion, r.ModVersion, record.State, record.TargetModVersion,
                status != null, status?.Players, status?.MaxPlayers, status?.Rooms);
        }));
        return new(DateTimeOffset.UtcNow, seq, stats.Active, instances.Sum(i => i.Players ?? 0), instances);
    }
}
