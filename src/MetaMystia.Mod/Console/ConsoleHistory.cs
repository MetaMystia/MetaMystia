#nullable enable
using System.Collections.Generic;

namespace MetaMystia.ConsoleSystem;

/// <summary>
/// 输入历史。浏览时保留未发送的草稿，回到末尾时恢复。
/// </summary>
public sealed class ConsoleHistory(int capacity)
{
    private readonly List<string> _items = [];
    private int _cursor = -1;
    private string _draft = "";

    public IReadOnlyList<string> Items => _items;
    public bool IsBrowsing => _cursor != -1;

    public void Add(string line)
    {
        _cursor = -1;
        if (_items.Count > 0 && _items[^1] == line) return;
        _items.Add(line);
        if (_items.Count > capacity) _items.RemoveRange(0, _items.Count - capacity);
    }

    public string? Previous(string current)
    {
        if (_cursor + 1 >= _items.Count) return null;
        if (_cursor == -1) _draft = current;
        _cursor++;
        return _items[^(_cursor + 1)];
    }

    public string? Next()
    {
        if (_cursor == -1) return null;
        _cursor--;
        return _cursor == -1 ? _draft : _items[^(_cursor + 1)];
    }

    public void ResetCursor() => _cursor = -1;
}
