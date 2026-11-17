#nullable enable
using System;
using System.Collections.Generic;

namespace MetaMystia.ConsoleSystem;

/// <summary>
/// 输入时自动给出候选。↑↓ 选择，Tab 填入并在多个候选间轮换，唯一候选填入后补空格。
/// </summary>
public sealed class CompletionEngine
{
    public const int MaxVisible = 8;

    private readonly Func<string, CompletionResult?> _complete;
    private string _text = "";
    private int _start;
    private bool _dismissed;
    private string? _applied;
    private int _appliedIndex;

    public IReadOnlyList<Suggestion> Items { get; private set; } = [];
    public string? Usage { get; private set; }
    public int Selected { get; private set; }
    public int Offset { get; private set; }
    public int Start => _start;
    public string Partial => _text[_start..];
    public bool HasItems => !_dismissed && Items.Count > 0;
    public bool IsVisible => !_dismissed && (Items.Count > 0 || Usage != null);

    /// <summary>已按 Tab 或在列表中选择过，此时 ↑↓ 与 Esc 作用于列表。</summary>
    public bool IsEngaged { get; private set; }

    /// <summary>用方向键选中了尚未填入的候选。</summary>
    public bool HasPendingSelection => HasItems && IsEngaged && (_applied == null || Selected != _appliedIndex);

    public CompletionEngine(Func<string, CompletionResult?> complete) => _complete = complete;

    /// <summary>输入变化时调用；由本类填入的文本不会重置轮换状态。</summary>
    public void Refresh(string text)
    {
        if (text == _applied) return;
        _applied = null;
        _dismissed = false;
        IsEngaged = false;
        _text = text;
        var result = _complete(text);
        Items = result?.Items ?? [];
        Usage = result?.Usage;
        _start = result?.Start ?? text.Length;
        Selected = 0;
        Offset = 0;
    }

    public void Move(int delta)
    {
        if (!HasItems) return;
        IsEngaged = true;
        Selected = (Selected + delta + Items.Count) % Items.Count;
        Offset = Math.Clamp(Offset, Selected - MaxVisible + 1, Selected);
    }

    /// <summary>滚动可见范围，不改变选中项。</summary>
    public void Scroll(int delta)
    {
        if (HasItems) Offset = Math.Clamp(Offset + delta, 0, Math.Max(0, Items.Count - MaxVisible));
    }

    /// <summary>Tab：填入选中项；继续按 Tab 时轮换到下一项。返回新的输入文本。</summary>
    public string? Cycle(bool reverse)
    {
        if (!HasItems) return null;
        if (_applied != null && Selected == _appliedIndex) Move(reverse ? -1 : 1);
        IsEngaged = true;
        if (Items.Count == 1) return Accept(Selected);
        _appliedIndex = Selected;
        return _applied = _text[.._start] + Items[Selected].Label;
    }

    /// <summary>确认第 index 项并补空格，随后给出下一级候选。</summary>
    public string Accept(int index)
    {
        string text = _text[.._start] + Items[index].Label + " ";
        Refresh(text);
        return text;
    }

    public void Dismiss()
    {
        _dismissed = true;
        IsEngaged = false;
    }

    public void Reset()
    {
        _applied = null;
        _text = "";
        Items = [];
        Usage = null;
        _dismissed = false;
        IsEngaged = false;
    }
}
