#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

using Mystia;
using Mystia.Imgui;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.ConsoleSystem;

using Color = Mystia.Numerics.Color;
using Rect = Mystia.Numerics.Rect;
using Vector2 = Mystia.Numerics.Vector2;

namespace MetaMystia.UI;

[AutoLog]
public static partial class InGameConsole
{
    // ====================================================================
    // Log entry with timestamp for passive fade
    // ====================================================================
    private class LogEntry
    {
        public string Text;
        public float Timestamp; // 建条目时的 ModRuntime.Clock.Now（不受 timeScale 的秒数）

        // Cached layout (invalidated when width/fontSize differ)
        public float CachedHeight;
        public float CachedWidth;
        public int CachedFontSize;
        public float CachedTextWidth; // for passive bubble bg sizing

        public LogEntry(string text)
        {
            Text = text;
            Timestamp = ModRuntime.Clock.Now;
        }
    }

    // ====================================================================
    // State
    // ====================================================================
    private static bool _isOpen = false;
    public static bool IsOpen
    {
        get { return _isOpen; }
        set
        {
            if (_isOpen != value)
            {
                Log.Info($"console {(value ? "opened" : "closed")}");
                _isOpen = value;
                if (value)
                    _scrollToBottom = true;
                UpdateGameInputState();
            }
        }
    }

    private static string input = "";
    private static Vector2 scrollPosition;
    private static readonly List<LogEntry> _logs = [];
    // Pending logs queued from any thread; drained only on ImguiEventKind.Layout to keep
    // GUILayout control counts consistent between Layout and Repaint passes.
    private static readonly ConcurrentQueue<string> _pendingLogs = new();
    private static List<string> inputs = [];
    private static int inputsCursor = 0;
    private const int MaxLogs = 1024;
    private static int MaxHistorySize => ConfigManager.ConsoleHistorySize?.Value ?? 200;
    private static string HistoryFileName => ConfigManager.ConsoleHistoryFile?.Value ?? "MetaMystia_console_history.txt";
    private static string HistoryFilePath => Path.Combine(ModRuntime.Directory, HistoryFileName);
    private static bool focusTextField = true;
    private static bool moveCursor = false;
    private const string TextFieldControlName = "ConsoleInput";
    private static bool justOpened = false;
    private static bool _scrollToBottom = false;

    // Command system
    private static ConsoleContext _consoleContext = null!;
    private static CompletionEngine _completion = new();

    // Deferred log queue
    private static readonly List<Func<string>> _deferredLogs = [];
    private static bool _deferredFlushed = false;

    // ====================================================================
    // Passive mode config (Minecraft-style fade)
    // ====================================================================
    private const int PassiveMaxLines = 10;
    private static float PassiveLingerTime => ConfigManager.ConsolePassiveLingerTime.Value;
    private static float PassiveFadeTime => ConfigManager.ConsolePassiveFadeTime.Value;

    // ====================================================================
    // Layout constants
    // ====================================================================
    private const float InputHeight = 48f;
    private const float Padding = 8f;
    private const float BottomMargin = 100f;         // avoid version text at bottom-left
    private const float DragHandleHeight = 14f;      // thin drag bar at top of console

    // Dragging state
    private static bool _isDragging = false;
    private static Vector2 _dragOffset;

    // Resize state
    private static bool _isResizing = false;
    private static Vector2 _resizeStart;
    private static float _resizeStartW, _resizeStartH;
    private const float ResizeHandleSize = 14f;
    private const float MinPanelW = 300f;
    private const float MinPanelH = 120f;

    // ====================================================================
    // IMGUI style cache
    // ====================================================================
    private static TextStyleHandle? _logStyle;
    private static TextStyleHandle? _inputStyle;
    private static TextStyleHandle? _completionStyle;
    private static TextStyleHandle? _completionSelectedStyle;
    private static TextStyleHandle? _fontBtnStyle;
    private static bool _stylesInitialized = false;
    private static FontHandle? _font;

    // 面板底色：框架只给一张白纹理，颜色由 drawer.Color 乘上去
    //（原先每种颜色各造一张 1x1 贴图，现在只留颜色）。
    private static readonly Color LogBgColor = new(0.05f, 0.05f, 0.08f, 0.55f);
    private static readonly Color InputBgColor = new(0.0f, 0.0f, 0.0f, 0.50f);
    private static readonly Color CompletionBgColor = new(0.10f, 0.10f, 0.15f, 0.92f);
    private static readonly Color CompletionSelectedBgColor = new(0.25f, 0.40f, 0.65f, 0.90f);
    private static readonly Color DragHandleColor = new(0.3f, 0.3f, 0.4f, 0.6f);
    private static readonly Color ResizeHandleColor = new(0.4f, 0.4f, 0.5f, 0.7f);

    /// <summary>
    /// 原先那张 1x1 纯黑阴影贴图的不透明度：白纹理替掉它之后，原贴图 alpha 与原先叠在
    /// <see cref="IIMGUIDrawer.Color"/> 上的 0.85 一并折进这里，画面结果不变。
    /// </summary>
    private const float ShadowAlpha = 0.35f;

    public static void ResetStyles() => _stylesInitialized = false;

    public static void Initialize()
    {
        _consoleContext = new ConsoleContext(LogToConsole);
        CommandRegistry.Initialize();
        LoadHistory();

        LogToConsole($"<color=#66CCFF>MetaMystia</color> <color=#888899>v{ModRuntime.Version}</color>");
        LogDeferred(() => $"<color=#888899>{TextId.ConsoleStarPrompt.Get()}</color>");
        LogDeferred(() => $"<color=#888899>{TextId.ConsoleHelpHint.Get()}</color>");
    }

    private static void LoadHistory()
    {
        try
        {
            // 控制台历史是模组私有状态，走框架的模组存储缓存区（原始流，位置对模组隐藏）；
            // 存储未就绪（早期初始化）时退回模组目录，与 ConfigManager 的落盘策略一致。
            if (ModRuntime.Storage is { } storage)
            {
                if (!storage.TryOpenRead(HistoryFileName, out var stream))
                    return;
                using (stream)
                using (var reader = new StreamReader(stream))
                    while (reader.ReadLine() is { } line)
                        inputs.Add(line);
            }
            else
            {
                if (!File.Exists(HistoryFilePath))
                    return;
                inputs.AddRange(File.ReadAllLines(HistoryFilePath));
            }

            if (inputs.Count > MaxHistorySize)
                inputs.RemoveRange(0, inputs.Count - MaxHistorySize);
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
            var toSave = inputs.Count > MaxHistorySize
                ? inputs.GetRange(inputs.Count - MaxHistorySize, MaxHistorySize)
                : inputs;
            if (ModRuntime.Storage is { } storage)
            {
                if (!storage.TryOpenWrite(HistoryFileName, out var stream))
                    return;
                using (stream)
                using (var writer = new StreamWriter(stream))
                    foreach (var line in toSave)
                        writer.WriteLine(line);
            }
            else
            {
                var directory = Path.GetDirectoryName(HistoryFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllLines(HistoryFilePath, toSave);
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Failed to save console history: {ex.Message}");
        }
    }

    public static void LogDeferred(Func<string> messageFactory)
    {
        if (_deferredFlushed)
            LogToConsole(messageFactory());
        else
            _deferredLogs.Add(messageFactory);
    }

    public static void FlushDeferred()
    {
        foreach (var factory in _deferredLogs)
            LogToConsole(factory());
        _deferredLogs.Clear();
        _deferredFlushed = true;
    }

    public static void AddPeerMessage(string senderName, string message)
    {
        message = LiveModeManager.MaskMessage(message);
        LogToConsole(TextId.PeerMessagePrefix.Get(senderName, message));
    }

    public static void ClearLogs()
    {
        _logs.Clear();
        while (_pendingLogs.TryDequeue(out _)) { }
    }

    private static void UpdateGameInputState()
    {
        try
        {
            UniversalGameManager.UpdatePlayerInputAvailability(!IsOpen);
        }
        catch (Exception e)
        {
            Log.LogWarning($"Console: Failed to update UniversalGameManager input: {e.Message}");
        }

        // 场景里没有事件系统时，写入是无操作、读取回落到 true，等价原先「取不到就跳过」。
        ModRuntime.CommonServices.UiNavigationEnabled = !IsOpen;
    }

    public static void Update()
    {
        // 打开期间每帧强推一次：读到的仍是事件系统的真实开关，只有它为真时才写。
        if (IsOpen && ModRuntime.CommonServices.UiNavigationEnabled)
            ModRuntime.CommonServices.UiNavigationEnabled = false;

        if (justOpened) justOpened = false;

        if (ModRuntime.Input.IsKeyDown(ConfigManager.KeyOpenCommand.Value) || ModRuntime.Input.IsKeyDown(ConfigManager.KeyOpenChat.Value))
        {
            if (!IsOpen)
            {
                IsOpen = true;
                input = ModRuntime.Input.IsKeyDown(ConfigManager.KeyOpenCommand.Value) ? "/" : "";
                focusTextField = true;
                moveCursor = true;
                justOpened = true;
                _completion.Reset();
            }
        }
    }

    // ====================================================================
    // Styles
    // ====================================================================
    #region IMGUI Styles

    private static FontHandle? GetFont(IIMGUIDrawer drawer)
    {
        if (_font != null) return _font;
        try
        {
            _font = drawer.CreateFontFromOsFont("Microsoft YaHei", 1);
            if (_font != null) return _font;
        }
        catch { /* fallback */ }
        return drawer.Skin.Font;
    }

    private static void InitStyles(IIMGUIDrawer drawer)
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        var font = GetFont(drawer);

        int fontSize = ConfigManager.ConsoleFontSize.Value > 0
            ? ConfigManager.ConsoleFontSize.Value
            : Math.Clamp((int)drawer.ScreenSize.Y / 50, 14, 24);

        _logStyle = drawer.Skin.Label;
        _logStyle.Font = font;
        _logStyle.FontSize = fontSize;
        _logStyle.WordWrap = true;
        _logStyle.RichText = true;
        _logStyle.Normal.TextColor = Color.White;
        _logStyle.Padding.Left = 6;
        _logStyle.Padding.Right = 6;
        _logStyle.Padding.Top = 2;
        _logStyle.Padding.Bottom = 2;

        _inputStyle = drawer.Skin.TextField;
        _inputStyle.Font = font;
        _inputStyle.FontSize = fontSize;
        _inputStyle.RichText = false;
        // 底色不再由样式贴图给，改为在控件后面用白纹理 + drawer.Color 画（原先的纯色贴图）。
        _inputStyle.Normal.TextColor = new Color(0.95f, 0.95f, 1f);
        _inputStyle.Normal.Background = null;
        _inputStyle.Focused.TextColor = Color.White;
        _inputStyle.Focused.Background = null;
        _inputStyle.Padding.Left = 8;
        _inputStyle.Padding.Right = 8;
        _inputStyle.Padding.Top = 8;
        _inputStyle.Padding.Bottom = 10;

        _completionStyle = drawer.Skin.Label;
        _completionStyle.Font = font;
        _completionStyle.FontSize = fontSize - 2;
        _completionStyle.RichText = true;
        _completionStyle.Normal.TextColor = new Color(0.8f, 0.8f, 0.85f);
        _completionStyle.Normal.Background = null;
        _completionStyle.Padding.Left = 10;
        _completionStyle.Padding.Right = 10;
        _completionStyle.Padding.Top = 4;
        _completionStyle.Padding.Bottom = 4;
        _completionStyle.Margin.Left = 0;
        _completionStyle.Margin.Right = 0;
        _completionStyle.Margin.Top = 0;
        _completionStyle.Margin.Bottom = 0;

        _completionSelectedStyle = _completionStyle.Clone();
        _completionSelectedStyle.Normal.TextColor = Color.White;
        _completionSelectedStyle.Normal.Background = null;
        _completionSelectedStyle.FontStyle = ImguiFontStyle.Bold;

        _fontBtnStyle = drawer.Skin.Button;
        _fontBtnStyle.Font = font;
        _fontBtnStyle.FontSize = 10;
        _fontBtnStyle.Alignment = ImguiTextAnchor.MiddleCenter;
        _fontBtnStyle.Padding.Left = 0;
        _fontBtnStyle.Padding.Right = 0;
        _fontBtnStyle.Padding.Top = 0;
        _fontBtnStyle.Padding.Bottom = 0;
        _fontBtnStyle.Margin.Left = 0;
        _fontBtnStyle.Margin.Right = 0;
        _fontBtnStyle.Margin.Top = 0;
        _fontBtnStyle.Margin.Bottom = 0;
    }

    /// <summary>用框架的白纹理加 <see cref="IIMGUIDrawer.Color"/> 画一块纯色矩形，画完把颜色还原。</summary>
    private static void Fill(IIMGUIDrawer drawer, Rect rect, Color color)
    {
        var previous = drawer.Color;
        drawer.Color = color;
        drawer.DrawTexture(rect, drawer.WhiteTexture, ImguiScaleMode.StretchToFill, true);
        drawer.Color = previous;
    }

    #endregion

    // ====================================================================
    // Font size adjustment
    // ====================================================================
    private static void AdjustFontSize(IIMGUIDrawer drawer, int delta)
    {
        int current = ConfigManager.ConsoleFontSize.Value;
        int effective = current > 0
            ? current
            : Math.Clamp((int)drawer.ScreenSize.Y / 50, 14, 24);
        int newSize = Math.Clamp(effective + delta, 10, 36);
        ConfigManager.ConsoleFontSize.Value = newSize;
        ResetStyles();
    }

    // ====================================================================
    // OnGui — Minecraft-style bottom chat
    // ====================================================================
    public static void OnGui(IIMGUIDrawer drawer)
    {
        if (ConfigManager.ConsoleX.Value > drawer.ScreenSize.X * 0.95f || ConfigManager.ConsoleY.Value > drawer.ScreenSize.Y * 0.95f)
        {
            ConfigManager.ConsoleX.Value = (float)ConfigManager.ConsoleX.DefaultValue;
            ConfigManager.ConsoleY.Value = (float)ConfigManager.ConsoleY.DefaultValue;
        }

        InitStyles(drawer);

        // Drain pending logs only during the layout pass so that control count is stable
        // for the matching repaint pass. This also serializes cross-thread writes
        // (TCP receive thread enqueues; main thread dequeues).
        if (drawer.Current.Kind == ImguiEventKind.Layout)
            DrainPendingLogs();

        if (IsOpen)
            DrawOpenMode(drawer);
        else
            DrawPassiveMode(drawer);

        if (LiveModeManager.ShowUntrustedZoneOutline)
            DrawLiveUntrustedZoneOutline(drawer);
    }

    private static void DrainPendingLogs()
    {
        while (_pendingLogs.TryDequeue(out var msg))
        {
            _logs.Add(new LogEntry(msg));
            if (_logs.Count > MaxLogs) _logs.RemoveAt(0);
            _scrollToBottom = true;
        }
    }

    private static float GetEntryHeight(LogEntry entry, float width)
    {
        int fs = _logStyle!.FontSize;
        if (entry.CachedHeight > 0f && entry.CachedWidth == width && entry.CachedFontSize == fs)
            return entry.CachedHeight;
        entry.CachedHeight = _logStyle.CalcHeight(entry.Text, width);
        entry.CachedWidth = width;
        entry.CachedFontSize = fs;
        entry.CachedTextWidth = 0f; // invalidate passive bubble width too
        return entry.CachedHeight;
    }

    // ====================================================================
    // Passive mode: show recent messages with fade, no input
    // ====================================================================
    private static void DrawPassiveMode(IIMGUIDrawer drawer)
    {
        float now = ModRuntime.Clock.Now;
        float cutoff = PassiveLingerTime + PassiveFadeTime;

        // Backward scan to collect last N still-visible entries; avoids LINQ over full _logs.
        var visible = new List<LogEntry>(PassiveMaxLines);
        for (int i = _logs.Count - 1; i >= 0 && visible.Count < PassiveMaxLines; i--)
        {
            var e = _logs[i];
            float age = now - e.Timestamp;
            if (age >= cutoff) continue;
            visible.Add(e);
        }
        if (visible.Count == 0) return;
        visible.Reverse();

        // Use same panel position as open mode for alignment
        float panelW = ConfigManager.ConsoleWidth.Value;
        float panelX = ConfigManager.ConsoleX.Value;
        float logAreaH = ConfigManager.ConsoleHeight.Value;
        float panelBottomY = ConfigManager.ConsoleY.Value < 0
            ? drawer.ScreenSize.Y - BottomMargin
            : ConfigManager.ConsoleY.Value + logAreaH + InputHeight;
        float inputTopY = panelBottomY - InputHeight;
        float maxWidth = panelW - Padding * 2;

        float totalHeight = 0;
        for (int i = 0; i < visible.Count; i++)
            totalHeight += GetEntryHeight(visible[i], maxWidth);

        float currentY = inputTopY - totalHeight;

        for (int i = 0; i < visible.Count; i++)
        {
            var entry = visible[i];
            float age = now - entry.Timestamp;

            float alpha = age < PassiveLingerTime
                ? 1f
                : 1f - Math.Clamp((age - PassiveLingerTime) / PassiveFadeTime, 0f, 1f);

            float itemH = entry.CachedHeight;

            if (entry.CachedTextWidth <= 0f)
            {
                float tw = _logStyle!.CalcSize(StripRichText(entry.Text)).X + 16f;
                // 用 Min/Max 而不是 Math.Clamp：控制台被缩窄时上界会小于下界，Clamp 会抛。
                entry.CachedTextWidth = Math.Min(Math.Max(tw, 100f), maxWidth);
            }
            float textWidth = Math.Min(entry.CachedTextWidth, maxWidth);

            var prevColor = drawer.Color;
            Fill(drawer, new Rect(panelX + Padding, currentY, textWidth, itemH),
                new Color(0f, 0f, 0f, alpha * 0.85f * ShadowAlpha));

            drawer.Color = new Color(1f, 1f, 1f, alpha);
            drawer.Label(new Rect(panelX + Padding, currentY, maxWidth, itemH), entry.Text, _logStyle!);

            drawer.Color = prevColor;
            currentY += itemH;
        }
    }

    /// <summary>Strip IMGUI rich text tags for width measurement.</summary>
    private static string StripRichText(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
    }

    // ====================================================================
    // Open mode: full chat with scrollable history + input bar
    // ====================================================================
    private static void DrawOpenMode(IIMGUIDrawer drawer)
    {
        ImguiEvent e = drawer.Current;

        // ── Key handling ──
        if (e.Kind == ImguiEventKind.KeyDown && e.Key == ImguiKey.Escape)
        {
            if (_completion.IsActive)
                _completion.Dismiss();
            else
                IsOpen = false;
            e.Use();
            return;
        }

        if (e.Kind == ImguiEventKind.KeyDown && e.Key == ImguiKey.Tab)
        {
            if (_completion.HasCompletions)
            {
                var applied = _completion.TabCycle(reverse: e.Shift);
                if (applied != null)
                {
                    input = applied;
                    moveCursor = true;
                }
            }
            e.Use();
        }

        bool submit = false;
        if (e.Kind == ImguiEventKind.KeyDown && (e.Key == ImguiKey.Return || e.Key == ImguiKey.KeypadEnter))
        {
            submit = true;
            e.Use();
        }

        if (e.Kind == ImguiEventKind.KeyDown && e.Key == ImguiKey.UpArrow)
        {
            if (_completion.IsTabCycling)
                _completion.Dismiss();
            if (inputsCursor < inputs.Count)
            {
                inputsCursor++;
                if (inputs.Count > 0)
                {
                    input = inputs[^inputsCursor];
                    moveCursor = true;
                }
            }
            e.Use();
        }
        if (e.Kind == ImguiEventKind.KeyDown && e.Key == ImguiKey.DownArrow)
        {
            if (_completion.IsTabCycling)
                _completion.Dismiss();
            if (inputsCursor > 1)
            {
                inputsCursor--;
                input = inputs[^inputsCursor];
            }
            else
                input = "";
            moveCursor = true;
            e.Use();
        }

        if (justOpened && e.Kind == ImguiEventKind.KeyDown && e.Character == '/')
            e.Use();

        // ── Layout: config-based position and size ──
        float panelW = ConfigManager.ConsoleWidth.Value;
        float logAreaH = ConfigManager.ConsoleHeight.Value;
        float panelX = ConfigManager.ConsoleX.Value;
        // Auto-bottom when Y == -1
        float panelBottomY = ConfigManager.ConsoleY.Value < 0
            ? drawer.ScreenSize.Y - BottomMargin
            : ConfigManager.ConsoleY.Value + logAreaH + InputHeight;
        float inputY = panelBottomY - InputHeight;
        float logY = inputY - logAreaH;

        float totalH = logAreaH + InputHeight + DragHandleHeight;
        float dragY = logY - DragHandleHeight;
        bool lockConsoleUi = LiveModeManager.LockConsoleUi;

        // ── Drag handle ──
        var dragRect = new Rect(panelX, dragY, panelW, DragHandleHeight);
        Fill(drawer, dragRect, DragHandleColor);

        // ── Font size buttons (right side of drag handle) ──
        float fontBtnW = DragHandleHeight * 2f;
        float fontBtnH = DragHandleHeight;
        if (drawer.Button(new Rect(panelX + panelW - fontBtnW * 2 - 2, dragY, fontBtnW, fontBtnH), "A−", _fontBtnStyle!))
            AdjustFontSize(drawer, -2);
        if (drawer.Button(new Rect(panelX + panelW - fontBtnW, dragY, fontBtnW, fontBtnH), "A+", _fontBtnStyle!))
            AdjustFontSize(drawer, 2);

        // Drag logic
        if (!lockConsoleUi)
        {
            if (e.Kind == ImguiEventKind.MouseDown && dragRect.Contains(e.MousePosition))
            {
                _isDragging = true;
                _dragOffset = e.MousePosition - new Vector2(panelX, dragY);
                e.Use();
            }
            if (_isDragging)
            {
                if (e.Kind == ImguiEventKind.MouseDrag)
                {
                    float newX = e.MousePosition.X - _dragOffset.X;
                    float newTopY = e.MousePosition.Y - _dragOffset.Y;
                    ConfigManager.ConsoleX.Value = Math.Min(Math.Max(newX, 0f), drawer.ScreenSize.X - panelW);
                    ConfigManager.ConsoleY.Value = Math.Min(Math.Max(newTopY, 0f), drawer.ScreenSize.Y - totalH);
                    e.Use();
                }
                if (e.Kind == ImguiEventKind.MouseUp)
                {
                    _isDragging = false;
                    e.Use();
                }
            }
        }
        else if (_isDragging)
        {
            _isDragging = false;
        }

        // Background behind log area + input
        Fill(drawer, new Rect(panelX, logY, panelW, logAreaH + InputHeight), LogBgColor);

        // ── Resize handle (bottom-right corner) ──
        var resizeRect = new Rect(panelX + panelW - ResizeHandleSize, inputY + InputHeight - ResizeHandleSize,
            ResizeHandleSize, ResizeHandleSize);
        Fill(drawer, resizeRect, ResizeHandleColor);

        if (!lockConsoleUi)
        {
            if (e.Kind == ImguiEventKind.MouseDown && resizeRect.Contains(e.MousePosition))
            {
                _isResizing = true;
                _resizeStart = e.MousePosition;
                _resizeStartW = panelW;
                _resizeStartH = logAreaH;
                e.Use();
            }
            if (_isResizing)
            {
                if (e.Kind == ImguiEventKind.MouseDrag)
                {
                    float dw = e.MousePosition.X - _resizeStart.X;
                    float dh = e.MousePosition.Y - _resizeStart.Y; // down = taller (console grows upward)
                    ConfigManager.ConsoleWidth.Value = Math.Max(_resizeStartW + dw, MinPanelW);
                    ConfigManager.ConsoleHeight.Value = Math.Max(_resizeStartH + dh, MinPanelH);
                    e.Use();
                }
                if (e.Kind == ImguiEventKind.MouseUp)
                {
                    _isResizing = false;
                    e.Use();
                }
            }
        }
        else if (_isResizing)
        {
            _isResizing = false;
        }

        // Log area (scrollable, bottom-aligned) — virtualized to avoid laying out
        // thousands of labels per frame. Uses drawer.BeginScrollView with absolute rects
        // so control count is constant (1 scroll view + 0 inner GUILayout controls).
        var logViewRect = new Rect(panelX + Padding, logY, panelW - Padding * 2, logAreaH);
        float contentWidth = logViewRect.Width - 18f; // reserve scrollbar space

        int logCount = _logs.Count;
        float contentHeight = 0f;
        // Recompute heights (cached per entry/width/fontSize, so this is O(n) only when
        // width or font changes — and O(1) per entry otherwise).
        for (int i = 0; i < logCount; i++)
            contentHeight += GetEntryHeight(_logs[i], contentWidth);

        // Bottom-align: pad the top so content sits at the bottom of view when shorter.
        float topPad = Math.Max(0f, logAreaH - contentHeight);
        float totalContentH = contentHeight + topPad;

        var contentRect = new Rect(0, 0, contentWidth, totalContentH);
        scrollPosition = drawer.BeginScrollView(logViewRect, scrollPosition, contentRect);

        // Virtualize: only render entries whose rect intersects the visible viewport.
        float viewportTop = scrollPosition.Y;
        float viewportBottom = viewportTop + logAreaH;
        float y = topPad;
        for (int i = 0; i < logCount; i++)
        {
            float h = _logs[i].CachedHeight;
            if (y + h >= viewportTop && y <= viewportBottom)
                drawer.Label(new Rect(0, y, contentWidth, h), _logs[i].Text, _logStyle!);
            y += h;
            if (y > viewportBottom) break;
        }
        drawer.EndScrollView();

        // Auto-scroll to bottom
        if (_scrollToBottom && e.Kind == ImguiEventKind.Repaint)
        {
            scrollPosition.Y = Math.Max(0f, totalContentH - logAreaH);
            _scrollToBottom = false;
        }

        // Input bar background
        Fill(drawer, new Rect(panelX, inputY, panelW, InputHeight), InputBgColor);

        // Input field. Name based focus (SetNextControlName/FocusControl) is not part of this game
        // build, so the field is focused through the keyboard control id instead.
        string prevInput = input;
        input = drawer.TextField(new Rect(panelX + Padding, inputY, panelW - Padding * 2, InputHeight), input, _inputStyle!);

        if (input != prevInput)
            _completion.UpdateCompletions(input);

        if (focusTextField)
        {
            drawer.KeyboardControl = drawer.LastControlId;
            focusTextField = false;
        }

        if (moveCursor && e.Kind == ImguiEventKind.Repaint)
        {
            int id = drawer.KeyboardControl;
            var editor = drawer.GetStateObject<TextInputState>(id);
            if (editor != null)
            {
                editor.CursorIndex = input.Length;
                editor.SelectIndex = input.Length;
            }
            moveCursor = false;
        }

        // Completion dropdown (above input bar within panel bounds)
        if (_completion.IsActive)
            DrawCompletionDropdown(drawer, panelX + Padding, inputY, panelW - Padding * 2);

        // Submit
        if (submit)
        {
            if (!string.IsNullOrEmpty(input))
            {
                ExecuteCommand(input, out bool closeConsole);
                inputs.Add(input);
                inputsCursor = 0;
                input = "";
                _completion.Reset();
                _scrollToBottom = true;
                SaveHistory();
                if (closeConsole)
                {
                    IsOpen = false;
                    return;
                }
            }
            else
            {
                IsOpen = false;
                return;
            }
        }

        // Consume remaining key events
        if (e.Kind == ImguiEventKind.KeyDown && e.Key != ImguiKey.None)
            e.Use();
    }

    // ====================================================================
    // Completion dropdown (shared between modes)
    // ====================================================================
    private static void DrawCompletionDropdown(IIMGUIDrawer drawer, float x, float inputY, float width)
    {
        float itemHeight = (_completionStyle!.FontSize + 10);

        if (_completion.HasHint)
        {
            float hintHeight = itemHeight + 4;
            float hintY = inputY - hintHeight;
            Fill(drawer, new Rect(x, hintY, width, hintHeight), CompletionBgColor);

            var hintStyle = _completionStyle.Clone();
            hintStyle.FontStyle = ImguiFontStyle.Italic;
            hintStyle.Normal.TextColor = new Color(0.55f, 0.55f, 0.65f);
            drawer.Label(new Rect(x, hintY + 2, width, itemHeight),
                $"  {_completion.Hint}", hintStyle);
            return;
        }

        var completions = _completion.Completions;
        int totalCount = completions.Count;
        int maxVisible = Math.Min(totalCount, CompletionEngine.MaxVisibleItems);
        float dropdownHeight = maxVisible * itemHeight + 4;

        float dropY = inputY - dropdownHeight;
        Fill(drawer, new Rect(x, dropY, width, dropdownHeight), CompletionBgColor);

        int offset = _completion.ScrollOffset;
        for (int i = 0; i < maxVisible; i++)
        {
            int itemIndex = offset + i;
            if (itemIndex >= totalCount) break;

            bool isSelected = itemIndex == _completion.SelectedIndex;
            var style = isSelected ? _completionSelectedStyle : _completionStyle;
            float itemY = dropY + 2 + i * itemHeight;

            if (isSelected)
                Fill(drawer, new Rect(x, itemY, width, itemHeight), CompletionSelectedBgColor);

            string displayText = _completion.FormatWithHighlight(completions[itemIndex]);
            drawer.Label(new Rect(x, itemY, width, itemHeight), displayText, style!);
        }

        if (totalCount > maxVisible)
        {
            string indicator = $"[{offset + 1}-{Math.Min(offset + maxVisible, totalCount)}/{totalCount}]";
            var indicatorStyle = _completionStyle.Clone();
            indicatorStyle.Alignment = ImguiTextAnchor.MiddleRight;
            indicatorStyle.Normal.TextColor = new Color(0.5f, 0.5f, 0.6f);
            drawer.Label(new Rect(x, dropY + dropdownHeight - itemHeight, width - 8, itemHeight), indicator, indicatorStyle);
        }
    }

    // ====================================================================
    // Logging
    // ====================================================================
    public static void LogToConsole(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string stamped = $"<color=#888899>[{timestamp}]</color> {message}";
        // Thread-safe: enqueue and let OnGUI drain on the layout pass. This prevents
        // "Collection was modified" exceptions when callers (e.g. TCP receive thread)
        // log from off the main thread, and keeps GUILayout control counts stable
        // between Layout and Repaint events.
        _pendingLogs.Enqueue(stamped);
    }

    /// <summary>Replaces Notify.Show — log a gold event message (must be called on main thread).</summary>
    public static void ShowPassive(string text)
        => LogToConsole($"<color=#FFCC66>{text}</color>");

    /// <summary>Replaces Notify.ShowOnMainThread — dispatches to main thread, then logs gold event.</summary>
    public static void ShowPassiveFromAnyThread(string text)
        => PluginManager.RunOnMainThread(() => ShowPassive(text));

    /// <summary>Log a green success message.</summary>
    public static void LogSuccess(string text)
        => LogToConsole($"<color=#66FF88>{text}</color>");

    /// <summary>Log a red error message.</summary>
    public static void LogError(string text)
        => LogToConsole($"<color=#FF6666>{text}</color>");

    /// <summary>Log a prominent red alert (large bold text).</summary>
    public static void LogAlert(string text)
        => LogToConsole($"<size=22><color=#FF4444><b>{text}</b></color></size>");

    private static void ExecuteCommand(string cmd, out bool closeConsole)
    {
        closeConsole = false;
        Log.LogMessage($"Console Command: {cmd}");

        bool isMessage = cmd[0] != '/';
        if (isMessage)
        {
            if (GameSession.IsOnline)
                ChatMessage.Send(cmd);
            else
                LogToConsole($"{LiveModeManager.GetLocalDisplayName()}: {LiveModeManager.MaskMessage(cmd)}");

            closeConsole = true;
        }
        else
        {
            string commandInput = cmd[1..];
            closeConsole = CommandRegistry.Execute(commandInput, _consoleContext);
        }
    }

    private static void GetConsolePanelRect(IIMGUIDrawer drawer, out Rect panelRect)
    {
        float panelW = ConfigManager.ConsoleWidth.Value;
        float logAreaH = ConfigManager.ConsoleHeight.Value;
        float panelX = ConfigManager.ConsoleX.Value;
        float panelBottomY = ConfigManager.ConsoleY.Value < 0
            ? drawer.ScreenSize.Y - BottomMargin
            : ConfigManager.ConsoleY.Value + logAreaH + InputHeight;
        float inputY = panelBottomY - InputHeight;
        float logY = inputY - logAreaH;
        float dragY = logY - DragHandleHeight;
        float totalH = logAreaH + InputHeight + DragHandleHeight;
        panelRect = new Rect(panelX, dragY, panelW, totalH);
    }

    private static void DrawLiveUntrustedZoneOutline(IIMGUIDrawer drawer)
    {
        GetConsolePanelRect(drawer, out var rect);
        const float thickness = 3f;
        var color = new Color(1f, 0.85f, 0.1f, 0.95f);
        DrawRectOutline(drawer, rect, color, thickness);
    }

    private static void DrawRectOutline(IIMGUIDrawer drawer, Rect rect, Color color, float thickness)
    {
        Fill(drawer, new Rect(rect.X, rect.Y, rect.Width, thickness), color);
        Fill(drawer, new Rect(rect.X, rect.YMax - thickness, rect.Width, thickness), color);
        Fill(drawer, new Rect(rect.X, rect.Y, thickness, rect.Height), color);
        Fill(drawer, new Rect(rect.XMax - thickness, rect.Y, thickness, rect.Height), color);
    }
}
