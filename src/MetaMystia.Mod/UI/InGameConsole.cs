#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

using MetaMystia.ConsoleSystem;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

namespace MetaMystia.UI;

/// <summary>
/// 游戏内控制台：聊天与命令。uGUI + TMP 绘制（见 InGameConsole.View.cs），
/// 键盘文本走 legacy Input（inputString / IME），游戏输入由 <see cref="ConsoleInputGate"/> 屏蔽。
/// </summary>
[AutoLog]
public static partial class InGameConsole
{
    private sealed class LogEntry
    {
        public readonly string Display;
        public readonly string Plain;
        public float Created = -1f;

        // 日志区测量缓存
        public float Height;
        public float MeasuredWidth;
        public int MeasuredFont;
        // 常驻气泡测量缓存
        public Vector2 PassiveSize;
        public float PassiveMaxWidth;
        public int PassiveFont;

        public LogEntry(DateTime time, string text)
        {
            string stamp = time.ToString("HH:mm:ss");
            Display = $"<color=#888899>[{stamp}]</color> {text}";
            Plain = $"[{stamp}] {ConsoleText.Strip(text)}";
        }
    }

    private const int MaxLogs = 1024;
    private const int PassiveMaxLines = 10;
    private const int MinFontSize = 12;
    private const int MaxFontSize = 40;
    private const int DefaultFontSize = 20;

    public static bool IsOpen { get; private set; }

    /// <summary>控制台正在占用键盘（打开中，或刚关闭、Enter/Esc 还未松开）。其他快捷键应在此时让路。</summary>
    public static bool OwnsKeyboard => IsOpen || ConsoleInputGate.Busy;

    private static readonly List<LogEntry> _logs = [];
    private static readonly ConcurrentQueue<LogEntry> _pending = new();
    private static readonly List<Func<string>> _deferredLogs = [];
    private static bool _deferredFlushed;

    private static readonly LineEditor _editor = new() { MaxLength = ChatPayload.MaxLength };
    private static readonly ConsoleHistory _history = new();
    private static readonly CompletionEngine _completion = new(CommandRegistry.GetCompletions);
    private static ConsoleContext _consoleContext = null!;

    // 日志滚动：距底部的像素，0 表示跟随最新
    private static float _scroll;
    private static int _unread;

    private static int _openedFrame = -1;
    private static bool _composedLastFrame;
    private static float _lastEditTime;
    private static IMECompositionMode _previousIme = IMECompositionMode.Auto;
    private static readonly Dictionary<KeyCode, float> _repeatAt = new();

    // 鼠标交互
    private enum DragMode { None, Move, Resize, Select }
    private static DragMode _drag;
    private static Vector2 _dragStart;
    private static Vector4 _dragOrigin;
    private static float _lastClickTime;

    private static int HistoryCapacity => ConfigManager.ConsoleHistorySize?.Value ?? 200;
    private static string HistoryFilePath => Path.Combine(
        BepInEx.Paths.ConfigPath, ConfigManager.ConsoleHistoryFile?.Value ?? "MetaMystia_console_history.txt");

    // ====================================================================
    // Lifecycle
    // ====================================================================

    public static void Initialize()
    {
        _consoleContext = new ConsoleContext(LogToConsole);
        CommandRegistry.Initialize();
        LoadHistory();

        LogToConsole($"<color=#66CCFF>MetaMystia</color> <color=#888899>v{MyPluginInfo.PLUGIN_VERSION}</color>");
        LogDeferred(() => $"<color=#888899>{TextId.ConsoleStarPrompt.Get()}</color>");
        LogDeferred(() => $"<color=#888899>{TextId.ConsoleHelpHint.Get()}</color>");
    }

    public static void Update()
    {
        DrainPending();
        ConsoleInputGate.Tick();

        EnsureView();

        if (IsOpen)
            HandleOpenInput();
        else if (!ConsoleInputGate.Busy)
        {
            bool command = Input.GetKeyDown(ConfigManager.KeyOpenCommand.Value);
            if (command || Input.GetKeyDown(ConfigManager.KeyOpenChat.Value))
                Open(command ? "/" : "");
        }

        Render();
    }

    public static void Shutdown()
    {
        ConsoleInputGate.ForceRestore();
        if (IsOpen) Input.imeCompositionMode = _previousIme;
    }

    private static void Open(string prefix)
    {
        if (GameFlow.LocalScene == Common.UI.Scene.DayScene && DayScene.SceneManager.Instance != null
            && DayScene.SceneManager.Instance.IsMapSwapping)
        {
            ShowPassive(TextId.ConsoleMapSwapping.Get());
            return;
        }

        Log.Info("console opened");
        IsOpen = true;
        _openedFrame = Time.frameCount;
        _composedLastFrame = false;
        ConsoleInputGate.Engage();
        _previousIme = Input.imeCompositionMode;
        Input.imeCompositionMode = IMECompositionMode.On;
        _history.Reset();
        _editor.SetText(prefix);
        OnTextChanged();
    }

    private static void Close()
    {
        if (!IsOpen) return;
        Log.Info("console closed");
        IsOpen = false;
        _drag = DragMode.None;
        _completion.Clear();
        Input.imeCompositionMode = _previousIme;
        ConsoleInputGate.Release();
    }

    // ====================================================================
    // Keyboard
    // ====================================================================

    private static void HandleOpenInput()
    {
        // 输入法组字时 Enter/Esc/方向键属于输入法；提交组字的那一帧也要忽略
        bool composing = !string.IsNullOrEmpty(Input.compositionString);
        bool imeBusy = composing || _composedLastFrame;
        _composedLastFrame = composing;

        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                    || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (imeBusy)
        {
            if (Time.frameCount != _openedFrame && ConsumeTypedText(ctrl: false)) OnTextChanged();
            HandleMouse(false, false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_completion.PopupVisible) _completion.Dismiss();
            else Close();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (_completion.SelectionDiffersFromToken) AcceptCompletion();
            else Submit();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (_completion.PopupVisible)
            {
                if (shift) _completion.Move(-1);
                else if (_completion.SelectionDiffersFromToken) AcceptCompletion();
                else _completion.Move(1);
            }
            else
                _completion.Refresh(_editor.Text, _editor.Caret, textChanged: true);
        }

        if (Repeat(KeyCode.UpArrow)) VerticalKey(-1);
        if (Repeat(KeyCode.DownArrow)) VerticalKey(1);
        if (Repeat(KeyCode.PageUp)) ScrollBy(LogViewHeight * 0.8f);
        if (Repeat(KeyCode.PageDown)) ScrollBy(-LogViewHeight * 0.8f);

        bool caretMoved = false;
        if (Repeat(KeyCode.LeftArrow)) { _editor.MoveLeft(ctrl, shift); caretMoved = true; }
        if (Repeat(KeyCode.RightArrow)) { _editor.MoveRight(ctrl, shift); caretMoved = true; }
        if (Input.GetKeyDown(KeyCode.Home)) { _editor.Home(shift); caretMoved = true; }
        if (Input.GetKeyDown(KeyCode.End)) { _editor.End(shift); caretMoved = true; }

        bool edited = false;
        if (Repeat(KeyCode.Delete)) { _editor.Delete(ctrl); edited = true; }

        if (ctrl)
        {
            if (Input.GetKeyDown(KeyCode.A)) { _editor.SelectAll(); caretMoved = true; }
            if (Input.GetKeyDown(KeyCode.C) && _editor.HasSelection) GUIUtility.systemCopyBuffer = _editor.SelectedText;
            if (Input.GetKeyDown(KeyCode.X) && _editor.HasSelection) { GUIUtility.systemCopyBuffer = _editor.Cut(); edited = true; }
            if (Input.GetKeyDown(KeyCode.V)) { _editor.Insert(GUIUtility.systemCopyBuffer ?? ""); edited = true; }
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) SetFontSize(FontSize + 2);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) SetFontSize(FontSize - 2);
        }

        // 打开控制台的那一帧，触发键产生的字符不进入输入框
        if (Time.frameCount != _openedFrame)
            edited |= ConsumeTypedText(ctrl);

        if (edited) OnTextChanged();
        else if (caretMoved) OnCaretMoved();

        HandleMouse(ctrl, shift);
    }

    private static bool ConsumeTypedText(bool ctrl)
    {
        string typed = Input.inputString;
        if (string.IsNullOrEmpty(typed)) return false;

        bool edited = false;
        var buffer = new System.Text.StringBuilder();
        void Flush()
        {
            if (buffer.Length == 0) return;
            _editor.Insert(buffer.ToString());
            buffer.Clear();
            edited = true;
        }

        foreach (char c in typed)
        {
            switch (c)
            {
                case '\b':
                    Flush();
                    _editor.Backspace(ctrl);
                    edited = true;
                    break;
                case '\x7f': // Ctrl+Backspace
                    Flush();
                    _editor.Backspace(true);
                    edited = true;
                    break;
                default:
                    if (!char.IsControl(c)) buffer.Append(c);
                    break;
            }
        }
        Flush();
        return edited;
    }

    private static void VerticalKey(int direction)
    {
        if (_completion.PopupVisible)
        {
            _completion.Move(direction);
            return;
        }
        string? text = direction < 0 ? _history.Older(_editor.Text) : _history.Newer();
        if (text == null) return;
        _editor.SetText(text);
        OnTextChanged(fromHistory: true);
    }

    private static bool Repeat(KeyCode key)
    {
        float now = Time.unscaledTime;
        if (Input.GetKeyDown(key))
        {
            _repeatAt[key] = now + 0.4f;
            return true;
        }
        if (Input.GetKey(key) && _repeatAt.TryGetValue(key, out float at) && now >= at)
        {
            _repeatAt[key] = now + 0.035f;
            return true;
        }
        return false;
    }

    private static void OnTextChanged(bool fromHistory = false)
    {
        _lastEditTime = Time.unscaledTime;
        if (!fromHistory) _history.Reset();
        _completion.Refresh(_editor.Text, _editor.Caret, textChanged: true);
    }

    private static void OnCaretMoved()
    {
        _lastEditTime = Time.unscaledTime;
        _completion.Refresh(_editor.Text, _editor.Caret, textChanged: false);
    }

    private static void AcceptCompletion()
    {
        var (text, caret) = _completion.Accept(_editor.Text);
        _editor.SetText(text, caret);
        OnTextChanged();
    }

    private static void Submit()
    {
        string text = _editor.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            Close();
            return;
        }

        _history.Add(text);
        SaveHistory();
        _editor.Clear();
        _completion.Clear();
        ScrollToLatest();

        bool close = Execute(text);
        if (close) Close();
        else OnTextChanged();
    }

    private static bool Execute(string text)
    {
        Log.LogMessage($"Console Command: {text}");
        if (text[0] != '/')
        {
            if (GameSession.IsOnline)
                ChatMessage.Send(text);
            else
                AddPeerMessage(LiveModeManager.GetLocalDisplayName(), text);
            return true;
        }
        return CommandRegistry.Execute(text[1..], _consoleContext);
    }

    // ====================================================================
    // Mouse
    // ====================================================================

    private static void HandleMouse(bool ctrl, bool shift)
    {
        Vector2 mouse = MouseRef();
        bool locked = LiveModeManager.LockConsoleUi;

        float wheel = Input.mouseScrollDelta.y;
        if (wheel != 0f && PanelRect.Contains(mouse))
        {
            if (ctrl) SetFontSize(FontSize + (wheel > 0 ? 1 : -1));
            else if (_completion.PopupVisible && PopupRect.Contains(mouse)) _completion.Move(wheel > 0 ? -1 : 1);
            else ScrollBy(wheel * LineHeight * 3f);
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (TryPopupRowAt(mouse, out int row))
            {
                _completion.Select(row);
                AcceptCompletion();
            }
            else if (!locked && GripRect.Contains(mouse))
                BeginDrag(DragMode.Resize, mouse);
            else if (!locked && TitleRect.Contains(mouse))
                BeginDrag(DragMode.Move, mouse);
            else if (_unread > 0 && NewMessagesRect.Contains(mouse))
                ScrollToLatest();
            else if (InputRect.Contains(mouse))
            {
                int index = CaretIndexAt(mouse.x);
                bool doubleClick = Time.unscaledTime - _lastClickTime < 0.3f;
                _lastClickTime = Time.unscaledTime;
                if (doubleClick) _editor.SelectWordAt(index);
                else _editor.MoveTo(index, shift);
                _drag = doubleClick ? DragMode.None : DragMode.Select;
                OnCaretMoved();
            }
            else if (TryLogAt(mouse, out var entry))
            {
                GUIUtility.systemCopyBuffer = entry.Plain;
                FlashCopied(entry);
            }
        }

        if (_drag != DragMode.None && Input.GetMouseButton(0))
            ContinueDrag(mouse);
        if (Input.GetMouseButtonUp(0))
            _drag = DragMode.None;
    }

    private static void BeginDrag(DragMode mode, Vector2 mouse)
    {
        _drag = mode;
        _dragStart = mouse;
        _dragOrigin = new Vector4(ConfigManager.ConsoleLeft.Value, ConfigManager.ConsoleBottom.Value,
            ConfigManager.ConsoleWidth.Value, ConfigManager.ConsoleHeight.Value);
    }

    private static void ContinueDrag(Vector2 mouse)
    {
        Vector2 delta = mouse - _dragStart;
        switch (_drag)
        {
            case DragMode.Move:
                ConfigManager.ConsoleLeft.Value = _dragOrigin.x + delta.x;
                ConfigManager.ConsoleBottom.Value = _dragOrigin.y + delta.y;
                break;
            case DragMode.Resize:
                ConfigManager.ConsoleWidth.Value = _dragOrigin.z + delta.x;
                ConfigManager.ConsoleHeight.Value = _dragOrigin.w + delta.y;
                break;
            case DragMode.Select:
                _editor.MoveTo(CaretIndexAt(mouse.x), select: true);
                _lastEditTime = Time.unscaledTime;
                break;
        }
    }

    // ====================================================================
    // Scrolling
    // ====================================================================

    private static void ScrollBy(float delta)
    {
        _scroll = Mathf.Clamp(_scroll + delta, 0f, MaxScroll());
        if (_scroll <= 0.5f) _unread = 0;
    }

    private static void ScrollToLatest()
    {
        _scroll = 0f;
        _unread = 0;
    }

    // ====================================================================
    // Layout settings
    // ====================================================================

    private static int FontSize
    {
        get
        {
            int v = ConfigManager.ConsoleFontSize.Value;
            return v <= 0 ? DefaultFontSize : Mathf.Clamp(v, MinFontSize, MaxFontSize);
        }
    }

    public static void SetFontSize(int size)
    {
        ConfigManager.ConsoleFontSize.Value = Mathf.Clamp(size, MinFontSize, MaxFontSize);
    }

    public static void ResetLayout()
    {
        foreach (var entry in new BepInEx.Configuration.ConfigEntryBase[]
                 {
                     ConfigManager.ConsoleLeft, ConfigManager.ConsoleBottom, ConfigManager.ConsoleWidth,
                     ConfigManager.ConsoleHeight, ConfigManager.ConsoleFontSize,
                 })
            entry.BoxedValue = entry.DefaultValue;
    }

    // ====================================================================
    // History
    // ====================================================================

    private static void LoadHistory()
    {
        _history.Capacity = HistoryCapacity;
        try
        {
            if (File.Exists(HistoryFilePath))
                _history.Load(File.ReadAllLines(HistoryFilePath));
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to load console history: {ex.Message}");
        }
    }

    private static void SaveHistory()
    {
        try
        {
            File.WriteAllLines(HistoryFilePath, _history.Items);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save console history: {ex.Message}");
        }
    }

    // ====================================================================
    // Logging API
    // ====================================================================

    /// <summary>追加一行日志（富文本），任意线程可调用。</summary>
    public static void LogToConsole(string message) => _pending.Enqueue(new LogEntry(DateTime.Now, message));

    public static void LogDeferred(Func<string> messageFactory)
    {
        if (_deferredFlushed) LogToConsole(messageFactory());
        else _deferredLogs.Add(messageFactory);
    }

    public static void FlushDeferred()
    {
        foreach (var factory in _deferredLogs)
            LogToConsole(factory());
        _deferredLogs.Clear();
        _deferredFlushed = true;
    }

    /// <summary>聊天消息。发送者与内容按外部文本转义。</summary>
    public static void AddPeerMessage(string senderName, string message)
    {
        message = LiveModeManager.MaskMessage(message);
        LogToConsole(TextId.PeerMessagePrefix.Get(ConsoleText.Escape(senderName), ConsoleText.Escape(message)));
    }

    public static void ClearLogs()
    {
        _logs.Clear();
        while (_pending.TryDequeue(out _)) { }
        ScrollToLatest();
    }

    /// <summary>金色事件消息，任意线程可调用。</summary>
    public static void ShowPassive(string text) => LogToConsole($"<color=#FFCC66>{text}</color>");

    public static void LogSuccess(string text) => LogToConsole($"<color=#66FF88>{text}</color>");

    public static void LogError(string text) => LogToConsole($"<color=#FF6666>{text}</color>");

    public static void LogAlert(string text) => LogToConsole($"<size=125%><color=#FF4444><b>{text}</b></color></size>");

    private static void DrainPending()
    {
        while (_pending.TryDequeue(out var entry))
        {
            entry.Created = Time.unscaledTime;
            _logs.Add(entry);
            if (_logs.Count > MaxLogs) _logs.RemoveAt(0);
            if (_scroll > 0f)
            {
                _scroll += MeasureLog(entry);
                _unread = Math.Min(_unread + 1, MaxLogs);
            }
        }
    }
}
