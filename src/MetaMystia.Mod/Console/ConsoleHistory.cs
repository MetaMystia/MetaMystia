#nullable enable
using System.Collections.Generic;

namespace MetaMystia.ConsoleSystem;

/// <summary>输入历史。翻阅时保留正在编辑的草稿，回到底部时还原。</summary>
public sealed class ConsoleHistory
{
    private readonly List<string> _items = [];
    private int _cursor = -1;
    private string _draft = "";

    public int Capacity { get; set; } = 200;
    public IReadOnlyList<string> Items => _items;
    public bool IsBrowsing => _cursor >= 0;

    public void Load(IEnumerable<string> entries)
    {
        _items.Clear();
        foreach (var e in entries)
            if (!string.IsNullOrWhiteSpace(e)) Append(e);
        Trim();
        Reset();
    }

    public void Add(string entry)
    {
        if (!string.IsNullOrWhiteSpace(entry)) Append(entry);
        Trim();
        Reset();
    }

    /// <summary>上翻。返回 null 表示没有更早的记录。</summary>
    public string? Older(string current)
    {
        if (_items.Count == 0) return null;
        if (_cursor < 0)
        {
            _draft = current;
            _cursor = _items.Count - 1;
        }
        else if (_cursor > 0)
            _cursor--;
        else
            return null;
        return _items[_cursor];
    }

    /// <summary>下翻。越过最新一条时返回草稿；不在翻阅中返回 null。</summary>
    public string? Newer()
    {
        if (_cursor < 0) return null;
        if (_cursor < _items.Count - 1) return _items[++_cursor];
        _cursor = -1;
        return _draft;
    }

    public void Reset()
    {
        _cursor = -1;
        _draft = "";
    }

    private void Append(string entry)
    {
        if (_items.Count == 0 || _items[^1] != entry) _items.Add(entry);
    }

    private void Trim()
    {
        if (_items.Count > Capacity) _items.RemoveRange(0, _items.Count - Capacity);
    }
}
