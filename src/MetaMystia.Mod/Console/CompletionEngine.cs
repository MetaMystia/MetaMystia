#nullable enable
using System;
using System.Collections.Generic;

namespace MetaMystia.ConsoleSystem;

public sealed record CompletionItem(string Value, string Description);

/// <summary>补全查询结果。Signature 是当前命令的参数签名（富文本），可为空。</summary>
public sealed record CompletionQuery(IReadOnlyList<CompletionItem> Items, string? Signature)
{
    public static readonly CompletionQuery Empty = new(Array.Empty<CompletionItem>(), null);
}

/// <summary>
/// 补全状态：按光标所在的词查询候选，维护选中项。
/// 弹出列表时上下键选择，Tab/Enter 接受；Esc 收起，直到文本再次变化。
/// </summary>
public sealed class CompletionEngine
{
    public const int MaxVisible = 8;

    private readonly Func<string, CompletionQuery> _provider;
    private IReadOnlyList<CompletionItem> _items = Array.Empty<CompletionItem>();
    private bool _dismissed;

    public CompletionEngine(Func<string, CompletionQuery> provider) => _provider = provider;

    public IReadOnlyList<CompletionItem> Items => _items;
    public int Selected { get; private set; }
    public int ScrollOffset { get; private set; }
    public string? Signature { get; private set; }
    public string Token { get; private set; } = "";
    public int TokenStart { get; private set; }
    public int TokenEnd { get; private set; }

    public bool PopupVisible => !_dismissed && _items.Count > 0;
    public bool SignatureVisible => !_dismissed && !string.IsNullOrEmpty(Signature);

    /// <summary>选中项与已输入的词不同，Enter 应先接受补全而不是提交。</summary>
    public bool SelectionDiffersFromToken =>
        PopupVisible && !string.Equals(_items[Selected].Value, Token, StringComparison.OrdinalIgnoreCase);

    public void Refresh(string text, int caret, bool textChanged)
    {
        if (textChanged) _dismissed = false;
        if (string.IsNullOrEmpty(text) || text[0] != '/' || caret < 1)
        {
            Clear();
            return;
        }

        caret = Math.Min(caret, text.Length);
        int start = text.LastIndexOf(' ', caret - 1) + 1;
        if (start == 0) start = 1;
        int end = text.IndexOf(' ', caret);
        if (end < 0) end = text.Length;

        string previous = Selected < _items.Count ? _items[Selected].Value : "";
        var query = _provider(text[1..caret]);
        _items = query.Items;
        Signature = query.Signature;
        TokenStart = start;
        TokenEnd = end;
        Token = text[start..end];

        int keep = -1;
        for (int i = 0; i < _items.Count; i++)
            if (_items[i].Value == previous) { keep = i; break; }
        Selected = keep >= 0 ? keep : 0;
        ScrollOffset = 0;
        EnsureVisible();
    }

    public void Move(int delta)
    {
        if (_items.Count == 0) return;
        Selected = ((Selected + delta) % _items.Count + _items.Count) % _items.Count;
        EnsureVisible();
    }

    public void Select(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        Selected = index;
        EnsureVisible();
    }

    /// <summary>用选中项替换当前词，并在其后补一个空格。返回新文本与光标。</summary>
    public (string text, int caret) Accept(string text)
    {
        var value = _items[Selected].Value;
        string rest = TokenEnd <= text.Length ? text[TokenEnd..] : "";
        if (!rest.StartsWith(' ')) rest = " " + rest;
        string result = text[..TokenStart] + value + rest;
        return (result, TokenStart + value.Length + 1);
    }

    public void Dismiss() => _dismissed = true;

    public void Clear()
    {
        _items = Array.Empty<CompletionItem>();
        Signature = null;
        Selected = 0;
        ScrollOffset = 0;
        Token = "";
        TokenStart = TokenEnd = 0;
    }

    private void EnsureVisible()
    {
        if (Selected < ScrollOffset) ScrollOffset = Selected;
        else if (Selected >= ScrollOffset + MaxVisible) ScrollOffset = Selected - MaxVisible + 1;
    }
}
