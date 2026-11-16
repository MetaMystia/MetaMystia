#nullable enable
using System;
using System.Text;

namespace MetaMystia.ConsoleSystem;

/// <summary>单行文本编辑状态：文本、光标、选区。不依赖 Unity，按代理对移动光标。</summary>
public sealed class LineEditor
{
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public int Anchor { get; private set; }
    public int MaxLength { get; set; } = 512;

    public bool HasSelection => Caret != Anchor;
    public int SelectionStart => Math.Min(Caret, Anchor);
    public int SelectionEnd => Math.Max(Caret, Anchor);
    public string SelectedText => Text[SelectionStart..SelectionEnd];

    public void SetText(string text) => SetText(text, int.MaxValue);

    public void SetText(string text, int caret)
    {
        Text = Clip(Sanitize(text), MaxLength);
        Caret = Anchor = Math.Clamp(caret, 0, Text.Length);
    }

    public void Clear() => SetText("");

    public void Insert(string value)
    {
        value = Sanitize(value);
        if (value.Length == 0) return;
        DeleteSelection();
        value = Clip(value, MaxLength - Text.Length);
        if (value.Length == 0) return;
        Text = Text.Insert(Caret, value);
        Caret = Anchor = Caret + value.Length;
    }

    public bool DeleteSelection()
    {
        if (!HasSelection) return false;
        int start = SelectionStart;
        Text = Text.Remove(start, SelectionEnd - start);
        Caret = Anchor = start;
        return true;
    }

    public void Backspace(bool word)
    {
        if (DeleteSelection() || Caret == 0) return;
        int start = word ? WordLeft(Caret) : Prev(Caret);
        Text = Text.Remove(start, Caret - start);
        Caret = Anchor = start;
    }

    public void Delete(bool word)
    {
        if (DeleteSelection() || Caret == Text.Length) return;
        int end = word ? WordRight(Caret) : Next(Caret);
        Text = Text.Remove(Caret, end - Caret);
        Anchor = Caret;
    }

    public void MoveLeft(bool word, bool select)
    {
        if (!select && HasSelection) { Caret = Anchor = SelectionStart; return; }
        MoveTo(word ? WordLeft(Caret) : Prev(Caret), select);
    }

    public void MoveRight(bool word, bool select)
    {
        if (!select && HasSelection) { Caret = Anchor = SelectionEnd; return; }
        MoveTo(word ? WordRight(Caret) : Next(Caret), select);
    }

    public void Home(bool select) => MoveTo(0, select);
    public void End(bool select) => MoveTo(Text.Length, select);

    public void MoveTo(int index, bool select)
    {
        Caret = Snap(Math.Clamp(index, 0, Text.Length));
        if (!select) Anchor = Caret;
    }

    public void SelectAll()
    {
        Anchor = 0;
        Caret = Text.Length;
    }

    public void SelectWordAt(int index)
    {
        index = Math.Clamp(index, 0, Text.Length);
        int start = index, end = index;
        while (start > 0 && !char.IsWhiteSpace(Text[start - 1])) start--;
        while (end < Text.Length && !char.IsWhiteSpace(Text[end])) end++;
        Anchor = start;
        Caret = end;
    }

    public string Cut()
    {
        var s = SelectedText;
        DeleteSelection();
        return s;
    }

    private int Prev(int i)
    {
        if (i <= 0) return 0;
        return i >= 2 && char.IsSurrogatePair(Text[i - 2], Text[i - 1]) ? i - 2 : i - 1;
    }

    private int Next(int i)
    {
        if (i >= Text.Length) return Text.Length;
        return i + 1 < Text.Length && char.IsSurrogatePair(Text[i], Text[i + 1]) ? i + 2 : i + 1;
    }

    private int Snap(int i) =>
        i > 0 && i < Text.Length && char.IsSurrogatePair(Text[i - 1], Text[i]) ? i - 1 : i;

    private int WordLeft(int i)
    {
        while (i > 0 && char.IsWhiteSpace(Text[i - 1])) i--;
        while (i > 0 && !char.IsWhiteSpace(Text[i - 1])) i--;
        return i;
    }

    private int WordRight(int i)
    {
        while (i < Text.Length && char.IsWhiteSpace(Text[i])) i++;
        while (i < Text.Length && !char.IsWhiteSpace(Text[i])) i++;
        return i;
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        StringBuilder? sb = null;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool bad = char.IsControl(c);
            if (bad && sb == null) sb = new StringBuilder(value, 0, i, value.Length);
            if (!bad) sb?.Append(c);
            else if (c is '\n' or '\r' or '\t') sb!.Append(' ');
        }
        return sb?.ToString() ?? value;
    }

    private static string Clip(string value, int max)
    {
        if (max <= 0) return "";
        if (value.Length <= max) return value;
        int n = max;
        if (char.IsHighSurrogate(value[n - 1])) n--;
        return value[..n];
    }
}
