using System.Globalization;
using System.Text;

using MetaMystia.Network;

namespace MetaMystia.Hosting;

public sealed class ServerConsole : IDisposable
{
    private readonly object gate = new();
    private readonly string directory;
    private readonly int retentionDays;
    private readonly Func<DateTimeOffset> clock;
    private readonly TextWriter output;
    private StreamWriter? file;
    private DateOnly currentDate;
    private bool fileFailed;

    public ServerConsole(string directory, int retentionDays = 14, Func<DateTimeOffset>? clock = null, TextWriter? output = null)
    {
        this.directory = Path.GetFullPath(directory);
        this.retentionDays = Math.Max(0, retentionDays);
        this.clock = clock ?? (() => DateTimeOffset.Now);
        this.output = output ?? Console.Out;
        Directory.CreateDirectory(this.directory);
        OpenForDate(DateOnly.FromDateTime(this.clock().DateTime));
    }

    public void Write(ServerLogEntry entry) => Write(entry.Level, entry.Message);

    public void Write(ServerLogLevel level, string message)
    {
        lock (gate)
        {
            var now = clock();
            string label = level == ServerLogLevel.Warning ? "WARN" : level == ServerLogLevel.Chat ? "INFO" : level.ToString().ToUpperInvariant();
            string line = $"{now:yyyy-MM-dd HH:mm:ss zzz} [{label}] {message}";
            output.WriteLine(line);
            if (fileFailed) return;
            var date = DateOnly.FromDateTime(now.DateTime);
            try
            {
                if (file == null || date != currentDate) OpenForDate(date);
                file?.WriteLine(line);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                DisableFile(now, e);
            }
        }
    }

    private void OpenForDate(DateOnly date)
    {
        file?.Dispose();
        file = null;
        currentDate = date;
        Directory.CreateDirectory(directory);
        file = new StreamWriter(Path.Combine(directory, $"{date:yyyy-MM-dd}.log"), append: true, new UTF8Encoding(false)) { AutoFlush = true };
        PruneExpiredFiles(date);
    }

    private void PruneExpiredFiles(DateOnly today)
    {
        if (retentionDays <= 0) return;
        var cutoff = today.AddDays(-retentionDays);
        try
        {
            foreach (var path in Directory.EnumerateFiles(directory, "????-??-??.log"))
            {
                var stem = Path.GetFileNameWithoutExtension(path);
                if (DateOnly.TryParseExact(stem, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate)
                    && fileDate < cutoff)
                    File.Delete(path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private void DisableFile(DateTimeOffset now, Exception error)
    {
        fileFailed = true;
        try { file?.Dispose(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        file = null;
        Console.Error.WriteLine($"{now:yyyy-MM-dd HH:mm:ss zzz} [ERROR] [日志] 服务端无法写入日志文件，后续日志将仅输出到控制台。原因：{ServerLogEntry.Quote(error.Message)}");
    }

    public void Dispose()
    {
        lock (gate)
        {
            file?.Dispose();
            file = null;
        }
    }
}
