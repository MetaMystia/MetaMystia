using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Newtonsoft.Json;

using GameData.RunTime.Common;
using GameData.Utils;

using MetaMystia.MetaLib.Storage;

using JsonSerializer = System.Text.Json.JsonSerializer;

namespace MetaMystia.MetaLib;

[AutoLog]
public static partial class LabChecks
{
    public static string Status { get; private set; } = "写入和删除只改变当前内存；使用游戏原有操作保存。";
    private static int? callbackCounter;

    internal static void RegisterCallbacks()
    {
        if (!ModSaveData.TryRegister(SampleData.ModuleCallback, 1, LoadCallbackSample,
            () => callbackCounter.HasValue ? JsonSerializer.SerializeToElement(callbackCounter.Value) : null, out var error))
            Report(false, error);
    }

    private static bool LoadCallbackSample(ModuleData? data)
    {
        callbackCounter = null;
        if (data == null)
            return true;
        if (data.Data.ValueKind != JsonValueKind.Number || !data.Data.TryGetInt32(out var counter))
            return false;
        callbackCounter = counter;
        Log.Info($"加载回调样例：counter={counter}");
        return true;
    }

    public static void Run(int key)
    {
        switch (key)
        {
            case 1: Describe(); break;
            case 2: WriteSample(); break;
            case 3: VerifySample(); break;
            case 4: UpdateSample(); break;
            case 5: WriteSecondModule(); break;
            case 6: VerifyIsolation(); break;
            case 7: WriteLarge(); break;
            case 8: RemoveSamples(); break;
            case 9: CheckRoundTrip(); break;
        }
    }

    private static void Describe()
    {
        if (!ModSaveData.TryGetModuleIds(out var modules, out var error) || !ModSaveData.TryGetPayload(out var payload, out error))
        {
            Report(false, error);
            return;
        }
        Report(true, $"模块 [{string.Join(", ", modules)}]；{payload.Length} 条记录；UTF-8 {payload.Sum(record => Encoding.UTF8.GetByteCount(record ?? ""))} 字节；SHA256 {Hash(JsonSerializer.Serialize(payload))}");
    }

    private static void WriteSample()
    {
        if (!ModSaveData.TryWrite(SampleData.ModuleA, 1, SampleData.Create(0), out var error))
        {
            Report(false, error);
            return;
        }
        callbackCounter = 0;
        Report(true, "A 已写入，counter=0；回调样例计数归零，等待游戏保存时收集。");
    }

    private static bool ReadSample(out ModuleData value, out int counter)
    {
        value = null!;
        counter = 0;
        if (!ModSaveData.TryRead(SampleData.ModuleA, out var entry, out var error))
        {
            Report(false, error.Length == 0 ? "没有样例 A，请先按 F2。" : error);
            return false;
        }
        if (entry!.Version != 1 || entry.Data.ValueKind != JsonValueKind.Object ||
            !entry.Data.TryGetProperty("counter", out var number) || number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out counter) ||
            entry.Data.GetRawText() != SampleData.Create(counter).GetRawText())
        {
            Report(false, "A 的版本或内容与样例不一致。");
            return false;
        }
        value = entry;
        return true;
    }

    private static void VerifySample()
    {
        if (ReadSample(out var value, out var counter))
            Report(true, $"A 校验通过，counter={counter}；回调内存计数={callbackCounter?.ToString() ?? "无"}；SHA256 {Hash(value.Data.GetRawText())}");
    }

    private static void UpdateSample()
    {
        if (!ReadSample(out _, out var counter))
            return;
        if (counter == int.MaxValue)
        {
            Report(false, "计数已达上限。");
            return;
        }
        var snapshot = RunTimePlayerData.NotLoadedDLCSchedulerSaveData;
        ModSaveData.TryGetPayload(snapshot, out var before, out _);
        if (!ModSaveData.TryWrite(SampleData.ModuleA, 1, SampleData.Create(counter + 1), out var error))
        {
            Report(false, error);
            return;
        }
        var intact = ModSaveData.TryGetPayload(snapshot, out var after, out error) && before.SequenceEqual(after);
        callbackCounter = counter + 1;
        Report(intact, intact ? $"A counter={counter + 1}，旧快照保持不变；等待游戏保存。" : "旧快照发生变化。");
    }

    private static void WriteSecondModule()
    {
        if (!ReadSample(out var first, out _))
            return;
        var data = JsonSerializer.SerializeToElement(new { text = "第二模块独立数据", aHash = Hash(first.Data.GetRawText()) });
        Report(ModSaveData.TryWrite(SampleData.ModuleB, 1, data, out var error),
            error.Length == 0 ? "B 已写入，同时记录了 A 的摘要；F6 检查隔离。" : error);
    }

    private static void VerifyIsolation()
    {
        if (!ReadSample(out var first, out _))
            return;
        if (!ModSaveData.TryRead(SampleData.ModuleB, out var second, out var error))
        {
            Report(false, error.Length == 0 ? "没有样例 B，请先按 F5。" : error);
            return;
        }
        var expected = JsonSerializer.SerializeToElement(new { text = "第二模块独立数据", aHash = Hash(first.Data.GetRawText()) });
        var valid = second!.Version == 1 && second.Data.GetRawText() == expected.GetRawText();
        Report(valid, valid ? "A/B 隔离检查通过，A 与写入 B 时一致。" : "B 内容不符，或写入 B 后 A 已改变。");
    }

    private static void WriteLarge()
    {
        var value = JsonSerializer.SerializeToElement(new string('x', 65536));
        if (!ModSaveData.TryWrite(SampleData.ModuleLarge, 1, value, out var error))
        {
            Report(false, error);
            return;
        }
        var valid = ModSaveData.TryRead(SampleData.ModuleLarge, out var read, out error) && read!.Data.GetString() == value.GetString();
        Report(valid, valid ? "64 KiB 样例写入并回读通过；等待游戏保存。" : "64 KiB 样例回读失败。");
    }

    private static void RemoveSamples()
    {
        callbackCounter = null;
        foreach (var id in SampleData.ModuleIds)
        {
            if (ModSaveData.TryRemove(id, 1, out var error))
                continue;
            Report(false, error);
            return;
        }
        Report(true, "四个 MetaLib 样例模块已删除，其他模块保留；等待游戏保存。");
    }

    private static void CheckRoundTrip()
    {
        var failures = CodecChecks.Run();
        if (failures.Count != 0)
        {
            Report(false, string.Join("；", failures));
            return;
        }
        if (!ModSaveData.TryPrepareSave(out var preparationError))
        {
            Report(false, preparationError);
            return;
        }
        if (!ModSaveData.TryGetPayload(out var before, out var error) || before.Length == 0)
        {
            Report(false, $"托管格式检查通过；游戏往返需要已加载存档及样例。{error}");
            return;
        }
        var save = SaveManagement.GenerateCurrentPlayerSaveData();
        var json = SaveManagement.GenerateSaveString(save, Formatting.Indented);
        var loaded = SaveManagement.GenerateSaveData("MetaLib-memory", json, false);
        if (loaded.loadError || !loaded.fileData.HasValue)
        {
            Report(false, "游戏序列化往返失败。");
            return;
        }
        var valid = ModSaveData.TryGetPayload(loaded.fileData.Value.schedulerPartialDLC, out var after, out error) && before.SequenceEqual(after);
        Report(valid, valid ? $"托管格式及游戏内存序列化往返通过；{after.Length} 条记录；SHA256 {Hash(JsonSerializer.Serialize(after))}。未写磁盘、未加载场景。" : $"游戏往返内容不一致。{error}");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void Report(bool success, string message)
    {
        Status = message;
        if (success)
            Log.Info(message);
        else
            Log.Warning(message);
    }
}
