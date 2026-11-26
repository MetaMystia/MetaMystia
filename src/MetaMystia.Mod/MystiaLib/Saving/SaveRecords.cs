#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MetaMystia.MystiaLib.Saving;

/// <summary>一份存档中的全部记录，每条一个 JSON 字符串。只解析、替换指定 ID 的记录，其余逐字保留。</summary>
internal sealed class SaveRecords(IEnumerable<string> raws)
{
    internal readonly record struct Record(int Version, JsonElement Data);

    private readonly List<(string? Id, string Raw)> _records = [.. raws.Select(raw => (IdOf(raw), raw))];

    public string[] ToArray() => [.. _records.Select(r => r.Raw)];

    /// <summary>没有该记录时返回 true，<paramref name="record"/> 为 null；重复或格式不对时返回 false。</summary>
    public bool TryGet(string id, out Record? record)
    {
        record = null;
        var matches = _records.FindAll(r => r.Id == id);
        if (matches.Count != 1) return matches.Count == 0;
        var root = Parse(matches[0].Raw);
        if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number
            || !version.TryGetInt32(out int number) || number < 1
            || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return false;
        record = new Record(number, data);
        return true;
    }

    public void Set(string id, string raw)
    {
        int index = _records.FindIndex(r => r.Id == id);
        if (index < 0)
            _records.Add((id, raw));
        else
            _records[index] = (id, raw);
    }

    private static string? IdOf(string raw)
    {
        var root = Parse(raw);
        return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString()
            : null;
    }

    // 存档内容可能来自其他版本或被手工修改。
    private static JsonElement Parse(string? raw)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(raw ?? "");
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
