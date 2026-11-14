using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MetaMystia.Hosting;

// Gateway 与实例共用的管理面板数据；只在内存中保留，进程重启即清空。

internal sealed record LogRecord(long Seq, DateTimeOffset Time, string Level, string Message);
internal sealed record LogPage(long Next, LogRecord[] Entries);

internal sealed class LogBuffer(int capacity = 2000)
{
    private readonly object gate = new();
    private readonly Queue<LogRecord> entries = new();
    private long seq;

    public void Add(string level, string message)
    {
        lock (gate)
        {
            entries.Enqueue(new(++seq, DateTimeOffset.UtcNow, level, message));
            while (entries.Count > capacity) entries.Dequeue();
        }
    }

    /// <summary>after 为空或超出当前序号（进程已重启）时返回最近 limit 条；否则返回其后的最早 limit 条。</summary>
    public LogPage Read(long? after, int? limit)
    {
        int count = Math.Clamp(limit ?? 500, 1, 1000);
        lock (gate)
        {
            var page = after is { } start && start >= 0 && start <= seq
                ? entries.Where(e => e.Seq > start).Take(count).ToArray()
                : entries.Skip(Math.Max(0, entries.Count - count)).ToArray();
            return new(page.Length > 0 ? page[^1].Seq : Math.Min(after ?? seq, seq), page);
        }
    }
}

internal sealed record ProcessInfo(DateTimeOffset StartedAt, double UptimeSeconds, long WorkingSetBytes, long ManagedBytes,
    double CpuSeconds, int Threads, int[] GcCollections, int Processors, string Runtime);

internal static class ProcessMetrics
{
    private static readonly DateTimeOffset started = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public static ProcessInfo Snapshot()
    {
        using var process = Process.GetCurrentProcess();
        return new(started, (DateTimeOffset.UtcNow - started).TotalSeconds, process.WorkingSet64, GC.GetTotalMemory(false),
            process.TotalProcessorTime.TotalSeconds, process.Threads.Count,
            [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)], Environment.ProcessorCount,
            RuntimeInformation.FrameworkDescription);
    }

    public static double CpuSeconds()
    {
        using var process = Process.GetCurrentProcess();
        return process.TotalProcessorTime.TotalSeconds;
    }
}

internal sealed record HistorySnapshot(int IntervalSeconds, string[] Names, double[][] Points);

/// <summary>按固定间隔采样，每个点为 [Unix 毫秒, 值...]。</summary>
internal sealed class MetricHistory : IDisposable
{
    private readonly object gate = new();
    private readonly Queue<double[]> points = new();
    private readonly string[] names;
    private readonly Func<Task<double[]>> sample;
    private readonly TimeSpan interval;
    private readonly int capacity;
    private readonly CancellationTokenSource stop = new();

    public MetricHistory(string[] names, Func<Task<double[]>> sample, TimeSpan interval, int capacity)
    {
        this.names = names;
        this.sample = sample;
        this.interval = interval;
        this.capacity = capacity;
        _ = Run();
    }

    private async Task Run()
    {
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                var values = await sample().ConfigureAwait(false);
                if (values.Length != names.Length) continue;
                lock (gate)
                {
                    points.Enqueue([DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), .. values]);
                    while (points.Count > capacity) points.Dequeue();
                }
            }
            catch (Exception error) when (error is InvalidOperationException or ObjectDisposedException) { }
        }
        while (await Wait(timer).ConfigureAwait(false));
    }

    private async Task<bool> Wait(PeriodicTimer timer)
    {
        try { return await timer.WaitForNextTickAsync(stop.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return false; }
    }

    public HistorySnapshot Snapshot()
    {
        lock (gate) return new((int)interval.TotalSeconds, names, points.ToArray());
    }

    public void Dispose() { stop.Cancel(); stop.Dispose(); }
}

/// <summary>两次采样之间的 CPU 占用百分比（按单核计，可超过 100）。</summary>
internal sealed class CpuMeter
{
    private double lastCpu = ProcessMetrics.CpuSeconds();
    private long lastTicks = Stopwatch.GetTimestamp();

    public double Next()
    {
        double cpu = ProcessMetrics.CpuSeconds();
        long ticks = Stopwatch.GetTimestamp();
        double elapsed = (ticks - lastTicks) / (double)Stopwatch.Frequency;
        double percent = elapsed > 0 ? (cpu - lastCpu) / elapsed * 100 : 0;
        lastCpu = cpu; lastTicks = ticks;
        return Math.Round(Math.Max(0, percent), 1);
    }
}
