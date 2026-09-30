using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

using MetaMystia.MetaLib.Storage;

namespace MetaMystia.MetaLib;

internal static class CodecChecks
{
    internal static IReadOnlyList<string> Run()
    {
        var failures = new List<string>();
        void Check(bool condition, string name)
        {
            if (!condition)
                failures.Add(name);
        }

        var document = new SaveDocument(Array.Empty<string>());
        Check(document.TryWrite("a", 1, SampleData.Create(0), out _), "写入复杂数据");
        var original = document.ToArray();
        var reopened = new SaveDocument(original);
        Check(reopened.TryRead("a", out var read, out _) && read!.Version == 1 &&
            read.Data.GetRawText() == SampleData.Create(0).GetRawText(), "中文、转义、数值精度和嵌套对象往返");
        using (var json = JsonDocument.Parse(original[0]))
            Check(json.RootElement.EnumerateObject().Count() == 3 && json.RootElement.GetProperty("module").GetString() == "a", "每条记录仅含 module/version/data");

        Check(document.TryWrite("b", 7, JsonSerializer.SerializeToElement(new[] { "其他模块", "保留" }), out _), "第二模块写入");
        var secondRecord = document.ToArray()[1];
        Check(document.TryWrite("a", 1, SampleData.Create(1), out _) && document.ToArray()[1] == secondRecord, "修改 A 时 B 原字符串不变");
        Check(document.ToArray().Length == 2, "每个模块独占一个数组元素");
        var beforeRejected = document.ToArray();
        Check(!document.TryWrite("b", 1, SampleData.Create(0), out _) && document.ToArray().SequenceEqual(beforeRejected), "拒绝覆盖更高模块版本");
        Check(!document.TryRemove("b", 1, out _) && document.ToArray().SequenceEqual(beforeRejected), "拒绝删除更高模块版本");
        Check(document.TryRemove("a", 1, out _) && !document.TryRead("a", out _, out _) && document.ToArray().Single() == secondRecord, "只删除指定模块");
        Check(original[0].Contains("\\u") && original.SequenceEqual(reopened.ToArray()), "修改后旧快照仍保持不变");

        string[] foreign = { "not json", "{", "null", "[]", "{\"future\":true}", "{ \"module\": \"foreign\", \"version\":9, \"data\":1e3, \"extra\":true }" };
        var mixed = new SaveDocument(foreign);
        Check(mixed.TryWrite("a", 1, SampleData.Create(0), out _) && mixed.ToArray().Take(foreign.Length).SequenceEqual(foreign), "未识别记录及其他模块逐字保留");
        string[] invalid =
        {
            "{\"module\":\"a\"}",
            "{\"module\":\"a\",\"version\":0,\"data\":null}",
            "{\"module\":\"a\",\"version\":1,\"version\":1,\"data\":null}",
            "{\"module\":\"a\",\"module\":\"b\",\"version\":1,\"data\":null}"
        };
        foreach (var record in invalid)
        {
            var invalidDocument = new SaveDocument(new[] { record });
            Check(!invalidDocument.TryRead("a", out _, out var error) && error.Length != 0 &&
                !invalidDocument.TryWrite("a", 1, SampleData.Create(0), out _) && invalidDocument.ToArray()[0] == record, "拒绝覆盖目标模块的异常记录");
        }
        var duplicate = new SaveDocument(new[] { original[0], original[0] });
        Check(!duplicate.TryRead("a", out _, out _) && !duplicate.TryWrite("a", 1, SampleData.Create(0), out _) &&
            !duplicate.TryRemove("a", 1, out _) && duplicate.ToArray().Length == 2, "拒绝重复模块名");
        Check(!document.TryRead("missing", out _, out var missingError) && missingError.Length == 0, "缺失模块与读取错误可区分");
        Check(!document.TryWrite(" ", 1, SampleData.Create(0), out _) && !document.TryWrite("c", 0, SampleData.Create(0), out _) &&
            !document.TryWrite("c", 1, default, out _), "拒绝无效参数");
        Check(document.TryWrite("null", 1, JsonSerializer.SerializeToElement<object?>(null), out _) &&
            document.TryRead("null", out var nullValue, out _) && nullValue!.Data.ValueKind == JsonValueKind.Null, "JSON null 往返");
        var large = JsonSerializer.SerializeToElement(new string('x', 65536));
        Check(document.TryWrite("large", 1, large, out _) && new SaveDocument(document.ToArray()).TryRead("large", out var largeRead, out _) &&
            largeRead!.Data.GetString()!.Length == 65536, "64 KiB 字符串往返");

        // 与 Release4.4.0e SaveFilePreProcessRegex 相同，只校验字符串转义边界。
        var regex = new Regex(",[\\n\\s]+\"magnitude\":\\s+[\\d.]+,[\\n\\s]+\"sqrMagnitude\":\\s+[\\d]+");
        var current = mixed.ToArray();
        var beforeRoundTrip = current;
        for (var i = 0; i < 3; i++)
        {
            var outer = JsonSerializer.Serialize(new { finishedEvents = current });
            using var parsed = JsonDocument.Parse(regex.Replace(outer, ""));
            current = parsed.RootElement.GetProperty("finishedEvents").EnumerateArray().Select(entry => entry.GetString()!).ToArray();
        }
        Check(current.SequenceEqual(beforeRoundTrip), "多字符串外层 JSON 与预处理三次往返");

        var handlers = new ModuleHandlers();
        int? state = null;
        var loads = 0;
        Check(handlers.TryRegister("callback", 2, data =>
        {
            loads++;
            state = data?.Data.GetInt32() ?? 0;
            return true;
        }, () => state.HasValue ? JsonSerializer.SerializeToElement(state.Value) : null, out _), "注册回调");
        Check(!handlers.TryRegister("callback", 2, _ => true, () => null, out _), "拒绝重复注册");
        Check(handlers.TryCapture(document, out _) && loads == 0 && !document.TryRead("callback", out _, out _), "读档前不调用保存回调");
        Check(handlers.Load(document).Count == 0 && loads == 1 && state == 0, "无记录时回调收到空值");
        state = 12;
        Check(handlers.TryCapture(document, out _) && document.TryRead("callback", out var captured, out _) &&
            captured!.Version == 2 && captured.Data.GetInt32() == 12, "保存回调提供版本和完整数据");
        state = null;
        var beforeSkip = document.ToArray();
        Check(handlers.TryCapture(document, out _) && document.ToArray().SequenceEqual(beforeSkip), "回调返回空值保留原记录");
        Check(handlers.Load(new SaveDocument(document.ToArray())).Count == 0 && state == 12, "重新读档恢复模块状态");
        handlers.Reset();
        state = 99;
        Check(handlers.TryCapture(document, out _) && document.ToArray().SequenceEqual(beforeSkip), "结束会话后不采集旧状态");
        Check(handlers.Load(new SaveDocument(Array.Empty<string>())).Count == 0 && state == 0, "切换空存档重置模块状态");

        var future = new SaveDocument(new[] { "{\"module\":\"callback\",\"version\":3,\"data\":42}" });
        var priorLoads = loads;
        Check(handlers.Load(future).Count == 1 && loads == priorLoads + 1 && state == 0 && handlers.TryCapture(future, out _) &&
            future.TryRead("callback", out var newer, out _) && newer!.Version == 3, "高版本记录清空旧状态但不覆盖原文");
        var rejected = new ModuleHandlers();
        var saveCalls = 0;
        rejected.TryRegister("rejected", 1, _ => false, () => { saveCalls++; return null; }, out _);
        Check(rejected.Load(document).Count == 1 && rejected.TryCapture(document, out _) && saveCalls == 0, "拒绝加载后不调用保存回调");
        var broken = new ModuleHandlers();
        broken.TryRegister("first", 1, _ => true, () => JsonSerializer.SerializeToElement(5), out _);
        broken.TryRegister("broken", 1, _ => true, () => default(JsonElement), out _);
        broken.Load(document);
        Check(!broken.TryCapture(new SaveDocument(beforeSkip), out _) && beforeSkip.SequenceEqual(document.ToArray()), "无效回调结果拒绝提交");
        var writeNull = new ModuleHandlers();
        writeNull.TryRegister("callback.null", 1, _ => true, () => JsonSerializer.SerializeToElement<object?>(null), out _);
        writeNull.Load(document);
        Check(writeNull.TryCapture(document, out _) && document.TryRead("callback.null", out var callbackNull, out _) &&
            callbackNull!.Data.ValueKind == JsonValueKind.Null, "回调能明确写入 JSON null");
        return failures;
    }
}
