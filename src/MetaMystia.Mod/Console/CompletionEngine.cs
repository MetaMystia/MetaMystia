#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace MetaMystia.ConsoleSystem;

/// <summary>
/// Minecraft-style tab completion engine for the in-game console.
/// Tab applies the first match inline; subsequent Tabs cycle through alternatives.
/// Space confirms the current selection and moves on.
/// </summary>
public class CompletionEngine
{
    private List<string> _completions = [];
    private int _completionIndex = -1;
    private string _originalInput = "";
    private string _matchPrefix = "";
    private int _scrollOffset = 0;
    private bool _dismissed = false;
    private string? _hint = null;

    // Tab-cycling state (Minecraft-style)
    private bool _isTabCycling = false;
    private string _tabCycleBase = "";   // Input BEFORE first Tab press
    private int _tabCycleIndex = -1;

    public const int MaxVisibleItems = 8;

    public bool IsActive => !_dismissed && (_completions.Count > 0 || _hint != null);
    public bool HasCompletions => _completions.Count > 0;
    public bool HasHint => _hint != null && _completions.Count == 0;
    public string? Hint => _hint;
    public IReadOnlyList<string> Completions => _completions;
    public int SelectedIndex => _completionIndex;
    public int ScrollOffset => _scrollOffset;
    public string MatchPrefix => _matchPrefix;
    public bool IsTabCycling => _isTabCycling;

    /// <summary>
    /// Called on every input change (from typing, NOT from Tab).
    /// Exits tab-cycling if active, then refreshes completions.
    /// </summary>
    public void UpdateCompletions(string input)
    {
        if (_isTabCycling)
            ExitTabCycle();

        _dismissed = false;

        if (string.IsNullOrEmpty(input) || !input.StartsWith("/"))
        {
            ClearCompletions();
            return;
        }

        string commandPart = input[1..];
        var result = CommandRegistry.GetCompletions(commandPart);

        if (result.Items.Count == 0 && result.Hint == null)
        {
            ClearCompletions();
            return;
        }

        int lastSpace = input.LastIndexOf(' ');
        _matchPrefix = lastSpace >= 0 ? input[(lastSpace + 1)..] : input[1..];

        _originalInput = input;
        _hint = result.Hint;

        if (result.Items.Count > 0)
        {
            _completions = result.Items;
            _completionIndex = 0;
            _scrollOffset = 0;
        }
        else
        {
            _completions = [];
            _completionIndex = -1;
            _scrollOffset = 0;
        }
    }

    /// <summary>
    /// Minecraft-style Tab press: enter or advance tab-cycling.
    /// Returns the new input with the completion applied inline (no trailing space).
    /// </summary>
    public string? TabCycle(bool reverse = false)
    {
        if (!HasCompletions) return null;

        if (!_isTabCycling)
        {
            _isTabCycling = true;
            _tabCycleBase = _originalInput;
            _tabCycleIndex = 0;
        }
        else
        {
            if (reverse)
                _tabCycleIndex = (_tabCycleIndex - 1 + _completions.Count) % _completions.Count;
            else
                _tabCycleIndex = (_tabCycleIndex + 1) % _completions.Count;
        }

        _completionIndex = _tabCycleIndex;
        EnsureSelectedVisible();

        return ApplyCompletion(_tabCycleBase, _completions[_tabCycleIndex]);
    }

    public void ExitTabCycle()
    {
        _isTabCycling = false;
        _tabCycleBase = "";
        _tabCycleIndex = -1;
    }

    public void Dismiss()
    {
        _dismissed = true;
        if (_isTabCycling) ExitTabCycle();
    }

    public void Reset()
    {
        ClearCompletions();
        _dismissed = false;
        if (_isTabCycling) ExitTabCycle();
    }

    public string FormatWithHighlight(string item)
    {
        if (string.IsNullOrEmpty(_matchPrefix) || !item.StartsWith(_matchPrefix, System.StringComparison.OrdinalIgnoreCase))
            return item;

        string matched = item[.._matchPrefix.Length];
        string rest = item[_matchPrefix.Length..];
        return $"<color=#66AAFF>{matched}</color>{rest}";
    }

    private void ClearCompletions()
    {
        _completions = [];
        _completionIndex = -1;
        _scrollOffset = 0;
        _originalInput = "";
        _matchPrefix = "";
        _hint = null;
    }

    private void EnsureSelectedVisible()
    {
        if (_completionIndex < _scrollOffset)
            _scrollOffset = _completionIndex;
        else if (_completionIndex >= _scrollOffset + MaxVisibleItems)
            _scrollOffset = _completionIndex - MaxVisibleItems + 1;
    }

    /// <summary>
    /// Replace the last partial token in input with the completion.
    /// Does NOT add trailing space — the user confirms with space.
    /// </summary>
    private static string ApplyCompletion(string input, string completion)
    {
        int lastSpace = input.LastIndexOf(' ');
        if (lastSpace < 0)
            return "/" + completion;
        return input[..(lastSpace + 1)] + completion;
    }
}
