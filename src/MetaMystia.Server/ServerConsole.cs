using System.Text;

using MetaMystia.Network;

namespace MetaMystia.Hosting;

internal sealed class ServerConsole : IDisposable
{
    private readonly object gate = new();
    private StreamWriter? file;

    public ServerConsole(string directory)
    {
        Directory.CreateDirectory(directory);
        file = new StreamWriter(Path.Combine(directory, $"{DateTime.Now:yyyy-MM-dd}.log"), append: true, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Write(ServerLogEntry entry) => Write(entry.Level, entry.Message);

    public void Write(ServerLogLevel level, string message)
    {
        lock (gate)
        {
            string label = level == ServerLogLevel.Warning ? "WARN" : level.ToString().ToUpperInvariant();
            string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} [{label}] {message}";
            Console.WriteLine(line);
            try { file?.WriteLine(line); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                file = null;
                Console.Error.WriteLine($"日志文件写入失败，后续仅输出到控制台：{e.Message}");
            }
        }
    }

    public void Dispose()
    {
        lock (gate) file?.Dispose();
    }
}
