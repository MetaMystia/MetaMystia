#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using UnityEngine;
using UnityEngine.EventSystems;

using MetaMystia.ConsoleSystem;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.UI;

/// <summary>
/// 聊天与命令共用的控制台。T 打开聊天，/ 打开命令；关闭时在原位置短暂显示新消息。
/// </summary>
[AutoLog]
public static partial class InGameConsole
{
    private sealed class Entry
    {
        public string Body = "";
        public DateTime Time;
        public float Shown;
        public int Count = 1;
        public RectTransform? Row;
        public UnityEngine.UI.Image Bubble = null!;
        public CanvasGroup Group = null!;
        public TMPro.TextMeshProUGUI Text = null!;
        public float Alpha = -1f;
        public float Top;
        public float Height;
        public float Width;
    }

    private const int MaxEntries = 300;
    private const float RepeatWindow = 30f;

    private static readonly List<Entry> _entries = [];
    private static readonly ConcurrentQueue<string> _pending = new();
    private static readonly List<Func<string>> _deferred = [];
    private static bool _deferredFlushed;

    private static ConsoleContext _context = null!;
    private static ConsoleHistory _history = null!;
    private static readonly CompletionEngine _completion = new(CommandRegistry.Complete);
    private static bool _open;
    private static GameObject? _previousSelection;

    public static bool IsOpen => _open;

    /// <summary>控制台打开，或在本帧刚关闭（提交的回车不应再触发其他快捷键）。</summary>
    public static bool HasKeyboardFocus => _open || _closedFrame == Time.frameCount;
    private static int _closedFrame = -1;

    private static string HistoryFilePath => Path.Combine(BepInEx.Paths.ConfigPath, ConfigManager.ConsoleHistoryFile.Value);

    public static void Initialize()
    {
        _context = new ConsoleContext(LogToConsole);
        _history = new ConsoleHistory(ConfigManager.ConsoleHistorySize.Value);
        LoadHistory();
        LogToConsole($"{ConsoleFormat.Cmd("MetaMystia")} {ConsoleFormat.Dim("v" + MyPluginInfo.PLUGIN_VERSION)}");
        LogDeferred(() => ConsoleFormat.Dim(TextId.ConsoleStarPrompt.Get()));
        LogDeferred(() => ConsoleFormat.Dim(TextId.ConsoleHelpHint.Get()));
    }

    // ── 日志 ──

    /// <summary>追加一行富文本，可在任意线程调用。</summary>
    public static void LogToConsole(string message) => _pending.Enqueue(message);

    public static void ShowPassive(string text) => LogToConsole(ConsoleFormat.Event(text));

    public static void ShowPassiveFromAnyThread(string text) => PluginManager.RunOnMainThread(() => ShowPassive(text));

    public static void LogSuccess(string text) => LogToConsole(ConsoleFormat.Ok(text));

    public static void LogError(string text) => LogToConsole(ConsoleFormat.Err(text));

    /// <summary>显示聊天消息；发送者与正文按纯文本显示。</summary>
    public static void AddChat(string sender, string message, bool fromServer = false)
    {
        string color = fromServer ? OverlayUi.Hex(OverlayUi.Accent) : NameColor(sender);
        LogToConsole(TextId.PeerMessagePrefix.Get(
            $"<color={color}>{ConsoleFormat.Plain(sender)}</color>",
            ConsoleFormat.Plain(LiveModeManager.MaskMessage(message))));
    }

    private static readonly string[] NameColors = ["#8FD3FF", "#B8E986", "#FFB3C7", "#C5B3FF", "#FFD480", "#7EE0C8"];

    private static string NameColor(string name)
    {
        int hash = 17;
        foreach (char c in name) hash = hash * 31 + c;
        return NameColors[(hash & int.MaxValue) % NameColors.Length];
    }

    public static void LogDeferred(Func<string> messageFactory)
    {
        if (_deferredFlushed) LogToConsole(messageFactory());
        else _deferred.Add(messageFactory);
    }

    public static void FlushDeferred()
    {
        _deferredFlushed = true;
        foreach (var factory in _deferred)
            LogToConsole(factory());
        _deferred.Clear();
        OverlayUi.RefreshFont();
        if (_root != null) ApplyLayout(force: true);
    }

    public static void ClearLogs()
    {
        while (_pending.TryDequeue(out _)) { }
        foreach (var entry in _entries)
            if (entry.Row != null) UnityEngine.Object.Destroy(entry.Row.gameObject);
        _entries.Clear();
        _scroll = 0f;
        ScrollToLatest();
        _layoutDirty = true;
    }

    private static void DrainPending()
    {
        float now = Time.unscaledTime;
        while (_pending.TryDequeue(out var message))
        {
            var last = _entries.Count > 0 ? _entries[^1] : null;
            if (last != null && last.Body == message && (DateTime.Now - last.Time).TotalSeconds < RepeatWindow)
            {
                last.Count++;
                last.Time = DateTime.Now;
                last.Shown = now;
                UpdateRow(last);
                _layoutDirty = true;
                continue;
            }
            var entry = new Entry { Body = message, Time = DateTime.Now, Shown = now };
            _entries.Add(entry);
            if (_entries.Count > MaxEntries)
            {
                var removed = _entries[0];
                _entries.RemoveAt(0);
                if (!_follow) _scroll = Mathf.Max(0f, _scroll - removed.Height - RowGap);
                if (removed.Row != null) UnityEngine.Object.Destroy(removed.Row.gameObject);
            }
            UpdateRow(entry);
            _layoutDirty = true;
            if (!_follow) _unread++;
        }
    }

    private static string RowText(Entry entry) =>
        $"<color=#6F6A7A><size=72%>{entry.Time:HH:mm}</size></color><indent=2.6em>{BodyText(entry)}</indent>";

    private static string BodyText(Entry entry) =>
        entry.Count > 1 ? $"{entry.Body} <color=#8C8698>×{entry.Count}</color>" : entry.Body;

    // ── 帧循环 ──

    public static void Update()
    {
        DrainPending();
        if (!EnsureView()) return;
        OverlayUi.KeepCursorOnTop();

        if (!_open)
        {
            if (Input.GetKeyDown(ConfigManager.KeyOpenCommand.Value)) Open("/");
            else if (Input.GetKeyDown(ConfigManager.KeyOpenChat.Value)) Open("");
        }
        else
        {
            HandleInput();
        }
        UpdateView();
    }

    public static void LateUpdate()
    {
        if (!_open || !_field.isFocused) return;
        if (_caretToEnd) _field.MoveTextEnd(false);
        else if (_caretRestore >= 0) _field.stringPosition = _caretRestore;
        _caretToEnd = false;
        _caretRestore = -1;
        _caretBefore = _field.stringPosition;
    }

    private static void Open(string text)
    {
        _open = true;
        var eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            _previousSelection = eventSystem.currentSelectedGameObject;
            eventSystem.sendNavigationEvents = false;
        }
        Log.Info("console opened");
        SetText(text, refresh: true);
        _history.ResetCursor();
        ScrollToLatest();
        ShowOpen(true);
        _field.ActivateInputField();
    }

    private static void Close()
    {
        _open = false;
        _closedFrame = Time.frameCount;
        _drag = DragMode.None;
        _field.DeactivateInputField();
        SetText("", refresh: false);
        _completion.Reset();
        ScrollToLatest();
        ShowOpen(false);
        var eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            eventSystem.sendNavigationEvents = true;
            var previous = _previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null;
            eventSystem.SetSelectedGameObject(previous);
        }
        _previousSelection = null;
        Log.Info("console closed");
    }

    private static void HandleInput()
    {
        // 文本变化：打字、粘贴、删除。
        if (_field.text != _text)
        {
            SetText(_field.text, refresh: true, moveCaret: false);
            _history.ResetCursor();
        }
        if (!_field.isFocused) _field.ActivateInputField();

        if (Input.compositionString.Length == 0)
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Tab) && _completion.Cycle(shift) is { } completed)
                SetText(completed, refresh: false);
            else if (Input.GetKeyDown(KeyCode.UpArrow)) Navigate(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) Navigate(1);
            else if (Input.GetKeyDown(KeyCode.PageUp)) PageScroll(-1);
            else if (Input.GetKeyDown(KeyCode.PageDown)) PageScroll(1);
        }
        HandlePointer();
    }

    private static void Navigate(int direction)
    {
        // 只输入了 / 或正在浏览历史时 ↑↓ 用于历史，其余情况在候选中选择。
        if (_completion.HasItems && !_history.IsBrowsing && (_completion.IsEngaged || _text.Length > 1))
        {
            _completion.Move(direction);
            _caretRestore = _caretBefore;
            _popupDirty = true;
            return;
        }
        string? text = direction < 0 ? _history.Previous(_text) : _history.Next();
        if (text != null) SetText(text, refresh: true);
        else _caretRestore = _caretBefore;
    }

    private static void PageScroll(int direction)
    {
        ScrollBy(direction * _viewHeight * 0.8f);
        _caretRestore = _caretBefore;
    }

    private static void SetText(string text, bool refresh, bool moveCaret = true)
    {
        _text = text;
        if (_field.text != text) _field.text = text;
        if (refresh) _completion.Refresh(text);
        _caretToEnd |= moveCaret;
        _popupDirty = true;
    }

    private static void OnSubmit(string text)
    {
        if (_completion.HasPendingSelection)
        {
            SetText(_completion.Accept(_completion.Selected), refresh: false);
            return;
        }
        text = text.Trim();
        if (text.Length == 0)
        {
            Close();
            return;
        }
        _history.Add(text);
        if (text[0] != '/')
        {
            if (GameSession.IsOnline) ChatMessage.Send(text);
            else AddChat(LiveModeManager.GetLocalDisplayName(), text);
            Close();
            return;
        }
        SaveHistory();
        Log.Message($"Console Command: {text}");
        LogToConsole(ConsoleFormat.Dim("› " + ConsoleFormat.Plain(text)));
        bool close = CommandRegistry.Execute(text[1..], _context);
        SetText("", refresh: true);
        ScrollToLatest();
        if (close) Close();
    }

    private static void OnEndEdit(string _)
    {
        if (!_open || !_field.wasCanceled) return;
        if (_completion.IsEngaged)
        {
            _completion.Dismiss();
            _popupDirty = true;
        }
        else
        {
            Close();
        }
    }

    // ── 历史文件 ──

    private static void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryFilePath)) return;
            foreach (var line in File.ReadAllLines(HistoryFilePath))
                if (line.StartsWith('/')) _history.Add(line);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to load console history: {ex.Message}");
        }
    }

    // 只保存命令，聊天内容不落盘。
    private static void SaveHistory()
    {
        try
        {
            File.WriteAllLines(HistoryFilePath, _history.Items.Where(line => line.StartsWith('/')));
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save console history: {ex.Message}");
        }
    }
}
