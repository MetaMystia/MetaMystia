#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;

using BepInEx;
using UnityEngine.InputSystem;

namespace MetaMystia.MystiaLib.Keybinding;

/// <summary>
/// 模组按键的绑定覆盖存在插件自己的文件里。Input System 1.3.0 读取覆盖时遇到不存在的绑定 ID 会抛 <c>NotImplementedException</c>：
/// 原版 <c>Mystia.binding</c> 含模组条目、启动时模组映射不存在，游戏就停在加载画面。所以原版文件只保留原版条目。
/// </summary>
internal static class KeybindStore
{
    // 字段与 Input System 的覆盖 JSON 相同。
    private sealed record Entry(string Action, string Id, string Path, string Interactions, string Processors);

    private sealed record Document(List<Entry> Bindings);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string FilePath => Path.Combine(Paths.ConfigPath, Bootstrap.Id + ".bindings.json");

    /// <summary>
    /// 拆出原版保存数据中的模组条目写入插件文件，返回只含原版条目的数据；没有模组条目时返回 null，不改动原数据。
    /// 本次未加入的模组条目原样保留在插件文件中。
    /// </summary>
    public static string? Split(string data)
    {
        if (!TryParse(data, out var all)) return null;
        var mod = all.Where(e => e.Action.StartsWith(Keybinds.MapPrefix)).ToList();
        var added = Keybinds.AddedMaps.Select(m => m.name).ToHashSet();
        var kept = Read().Where(e => !added.Contains(MapName(e)));
        Write(kept.Concat(mod).ToList());
        if (mod.Count == 0) return null;
        var vanilla = all.Except(mod).ToList();
        return vanilla.Count == 0 ? "" : Serialize(vanilla);
    }

    /// <summary>把插件文件中的覆盖按绑定 ID 应用到给定映射，跳过不存在的 ID。</summary>
    public static void Apply(IEnumerable<InputActionMap> maps)
    {
        var byName = maps.ToDictionary(m => m.name);
        if (byName.Count == 0) return;
        foreach (var entry in Read())
        {
            if (!byName.TryGetValue(MapName(entry), out var map)) continue;
            var action = map.FindAction(entry.Action[(entry.Action.IndexOf('/') + 1)..], false);
            if (action == null) continue;
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                if (!string.Equals(bindings[i].id.ToString(), entry.Id, StringComparison.OrdinalIgnoreCase)) continue;
                InputActionRebindingExtensions.ApplyBindingOverride(action, i, entry.Path);
                break;
            }
        }
    }

    private static string MapName(Entry entry) => entry.Action[..Math.Max(entry.Action.IndexOf('/'), 0)];

    private static List<Entry> Read()
    {
        string text;
        try
        {
            if (!File.Exists(FilePath)) return [];
            text = File.ReadAllText(FilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Bootstrap.Log.LogError($"Failed to read {FilePath}: {e.Message}");
            return [];
        }
        return TryParse(text, out var entries) ? entries : [];
    }

    private static void Write(List<Entry> entries)
    {
        var text = Serialize(entries);
        try
        {
            if (File.Exists(FilePath) ? File.ReadAllText(FilePath) == text : entries.Count == 0) return;
            File.WriteAllText(FilePath, text);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Bootstrap.Log.LogError($"Failed to write {FilePath}: {e.Message}");
        }
    }

    // 原版没有覆盖时保存空字符串。
    private static bool TryParse(string text, out List<Entry> entries)
    {
        entries = [];
        if (string.IsNullOrEmpty(text)) return true;
        try
        {
            entries = JsonSerializer.Deserialize<Document>(text, Options)?.Bindings ?? [];
            return true;
        }
        catch (JsonException e)
        {
            Bootstrap.Log.LogError($"Invalid binding overrides: {e.Message}");
            return false;
        }
    }

    private static string Serialize(List<Entry> entries) => JsonSerializer.Serialize(new Document(entries), Options);
}
