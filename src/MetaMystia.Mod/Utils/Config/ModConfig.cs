using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

using Mystia;

namespace MetaMystia;

/// <summary>配置项：读取与写入都直接落在模组配置文件上，写入标记为待落盘。</summary>
public sealed class ConfigEntry<T>
{
    private readonly ModConfigFile _file;
    private T _value;

    internal ConfigEntry(ModConfigFile file, string key, T value)
    {
        _file = file;
        Key = key;
        _value = value;
        DefaultValue = value;
    }

    public string Key { get; }

    public T DefaultValue { get; }

    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value))
                return;
            _value = value;
            _file.Set(Key, value);
        }
    }
}

/// <summary>
/// 极简 JSON 配置存储。位置由框架的模组缓存提供；未就绪时退回模组目录，便于早期初始化。
/// </summary>
public sealed class ModConfigFile
{
    private readonly Dictionary<string, object> _values = new(StringComparer.Ordinal);

    private IModCache _cache;
    private string _path;
    private bool _dirty;

    private ModConfigFile(IModCache cache, string path)
    {
        _cache = cache;
        _path = path;
    }

    public static ModConfigFile Open(IModCache cache, string relativePath = "config.json")
    {
        var file = new ModConfigFile(cache, relativePath);
        file.Load();
        return file;
    }

    public static ModConfigFile Open(string absolutePath)
    {
        var file = new ModConfigFile(null, absolutePath);
        file.Load();
        return file;
    }

    public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description = "")
    {
        _ = description;
        var full = section + "." + key;
        var value = defaultValue;
        if (_values.TryGetValue(full, out var raw) && TryConvert(raw, out T parsed))
            value = parsed;
        _values[full] = value;
        return new ConfigEntry<T>(this, full, value);
    }

    public void Set(string key, object value)
    {
        _values[key] = value;
        _dirty = true;
    }

    public void FlushIfDirty()
    {
        if (!_dirty)
            return;
        _dirty = false;

        var node = new JsonObject();
        foreach (var pair in _values)
            node[pair.Key] = pair.Value is null ? null : JsonValue.Create(pair.Value);

        using var writer = OpenWrite();
        using var json = new Utf8JsonWriter(writer, new JsonWriterOptions { Indented = true });
        node.WriteTo(json);
    }

    private void Load()
    {
        using var reader = OpenRead();
        if (reader is null)
            return;

        JsonNode root;
        try
        {
            root = JsonNode.Parse(reader.ReadToEnd());
        }
        catch (JsonException)
        {
            return;
        }

        if (root is not JsonObject obj)
            return;

        foreach (var pair in obj)
        {
            if (pair.Value is null)
                continue;
            _values[pair.Key] = pair.Value.GetValueKind() switch
            {
                JsonValueKind.True or JsonValueKind.False => pair.Value.GetValue<bool>(),
                JsonValueKind.Number => pair.Value.GetValue<double>(),
                JsonValueKind.String => pair.Value.GetValue<string>(),
                _ => pair.Value.ToJsonString(),
            };
        }
    }

    private StreamReader OpenRead()
    {
        if (_cache is not null)
            return _cache.Exists(_path) ? new StreamReader(_cache.OpenRead(_path)) : null;
        return File.Exists(_path) ? new StreamReader(_path) : null;
    }

    private Stream OpenWrite()
    {
        if (_cache is not null)
            return _cache.OpenWrite(_path);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        return File.Create(_path);
    }

    private static bool TryConvert<T>(object raw, out T value)
    {
        value = default;
        var target = typeof(T);

        if (raw is T typed)
        {
            value = typed;
            return true;
        }

        if (target.IsEnum && raw is double number)
        {
            value = (T)Enum.ToObject(target, (int)number);
            return true;
        }

        if (raw is string text && target.IsEnum)
        {
            if (Enum.TryParse(target, text, ignoreCase: true, out var parsedEnum))
            {
                value = (T)parsedEnum;
                return true;
            }
            return false;
        }

        if (raw is IConvertible convertible && typeof(IConvertible).IsAssignableFrom(target))
        {
            value = (T)Convert.ChangeType(convertible, target);
            return true;
        }

        return false;
    }
}
