using System.Runtime.CompilerServices;
using System.Text;

namespace MetaMystia.Logging;

public enum ModLogLevel : byte { Off, Fatal, Error, Warning, Message, Info, Debug }

public readonly struct ModLogger(string category)
{
    public string Category { get; } = string.IsNullOrWhiteSpace(category) ? "Core" : category;
    public bool IsEnabled(ModLogLevel level) => ModLog.IsEnabled(Category, level);
    public bool IsOutputEnabled(ModLogLevel level) => ModLog.IsOutputEnabled(Category, level);
    public void Write(ModLogLevel level, string message, Exception? ex = null) => ModLog.Write(Category, level, message, ex);
    public void Debug(string message) => ModLog.Write(Category, ModLogLevel.Debug, message);
    public void Info(string message) => ModLog.Write(Category, ModLogLevel.Info, message);
    public void Message(string message) => ModLog.Write(Category, ModLogLevel.Message, message);
    public void Warning(string message) => ModLog.Write(Category, ModLogLevel.Warning, message);
    public void Warning(Exception ex, string message) => ModLog.Write(Category, ModLogLevel.Warning, message, ex);
    public void Error(string message) => ModLog.Write(Category, ModLogLevel.Error, message);
    public void Error(Exception ex, string message) => ModLog.Write(Category, ModLogLevel.Error, message, ex);
    public void Fatal(string message) => ModLog.Write(Category, ModLogLevel.Fatal, message);
    public void Fatal(Exception ex, string message) => ModLog.Write(Category, ModLogLevel.Fatal, message, ex);
    public void DebugCaller(string message, [CallerMemberName] string caller = "") => ModLog.Write(Category, ModLogLevel.Debug, $"[{caller}] {message}");
    public void InfoCaller(string message, [CallerMemberName] string caller = "") => ModLog.Write(Category, ModLogLevel.Info, $"[{caller}] {message}");
}

public static class ModLog
{
    public static readonly ModLogger Core = For("Core");
    public static readonly ModLogger Patch = For("Patch");
    public static readonly ModLogger Net = For("Net");
    public static readonly ModLogger NetMsg = For("Net.Msg");
    public static readonly ModLogger LanServer = For("Net.Lan");
    public static readonly ModLogger Guest = For("Sync.Guest");
    public static readonly ModLogger Yuyuko = For("Sync.Yuyuko");
    public static readonly ModLogger ResourceEx = For("ResourceEx");
    public static readonly ModLogger Skin = For("Skin");
    public static readonly ModLogger UI = For("UI");
    public static readonly ModLogger Console = For("Console");
    public static readonly ModLogger Trace = For("Trace");

    private sealed class Bucket(DateTimeOffset start)
    {
        public DateTimeOffset Start = start, Last = start;
        public int Emitted = 1, Suppressed;
        public string Preview = "";
        public ModLogLevel Level;
        public string Category = "";
    }

    private static readonly object Gate = new();
    private static readonly Dictionary<(string Category, ModLogLevel Level, string Sig), Bucket> Bursts = new();
    private static readonly Queue<string> Recorder = new();
    private static volatile (string Prefix, ModLogLevel Level)[] rules = [];
    private static volatile ModLogLevel defaultLevel = ModLogLevel.Info;
    private static volatile Action<ModLogLevel, string>? output;
    private static volatile Func<string?>? contextProvider;
    private static volatile Func<string?>? dumpSummaryProvider;
    private static volatile Func<DateTimeOffset>? clock;
    private static volatile string? logDir;
    private static volatile int recorderCapacity;
    private static int mainThreadId, markerId, burstLimit = 3;
    private static TimeSpan burstWindow = TimeSpan.FromSeconds(10);
    private static long totalWritten, totalSuppressed, lastAutoDumpTicks;
    private static StreamWriter? file;
    private static string? fileBase, filePath;
    private static int filePart, maxRetainedFiles = 10;
    private static long fileBytes, maxFileBytes = 10 * 1024 * 1024;

    public static ModLogLevel DefaultLevel => defaultLevel;
    public static string? CurrentFilePath => filePath;
    public static long TotalWritten => Volatile.Read(ref totalWritten);
    public static long TotalSuppressed => Volatile.Read(ref totalSuppressed);
    public static int RecorderCount { get { lock (Gate) return Recorder.Count; } }
    public static int RecorderCapacity => recorderCapacity;

    public static ModLogger For(string category) => new(category);
    public static void SetMainThread(int threadId) => Volatile.Write(ref mainThreadId, threadId);
    public static void SetContextProvider(Func<string?>? provider) => contextProvider = provider;
    public static void SetDumpSummaryProvider(Func<string?>? provider) => dumpSummaryProvider = provider;
    public static void SetClock(Func<DateTimeOffset>? provider) => clock = provider;
    public static void SetOutput(Action<ModLogLevel, string>? writer) => output = writer;
    public static void SetBurstPolicy(int limit, TimeSpan window) { lock (Gate) { burstLimit = Math.Max(1, limit); burstWindow = window; } }

    public static bool TryParseLevel(string? text, out ModLogLevel level) =>
        Enum.TryParse(text?.Trim() switch { "warn" or "WARN" or "Warn" => "Warning", var s => s }, ignoreCase: true, out level);

    public static void ConfigureLevels(ModLogLevel fallback, string? spec = null)
    {
        var dict = new Dictionary<string, ModLogLevel>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(spec))
            foreach (var part in spec.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (part.IndexOf('=') is > 0 and var eq && TryParseLevel(part[(eq + 1)..], out var lvl))
                    dict[part[..eq].Trim()] = lvl;
        lock (Gate)
        {
            defaultLevel = fallback;
            rules = dict.Select(kv => (kv.Key, kv.Value)).OrderByDescending(r => r.Key.Length).ToArray();
        }
    }

    public static void SetCategoryLevel(string prefix, ModLogLevel? level)
    {
        lock (Gate)
        {
            var dict = rules.ToDictionary(r => r.Prefix, r => r.Level, StringComparer.OrdinalIgnoreCase);
            if (level is { } lvl) dict[prefix.Trim()] = lvl;
            else dict.Remove(prefix.Trim());
            rules = dict.Select(kv => (kv.Key, kv.Value)).OrderByDescending(r => r.Key.Length).ToArray();
        }
    }

    public static ModLogLevel GetLevel(string category)
    {
        var current = rules;
        for (int i = 0; i < current.Length; i++)
        {
            var p = current[i].Prefix;
            if (category.Equals(p, StringComparison.OrdinalIgnoreCase)
                || (category.Length > p.Length && category[p.Length] == '.' && category.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return current[i].Level;
        }
        return defaultLevel;
    }

    public static void EnableRollingFile(string directory, long maxBytes = 10 * 1024 * 1024, int retainedFiles = 10, DateTimeOffset? startedAt = null, int? pid = null)
    {
        lock (Gate)
        {
            logDir = Path.GetFullPath(directory);
            maxFileBytes = Math.Max(4096, maxBytes);
            maxRetainedFiles = Math.Max(1, retainedFiles);
            filePart = 0;
            fileBase = $"{(startedAt ?? DateTimeOffset.Now):yyyyMMdd-HHmmss}-p{pid ?? Environment.ProcessId}";
            OpenFileLocked();
            PruneFilesLocked("*.log", maxRetainedFiles, excludeDump: true);
        }
    }

    public static void EnableFlightRecorder(int capacity, string? dumpDirectory = null)
    {
        lock (Gate)
        {
            recorderCapacity = Math.Max(0, capacity);
            if (!string.IsNullOrWhiteSpace(dumpDirectory)) logDir = Path.GetFullPath(dumpDirectory);
            while (Recorder.Count > recorderCapacity) Recorder.Dequeue();
        }
    }

    public static bool IsOutputEnabled(string category, ModLogLevel level) =>
        level != ModLogLevel.Off && (output != null || file != null) && level <= GetLevel(category);

    public static bool IsEnabled(string category, ModLogLevel level) =>
        level != ModLogLevel.Off && (IsOutputEnabled(category, level) || recorderCapacity > 0);

    public static void Write(string category, ModLogLevel level, string message, Exception? ex = null)
    {
        if (level == ModLogLevel.Off) return;
        bool toOutput = IsOutputEnabled(category, level);
        bool toRecorder = recorderCapacity > 0;
        if (!toOutput && !toRecorder) return;

        var now = clock?.Invoke() ?? DateTimeOffset.Now;
        int tid = Environment.CurrentManagedThreadId;
        bool isMain = Volatile.Read(ref mainThreadId) is var main && main != 0 && tid == main;
        string? ctx = contextProvider?.Invoke();
        string body = FormatBody(now, category, tid, isMain, ctx, message ?? "", ex);
        string full = $"{now:yyyy-MM-dd} [{ShortLevel(level)}] {body}";
        string? summaryBody = null, summaryFull = null;
        ModLogLevel summaryLevel = ModLogLevel.Warning;
        bool shouldAutoDump = false;

        lock (Gate)
        {
            if (!AcceptBurstLocked(category, level, message ?? "", ex, now, out var suppressed, out var elapsed, out var prevPreview))
                return;
            if (suppressed > 0)
            {
                summaryLevel = level <= ModLogLevel.Warning ? ModLogLevel.Warning : level;
                summaryBody = FormatBody(now, category, tid, isMain, ctx, $"Suppressed {suppressed} duplicate logs in {elapsed:0.0}s (last: {prevPreview})", null);
                summaryFull = $"{now:yyyy-MM-dd} [{ShortLevel(summaryLevel)}] {summaryBody}";
                EmitLocked(summaryLevel, summaryBody, summaryFull, toOutput, toRecorder);
            }
            EmitLocked(level, body, full, toOutput, toRecorder);
            if (level == ModLogLevel.Fatal && recorderCapacity > 0 && logDir != null)
            {
                long ticks = DateTime.UtcNow.Ticks;
                if (ticks - lastAutoDumpTicks >= TimeSpan.FromSeconds(60).Ticks)
                {
                    lastAutoDumpTicks = ticks;
                    shouldAutoDump = true;
                }
            }
        }

        if (shouldAutoDump)
        {
            var dumped = DumpRecorder($"Fatal in {category}: {message}");
            if (dumped != null) Write("Core", ModLogLevel.Warning, $"Flight recorder dump saved: {dumped}");
        }
    }

    public static int WriteMarker(string? note = null)
    {
        int id = Interlocked.Increment(ref markerId);
        Write("Core", ModLogLevel.Message, string.IsNullOrWhiteSpace(note) ? $"=== MARKER #{id} ===" : $"=== MARKER #{id} note={LogSanitizer.Quote(note.Trim())} ===");
        return id;
    }

    public static string? DumpRecorder(string reason, string? directoryOverride = null)
    {
        string[] lines;
        string? dir;
        lock (Gate)
        {
            if (recorderCapacity <= 0) return null;
            dir = directoryOverride ?? logDir;
            if (string.IsNullOrWhiteSpace(dir)) return null;
            lines = Recorder.ToArray();
        }
        try
        {
            Directory.CreateDirectory(dir);
            var now = clock?.Invoke() ?? DateTimeOffset.Now;
            var path = Path.Combine(dir, $"dump-{now:yyyyMMdd-HHmmss}-p{Environment.ProcessId}.log");
            using var w = new StreamWriter(path, false, new UTF8Encoding(false));
            w.WriteLine($"# MetaMystia Flight Recorder Dump | Time: {now:yyyy-MM-dd HH:mm:ss.fff zzz} | Reason: {reason} | Entries: {lines.Length}/{recorderCapacity}");
            if (dumpSummaryProvider?.Invoke() is { Length: > 0 } summary)
                foreach (var l in summary.Split('\n')) w.WriteLine($"# {l.TrimEnd('\r')}");
            w.WriteLine();
            foreach (var l in lines) w.WriteLine(l);
            lock (Gate) PruneFilesLocked("dump-*.log", 10, excludeDump: false, keepPath: path, dirOverride: dir);
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public static void Flush()
    {
        lock (Gate)
        {
            var now = clock?.Invoke() ?? DateTimeOffset.Now;
            foreach (var b in Bursts.Values)
            {
                if (b.Suppressed <= 0) continue;
                var lvl = b.Level <= ModLogLevel.Warning ? ModLogLevel.Warning : b.Level;
                var body = FormatBody(now, b.Category, Environment.CurrentManagedThreadId, true, null,
                    $"Suppressed {b.Suppressed} duplicate logs in {Math.Max(0.1, (b.Last - b.Start).TotalSeconds):0.0}s (last: {b.Preview})", null);
                EmitLocked(lvl, body, $"{now:yyyy-MM-dd} [{ShortLevel(lvl)}] {body}", output != null || file != null, recorderCapacity > 0);
            }
            Bursts.Clear();
            try { file?.Flush(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { CloseFileLocked(); }
        }
    }

    public static void Shutdown()
    {
        Flush();
        lock (Gate) { CloseFileLocked(); output = null; recorderCapacity = 0; Recorder.Clear(); }
    }

    public static void Reset()
    {
        Shutdown();
        lock (Gate)
        {
            defaultLevel = ModLogLevel.Info;
            rules = [];
            Bursts.Clear();
            contextProvider = null;
            dumpSummaryProvider = null;
            clock = null;
            logDir = null;
            fileBase = filePath = null;
            burstLimit = 3;
            burstWindow = TimeSpan.FromSeconds(10);
            mainThreadId = markerId = 0;
            totalWritten = totalSuppressed = lastAutoDumpTicks = 0;
        }
    }

    private static void EmitLocked(ModLogLevel level, string body, string full, bool toOutput, bool toRecorder)
    {
        if (toRecorder && recorderCapacity > 0)
        {
            Recorder.Enqueue(full);
            while (Recorder.Count > recorderCapacity) Recorder.Dequeue();
        }
        if (!toOutput) return;
        Interlocked.Increment(ref totalWritten);
        output?.Invoke(level, body);
        WriteFileLocked(full, urgent: level <= ModLogLevel.Error);
    }

    private static bool AcceptBurstLocked(string category, ModLogLevel level, string message, Exception? ex, DateTimeOffset now,
        out int suppressed, out double elapsed, out string preview)
    {
        suppressed = 0; elapsed = 0; preview = "";
        if (message.StartsWith("=== MARKER", StringComparison.Ordinal)) return true;
        string sig = ex != null ? $"EX:{ex.GetType().FullName}:{FirstLine(ex.StackTrace ?? ex.Message)}" : message;
        var key = (category, level, sig);
        if (Bursts.TryGetValue(key, out var b))
        {
            if (now - b.Start >= burstWindow)
            {
                suppressed = b.Suppressed;
                elapsed = Math.Max(0.1, (b.Last - b.Start).TotalSeconds);
                preview = b.Preview;
                b.Start = b.Last = now; b.Emitted = 1; b.Suppressed = 0;
                return true;
            }
            b.Last = now;
            if (b.Emitted < burstLimit) { b.Emitted++; return true; }
            b.Suppressed++;
            Interlocked.Increment(ref totalSuppressed);
            return false;
        }
        if (Bursts.Count >= 128) Bursts.Clear();
        Bursts[key] = new Bucket(now)
        {
            Category = category, Level = level,
            Preview = FirstLine(ex != null ? $"{ex.GetType().Name}: {message}" : message, 90),
        };
        return true;
    }

    private static void WriteFileLocked(string line, bool urgent)
    {
        if (file == null) return;
        try
        {
            int bytes = Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
            if (fileBytes > 0 && fileBytes + bytes > maxFileBytes)
            {
                filePart++;
                OpenFileLocked();
                PruneFilesLocked("*.log", maxRetainedFiles, excludeDump: true);
            }
            file?.WriteLine(line);
            fileBytes += bytes;
            if (urgent) file?.Flush();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { CloseFileLocked(); }
    }

    private static void OpenFileLocked()
    {
        CloseFileLocked();
        if (logDir == null || fileBase == null) return;
        try
        {
            Directory.CreateDirectory(logDir);
            filePath = Path.Combine(logDir, filePart == 0 ? $"{fileBase}.log" : $"{fileBase}.{filePart}.log");
            file = new StreamWriter(new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read), new UTF8Encoding(false), 4096) { AutoFlush = false };
            fileBytes = 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { file = null; }
    }

    private static void CloseFileLocked()
    {
        try { file?.Dispose(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        file = null;
    }

    private static void PruneFilesLocked(string pattern, int keep, bool excludeDump, string? keepPath = null, string? dirOverride = null)
    {
        var dir = dirOverride ?? logDir;
        if (dir == null) return;
        try
        {
            var files = new DirectoryInfo(dir).EnumerateFiles(pattern)
                .Where(f => !excludeDump || !f.Name.StartsWith("dump-", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => f.LastWriteTimeUtc).ThenByDescending(f => f.Name, StringComparer.Ordinal).ToList();
            var keepTarget = keepPath ?? filePath;
            for (int i = keep; i < files.Count; i++)
                if (!string.Equals(files[i].FullName, keepTarget, StringComparison.OrdinalIgnoreCase))
                    try { files[i].Delete(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private static string FormatBody(DateTimeOffset now, string category, int tid, bool isMain, string? ctx, string message, Exception? ex)
    {
        var sb = new StringBuilder(96);
        sb.Append(now.ToString("HH:mm:ss.fff")).Append(isMain ? " [M]" : $" [T{tid}]");
        if (!string.IsNullOrEmpty(ctx)) sb.Append(ctx);
        sb.Append('[').Append(category).Append("] ");
        AppendLines(sb, message);
        if (ex != null) { sb.Append(" | "); AppendLines(sb, ex.ToString()); }
        return sb.ToString();
    }

    private static void AppendLines(StringBuilder sb, string text)
    {
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) sb.Append("\n  | ");
            sb.Append(lines[i].TrimEnd('\r'));
        }
    }

    private static string FirstLine(string? text, int max = 120)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var line = text.Split('\n')[0].Trim();
        return line.Length <= max ? line : line[..max] + "...";
    }

    private static string ShortLevel(ModLogLevel level) => level switch
    {
        ModLogLevel.Fatal => "FTL", ModLogLevel.Error => "ERR", ModLogLevel.Warning => "WRN",
        ModLogLevel.Message => "MSG", ModLogLevel.Info => "INF", ModLogLevel.Debug => "DBG", _ => "OFF",
    };
}
