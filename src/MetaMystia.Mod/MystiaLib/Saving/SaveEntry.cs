#nullable enable

using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MetaMystia.MystiaLib.Saving;

/// <summary>一项跟随存档的数据，由 <see cref="Saves.Register{T}"/> 创建。</summary>
public sealed class SaveEntry<T> where T : class, new()
{
    private sealed record Envelope(string Id, int Version, T Data);

    private readonly string _id;
    private readonly int _version;
    private readonly Func<int, JsonObject, JsonObject>? _migrate;

    private SaveRecords? _source;
    private T? _value;
    private bool _writable;

    internal SaveEntry(string id, int version, Func<int, JsonObject, JsonObject>? migrate)
    {
        _id = id;
        _version = version;
        _migrate = migrate;
    }

    /// <summary>当前存档的数据，没有记录时为默认值。读档后换成新对象，不要留存；修改须经 <see cref="Update"/>。</summary>
    public T Value
    {
        get
        {
            var records = Saves.Current();
            if (_value == null || records != _source)
            {
                (_value, _writable) = records == null ? (new T(), false) : Read(records);
                _source = records;
            }
            return _value;
        }
    }

    /// <summary>修改数据并写入当前存档的内存数据，返回本次修改是否写入。还没读过档、记录损坏或版本更高时只修改内存。</summary>
    public bool Update(Action<T> change)
    {
        var value = Value;
        change(value);
        if (!_writable) return false;
        Saves.Write(_id, JsonSerializer.Serialize(new Envelope(_id, _version, value), Saves.Options));
        return true;
    }

    private (T Value, bool Writable) Read(SaveRecords records)
    {
        if (!records.TryGet(_id, out var found)) return Unusable("duplicate or malformed");
        if (found is not { } record) return (new T(), true);
        if (record.Version > _version) return Unusable($"version {record.Version} is newer than {_version}");
        try
        {
            var value = record.Version < _version && _migrate != null
                ? _migrate(record.Version, JsonObject.Create(record.Data)!).Deserialize<T>(Saves.Options)
                : record.Data.Deserialize<T>(Saves.Options);
            return (value!, true);
        }
        catch (JsonException e)
        {
            return Unusable(e.Message);
        }
    }

    // 原记录留在存档中，不能被默认值覆盖。
    private (T Value, bool Writable) Unusable(string reason)
    {
        Bootstrap.Log.LogWarning($"Save record '{_id}' is kept but not used: {reason}");
        return (new T(), false);
    }
}
