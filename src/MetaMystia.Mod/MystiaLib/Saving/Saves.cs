#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MetaMystia.MystiaLib.Saving;

/// <summary>跟随游戏存档的模组数据。数据写入当前存档的内存数据，落盘由原版存档完成。只在主线程使用。</summary>
public static class Saves
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly HashSet<string> Ids = [];
    private static SaveRecords? _records;

    /// <summary>注册一项数据，读档前后均可。</summary>
    /// <param name="owner">模组标识，通常为插件 GUID。</param>
    /// <param name="name">数据名，同一模组内唯一。与 <paramref name="owner"/> 一起写入存档，不得修改。</param>
    /// <param name="version">数据结构的版本，改名、改含义、删字段时递增。</param>
    /// <param name="migrate">把旧版本的数据改为当前结构；参数为存档中的版本与数据。</param>
    public static SaveEntry<T> Register<T>(string owner, string name, int version = 1, Func<int, JsonObject, JsonObject>? migrate = null)
        where T : class, new()
    {
        if (string.IsNullOrEmpty(owner) || owner.Contains('/'))
            throw new ArgumentException($"Invalid owner '{owner}'", nameof(owner));
        if (string.IsNullOrEmpty(name) || name.Contains('/'))
            throw new ArgumentException($"Invalid name '{name}'", nameof(name));
        if (version < 1)
            throw new ArgumentException($"Invalid version {version}", nameof(version));
        if (JsonSerializer.SerializeToElement(new T(), Options).ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"{typeof(T)} is not serialized as a JSON object", nameof(T));
        string id = $"{owner}/{name}";
        if (!Ids.Add(id))
            throw new ArgumentException($"Duplicate save entry '{id}'", nameof(name));
        return new SaveEntry<T>(id, version, migrate);
    }

    /// <summary>当前存档的记录，换了存档即为新对象；还没读过档时为 null。</summary>
    internal static SaveRecords? Current()
    {
        if (SaveCarrier.TryTake(out var records))
            _records = new SaveRecords(records);
        return _records;
    }

    internal static void Write(string id, string record)
    {
        _records!.Set(id, record);
        SaveCarrier.Write(_records.ToArray());
    }
}
