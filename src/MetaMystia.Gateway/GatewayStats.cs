using System.Collections.Concurrent;
using System.Net;

using MetaMystia.Hosting;
using MetaMystia.Network;

namespace MetaMystia.Gateway;

/// <summary>一条带有效 Hello 的连接。IP 只保存在内存中，不写入日志文件。</summary>
public sealed class ConnectionRecord
{
    internal long bytesIn, bytesOut;
    public long Id { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public string Ip { get; init; } = "";
    public int Port { get; init; }
    public string GameVersion { get; init; } = "";
    public string ModVersion { get; init; } = "";
    public string? InstanceId { get; internal set; }
    /// <summary>forwarding、closed 或 rejected。</summary>
    public string State { get; internal set; } = "forwarding";
    public string? Reason { get; internal set; }
    public DateTimeOffset? EndedAt { get; internal set; }
    public long BytesIn => Interlocked.Read(ref bytesIn);
    public long BytesOut => Interlocked.Read(ref bytesOut);
}

public sealed record GatewayTotals(long Accepted, long Forwarded, long Invalid, long Overflow, Dictionary<string, long> Rejected);

public sealed class GatewayStats : IDisposable
{
    private readonly object gate = new();
    private readonly LinkedList<ConnectionRecord> records = new();
    private readonly ConcurrentDictionary<string, long> rejected = new();
    private readonly int capacity;
    private long sequence, accepted, forwarded, invalid, overflow, active;
    private long sampledForwarded, sampledRejected;
    internal LogBuffer Logs { get; } = new();
    internal MetricHistory History { get; }

    public GatewayStats(int capacity = 2000)
    {
        this.capacity = capacity;
        History = new(["connections", "forwarded", "rejected"], () =>
        {
            long forwardedNow = Interlocked.Read(ref forwarded), rejectedNow = rejected.Values.Sum();
            double[] values = [Active, forwardedNow - sampledForwarded, rejectedNow - sampledRejected];
            sampledForwarded = forwardedNow; sampledRejected = rejectedNow;
            return Task.FromResult(values);
        }, TimeSpan.FromMinutes(1), 24 * 60);
    }

    public int Active => (int)Interlocked.Read(ref active);

    public GatewayTotals Totals => new(Interlocked.Read(ref accepted), Interlocked.Read(ref forwarded), Interlocked.Read(ref invalid),
        Interlocked.Read(ref overflow), rejected.ToDictionary(p => p.Key, p => p.Value));

    public ConnectionRecord[] Recent(int limit)
    {
        lock (gate) return records.Take(Math.Clamp(limit, 1, capacity)).ToArray();
    }

    internal void Opened() { Interlocked.Increment(ref accepted); Interlocked.Increment(ref active); }
    internal void Closed() => Interlocked.Decrement(ref active);
    internal void Overflowed() => Interlocked.Increment(ref overflow);
    internal void Invalid() => Interlocked.Increment(ref invalid);

    internal ConnectionRecord Begin(IPEndPoint client, Versions versions)
    {
        var address = client.Address.IsIPv4MappedToIPv6 ? client.Address.MapToIPv4() : client.Address;
        var record = new ConnectionRecord
        {
            Id = Interlocked.Increment(ref sequence), StartedAt = DateTimeOffset.UtcNow, Ip = address.ToString(), Port = client.Port,
            GameVersion = versions.Game, ModVersion = versions.Mod,
        };
        lock (gate)
        {
            records.AddFirst(record);
            while (records.Count > capacity) records.RemoveLast();
        }
        return record;
    }

    internal void Forwarding(ConnectionRecord record, string instanceId)
    {
        record.InstanceId = instanceId;
        Interlocked.Increment(ref forwarded);
    }

    internal void Rejected(ConnectionRecord record, NetworkErrorCode code)
    {
        record.State = "rejected";
        record.Reason = code.ToString();
        record.EndedAt = DateTimeOffset.UtcNow;
        rejected.AddOrUpdate(record.Reason, 1, (_, count) => count + 1);
    }

    internal void Ended(ConnectionRecord record)
    {
        if (record.State != "forwarding") return;
        record.State = "closed";
        record.EndedAt = DateTimeOffset.UtcNow;
    }

    public void Dispose() => History.Dispose();
}
