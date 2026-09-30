using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MetaMystia.MetaLib.Storage;

internal sealed class SaveDocument
{
    private readonly List<string> records;

    internal SaveDocument(string[] records) => this.records = new(records);
    internal string[] ToArray() => records.ToArray();

    internal string[] ModuleIds => records.Select(Parse)
        .Where(entry => entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("module", out var id) && id.ValueKind == JsonValueKind.String)
        .Select(entry => entry.GetProperty("module").GetString()!).ToArray();

    internal bool TryRead(string moduleId, out ModuleData? value, out string error)
    {
        value = null;
        if (!TryFind(moduleId, out var index, out error) || index < 0)
            return false;
        var entry = Parse(records[index]);
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (entry.EnumerateObject().Any(property => !names.Add(property.Name)) ||
            !entry.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number < 1 ||
            !entry.TryGetProperty("data", out var data))
        {
            error = $"模块 {moduleId} 的记录格式无效，原数据已保留。";
            return false;
        }
        value = new(number, data.Clone());
        return true;
    }

    internal bool TryWrite(string moduleId, int version, JsonElement data, out string error)
    {
        if (!CanEdit(moduleId, version, out var index, out error))
            return false;
        if (data.ValueKind == JsonValueKind.Undefined)
        {
            error = "数据必须是有效 JSON 值。";
            return false;
        }
        var record = JsonSerializer.Serialize(new { module = moduleId, version, data });
        if (index < 0)
            records.Add(record);
        else
            records[index] = record;
        return true;
    }

    internal bool TryRemove(string moduleId, int supportedVersion, out string error)
    {
        if (!CanEdit(moduleId, supportedVersion, out var index, out error))
            return false;
        if (index >= 0)
            records.RemoveAt(index);
        return true;
    }

    private bool CanEdit(string moduleId, int supportedVersion, out int index, out string error)
    {
        if (!TryFind(moduleId, out index, out error))
            return false;
        if (supportedVersion < 1)
        {
            error = "数据版本必须大于零。";
            return false;
        }
        if (index < 0)
            return true;
        if (!TryRead(moduleId, out var value, out error))
            return false;
        if (value!.Version <= supportedVersion)
            return true;
        error = $"模块 {moduleId} 的版本 {value.Version} 高于支持版本 {supportedVersion}，原数据已保留。";
        return false;
    }

    private bool TryFind(string moduleId, out int index, out string error)
    {
        index = -1;
        error = "";
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            error = "模块 ID 不能为空。";
            return false;
        }
        for (var i = 0; i < records.Count; i++)
        {
            var entry = Parse(records[i]);
            if (entry.ValueKind != JsonValueKind.Object || !entry.EnumerateObject().Any(property =>
                property.NameEquals("module") && property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == moduleId))
                continue;
            if (index >= 0)
            {
                error = $"模块 {moduleId} 有重复记录，已拒绝操作。";
                return false;
            }
            index = i;
        }
        return true;
    }

    private static JsonElement Parse(string? record)
    {
        if (record == null)
            return default;
        // 存档字符串是外部输入；不认识的记录保留原文。
        try
        {
            using var json = JsonDocument.Parse(record);
            return json.RootElement.Clone();
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
