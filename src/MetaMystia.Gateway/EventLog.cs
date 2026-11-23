namespace MetaMystia.Gateway;

public sealed record VersionEvent(long Seq, DateTimeOffset Time, string Type, string InstanceId, string GameVersion, string ModVersion,
    string? TargetModVersion);
public sealed record EventPage(long Next, bool Reset, VersionEvent[] Events);

/// <summary>路由变化事件：registered、draining、removed。写入文件，Gateway 重启后序号延续；只保留最近 500 条。</summary>
public sealed class EventLog
{
    private const int Capacity = 500;
    private const int PageSize = 100;
    private readonly object gate = new();
    private readonly string path;
    private VersionEvent[] events;
    private TaskCompletionSource changed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public EventLog(string path)
    {
        this.path = Path.GetFullPath(path);
        events = JsonFile.Read<VersionEvent>(this.path);
    }

    public long Seq { get { lock (gate) return Last; } }
    private long Last => events.Length > 0 ? events[^1].Seq : 0;

    public void Add(string type, InstanceRecord record)
    {
        lock (gate)
        {
            var next = events.Append(new(Last + 1, DateTimeOffset.UtcNow, type, record.Id, record.Registration.GameVersion,
                record.Registration.ModVersion, record.TargetModVersion)).TakeLast(Capacity).ToArray();
            JsonFile.Write(path, next);
            events = next;
            changed.TrySetResult();
            changed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>after 之后没有事件时最多等待 wait；after 超出当前序号或早于保留范围时返回 Reset，调用方应重新读取状态。</summary>
    public async Task<EventPage> ReadAsync(long after, TimeSpan wait, CancellationToken token)
    {
        Task signal;
        lock (gate)
        {
            var page = Page(after);
            if (page.Reset || page.Events.Length > 0 || wait <= TimeSpan.Zero) return page;
            signal = changed.Task;
        }
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            await Task.WhenAny(signal, Task.Delay(wait, timeout.Token));
            timeout.Cancel();
        }
        lock (gate) return Page(after);
    }

    private EventPage Page(long after)
    {
        if (after > Last || (events.Length > 0 && after < events[0].Seq - 1)) return new(Last, true, []);
        var page = events.Where(e => e.Seq > after).Take(PageSize).ToArray();
        return new(page.Length > 0 ? page[^1].Seq : Last, false, page);
    }
}
