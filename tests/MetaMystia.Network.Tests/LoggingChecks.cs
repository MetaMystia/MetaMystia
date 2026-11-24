using MetaMystia.Logging;

static partial class Checks
{
    static void LoggingChecks()
    {
        try
        {
            // 1. 分层前缀过滤、主线程/角色标记与编译期 CallerMemberName
            ModLog.Reset();
            var lines = new List<(ModLogLevel Level, string Text)>();
            ModLog.SetOutput((lvl, text) => lines.Add((lvl, text)));
            ModLog.SetMainThread(Environment.CurrentManagedThreadId);
            ModLog.SetContextProvider(() => "[H]");
            ModLog.ConfigureLevels(ModLogLevel.Info, "ResourceEx=Warning;ResourceEx.Dialog=Debug");

            Assert(!ModLog.Core.IsEnabled(ModLogLevel.Debug) && !ModLog.For("ResourceEx.Guest").IsEnabled(ModLogLevel.Info),
                "默认级别与父前缀规则正确拦截低级日志");
            ModLog.For("ResourceEx.Dialog").Debug("dialog dbg");
            ModLog.For("ResourceEx.Guest").Warning("guest warn");
            ModLog.Core.InfoCaller("caller ok");
            Assert(lines.Count == 3 && lines[0].Text.Contains("[M][H][ResourceEx.Dialog] dialog dbg")
                && lines[2].Text.Contains($"[{nameof(LoggingChecks)}] caller ok"),
                "最长前缀优先生效，附带线程/角色标记与 CallerMemberName");

            // 2. 多行与异常统一缩进
            lines.Clear();
            ModLog.Net.Error(new InvalidOperationException("boom"), "line1\nline2");
            Assert(lines[0].Text.Contains("line1\n  | line2 | System.InvalidOperationException: boom"), "多行消息与异常统一缩进");

            // 3. 重复风暴折叠
            ModLog.Reset();
            lines.Clear();
            var fakeNow = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.FromHours(8));
            ModLog.SetClock(() => fakeNow);
            ModLog.SetBurstPolicy(3, TimeSpan.FromSeconds(10));
            ModLog.SetOutput((lvl, text) => lines.Add((lvl, text)));
            for (int i = 0; i < 10; i++)
            {
                fakeNow = fakeNow.AddMilliseconds(100);
                ModLog.Net.Error(new NullReferenceException("nre"), "Network callback");
            }
            Assert(lines.Count == 3 && ModLog.TotalSuppressed == 7, "重复异常窗口内放行前 3 条并折叠后续");
            fakeNow = fakeNow.AddSeconds(11);
            ModLog.Net.Error(new NullReferenceException("nre"), "Network callback");
            Assert(lines.Count == 5 && lines[3].Text.Contains("Suppressed 7 duplicate logs"), "跨窗后补发抑制汇总");

            // 4. 内存黑匣子与独立滚动文件
            ModLog.Reset();
            lines.Clear();
            var tempDir = Path.GetFullPath(Path.Combine(".tmp", "logging-tests", Guid.NewGuid().ToString("N")));
            ModLog.SetOutput((lvl, text) => lines.Add((lvl, text)));
            ModLog.ConfigureLevels(ModLogLevel.Info);
            ModLog.EnableFlightRecorder(5, tempDir);
            for (int i = 0; i < 7; i++) ModLog.Core.Debug($"dbg-{i}");
            ModLog.Core.Info("info-final");
            var dumpPath = ModLog.DumpRecorder("unit-test");
            Assert(lines.Count == 1 && dumpPath != null && File.Exists(dumpPath), "黑匣子捕获 Debug 并可导出快照");
            var dumpText = File.ReadAllText(dumpPath!);
            Assert(!dumpText.Contains("dbg-0") && dumpText.Contains("dbg-3") && dumpText.Contains("info-final"), "黑匣子环形顺序正确");

            var rollDir = Path.Combine(tempDir, "rolling");
            ModLog.EnableRollingFile(rollDir, maxBytes: 4096, retainedFiles: 2,
                startedAt: new DateTimeOffset(2026, 11, 24, 12, 30, 0, TimeSpan.FromHours(8)), pid: 4242);
            var payload = new string('x', 900);
            for (int i = 0; i < 20; i++) ModLog.Core.Info($"entry-{i} {payload}");
            ModLog.Shutdown();
            var rolled = Directory.GetFiles(rollDir, "*.log");
            Assert(rolled.Length <= 2 && rolled.Any(f => Path.GetFileName(f).Contains("-p4242.")), "滚动日志文件按大小切分、带 PID 并清理旧文件");

            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
        finally
        {
            ModLog.Reset();
        }
    }
}
