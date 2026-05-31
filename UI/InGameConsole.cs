#nullable enable
using System;
using System.IO;
using Il2CppInterop.Runtime;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

using Common.UI;

using MetaMystia.ConsoleSystem;
using MetaMystia.Network;

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
        public float Timestamp; // Time.unscaledTime when added

        // Cached layout (invalidated when width/fontSize differ)
        public float CachedHeight;
        public float CachedWidth;
        public int CachedFontSize;
        public float CachedTextWidth; // for passive bubble bg sizing

        public LogEntry(string text)
        {
            Text = text;
            Timestamp = Time.unscaledTime;
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
    // Pending logs queued from any thread; drained only on EventType.Layout to keep
    // GUILayout control counts consistent between Layout and Repaint passes.
    private static readonly ConcurrentQueue<string> _pendingLogs = new();
    private static List<string> inputs = [];
    private static int inputsCursor = 0;
    private const int MaxLogs = 1024;
    private static int MaxHistorySize => ConfigManager.ConsoleHistorySize?.Value ?? 200;
    private static string HistoryFilePath => Path.Combine(
        BepInEx.Paths.ConfigPath, ConfigManager.ConsoleHistoryFile?.Value ?? "MetaMystia_console_history.txt");
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
    private const float PassiveLingerTime = 4f;   // seconds before fade starts
    private const float PassiveFadeTime = 1f;      // fade-out duration

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
    private static GUIStyle? _logStyle;
    private static GUIStyle? _inputStyle;
    private static GUIStyle? _completionStyle;
    private static GUIStyle? _completionSelectedStyle;
    private static GUIStyle? _fontBtnStyle;
    private static Texture2D? _bgTexture;
    private static Texture2D? _inputBgTexture;
    private static Texture2D? _completionBgTexture;
    private static Texture2D? _completionSelTexture;
    private static Texture2D? _shadowTexture;
    private static Texture2D? _dragHandleTexture;
    private static Texture2D? _resizeHandleTexture;
    private static bool _stylesInitialized = false;
    private static Font? _font;

    public static void ResetStyles() => _stylesInitialized = false;

    public static void Initialize()
    {
        _consoleContext = new ConsoleContext(LogToConsole);
        CommandRegistry.Initialize();
        LoadHistory();

        LogToConsole($"<color=#66CCFF>MetaMystia</color> <color=#888899>v{MyPluginInfo.PLUGIN_VERSION}</color>");
        LogDeferred(() => $"<color=#888899>{TextId.ConsoleStarPrompt.Get()}</color>");
        LogDeferred(() => $"<color=#888899>{TextId.ConsoleHelpHint.Get()}</color>");
    }

    private static void LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryFilePath))
            {
                var lines = File.ReadAllLines(HistoryFilePath);
                inputs.AddRange(lines);
                if (inputs.Count > MaxHistorySize)
                    inputs.RemoveRange(0, inputs.Count - MaxHistorySize);
            }
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
            File.WriteAllLines(HistoryFilePath, toSave);
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

        var eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            eventSystem.sendNavigationEvents = !IsOpen;
        }
    }

    public static void Update()
    {
        if (IsOpen)
        {
            var es = EventSystem.current;
            if (es != null && es.sendNavigationEvents)
                es.sendNavigationEvents = false;
        }

        if (justOpened) justOpened = false;

        if (Input.GetKeyDown(ConfigManager.KeyOpenCommand.Value) || Input.GetKeyDown(ConfigManager.KeyOpenChat.Value))
        {
            if (!IsOpen)
            {
                IsOpen = true;
                input = Input.GetKeyDown(ConfigManager.KeyOpenCommand.Value) ? "/" : "";
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

    private static Font GetFont()
    {
        if (_font != null) return _font;
        try
        {
            _font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 1);
            if (_font != null) return _font;
        }
        catch { /* fallback */ }
        return GUI.skin.font;
    }

    private static void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        var font = GetFont();

        _bgTexture = MakeTex(1, 1, new Color(0.05f, 0.05f, 0.08f, 0.55f));
        _inputBgTexture = MakeTex(1, 1, new Color(0.0f, 0.0f, 0.0f, 0.50f));
        _completionBgTexture = MakeTex(1, 1, new Color(0.10f, 0.10f, 0.15f, 0.92f));
        _completionSelTexture = MakeTex(1, 1, new Color(0.25f, 0.40f, 0.65f, 0.90f));
        _shadowTexture = MakeTex(1, 1, new Color(0f, 0f, 0f, 0.35f));
        _dragHandleTexture = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.4f, 0.6f));
        _resizeHandleTexture = MakeTex(1, 1, new Color(0.4f, 0.4f, 0.5f, 0.7f));

        int fontSize = ConfigManager.ConsoleFontSize.Value > 0
            ? ConfigManager.ConsoleFontSize.Value
            : Mathf.Clamp(Screen.height / 50, 14, 24);

        _logStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = fontSize,
            wordWrap = true,
            richText = true,
            normal = { textColor = Color.white },
        };
        _logStyle.padding.left = 6;
        _logStyle.padding.right = 6;
        _logStyle.padding.top = 2;
        _logStyle.padding.bottom = 2;

        _inputStyle = new GUIStyle(GUI.skin.textField)
        {
            font = font,
            fontSize = fontSize,
            richText = false,
            normal = { textColor = new Color(0.95f, 0.95f, 1f), background = _inputBgTexture },
            focused = { textColor = Color.white, background = _inputBgTexture },
        };
        _inputStyle.padding.left = 8;
        _inputStyle.padding.right = 8;
        _inputStyle.padding.top = 8;
        _inputStyle.padding.bottom = 10;

        _completionStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = fontSize - 2,
            richText = true,
            normal = { textColor = new Color(0.8f, 0.8f, 0.85f), background = _completionBgTexture },
        };
        _completionStyle.padding.left = 10;
        _completionStyle.padding.right = 10;
        _completionStyle.padding.top = 4;
        _completionStyle.padding.bottom = 4;
        _completionStyle.margin.left = 0;
        _completionStyle.margin.right = 0;
        _completionStyle.margin.top = 0;
        _completionStyle.margin.bottom = 0;

        _completionSelectedStyle = new GUIStyle(_completionStyle)
        {
            normal = { textColor = Color.white, background = _completionSelTexture },
            fontStyle = FontStyle.Bold
        };

        _fontBtnStyle = new GUIStyle(GUI.skin.button)
        {
            font = font,
            fontSize = 10,
            alignment = TextAnchor.MiddleCenter,
        };
        _fontBtnStyle.padding.left = 0;
        _fontBtnStyle.padding.right = 0;
        _fontBtnStyle.padding.top = 0;
        _fontBtnStyle.padding.bottom = 0;
        _fontBtnStyle.margin.left = 0;
        _fontBtnStyle.margin.right = 0;
        _fontBtnStyle.margin.top = 0;
        _fontBtnStyle.margin.bottom = 0;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var tex = new Texture2D(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                tex.SetPixel(x, y, col);
        tex.Apply();
        tex.hideFlags = HideFlags.HideAndDontSave;
        return tex;
    }

    #endregion

    // ====================================================================
    // Font size adjustment
    // ====================================================================
    private static void AdjustFontSize(int delta)
    {
        int current = ConfigManager.ConsoleFontSize.Value;
        int effective = current > 0
            ? current
            : Mathf.Clamp(Screen.height / 50, 14, 24);
        int newSize = Mathf.Clamp(effective + delta, 10, 36);
        ConfigManager.ConsoleFontSize.Value = newSize;
        ResetStyles();
    }

    // ====================================================================
    // OnGUI — Minecraft-style bottom chat
    // ====================================================================
    public static void OnGUI()
    {
        InitStyles();

        // Drain pending logs only during Layout pass so that control count is stable
        // for the matching Repaint pass. This also serializes cross-thread writes
        // (TCP receive thread enqueues; main thread dequeues).
        if (Event.current.type == EventType.Layout)
            DrainPendingLogs();

        if (IsOpen)
            DrawOpenMode();
        else
            DrawPassiveMode();
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
        int fs = _logStyle!.fontSize;
        if (entry.CachedHeight > 0f && entry.CachedWidth == width && entry.CachedFontSize == fs)
            return entry.CachedHeight;
        var content = new GUIContent(entry.Text);
        entry.CachedHeight = _logStyle.CalcHeight(content, width);
        entry.CachedWidth = width;
        entry.CachedFontSize = fs;
        entry.CachedTextWidth = 0f; // invalidate passive bubble width too
        return entry.CachedHeight;
    }

    // ====================================================================
    // Passive mode: show recent messages with fade, no input
    // ====================================================================
    private static void DrawPassiveMode()
    {
        float now = Time.unscaledTime;
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
            ? Screen.height - BottomMargin
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
                : 1f - Mathf.Clamp01((age - PassiveLingerTime) / PassiveFadeTime);

            float itemH = entry.CachedHeight;

            if (entry.CachedTextWidth <= 0f)
            {
                var strippedContent = new GUIContent(StripRichText(entry.Text));
                float tw = _logStyle!.CalcSize(strippedContent).x + 16f;
                entry.CachedTextWidth = Mathf.Clamp(tw, 100f, maxWidth);
            }
            float textWidth = Mathf.Min(entry.CachedTextWidth, maxWidth);

            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.85f);
            GUI.DrawTexture(new Rect(panelX + Padding, currentY, textWidth, itemH), _shadowTexture);

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(panelX + Padding, currentY, maxWidth, itemH), entry.Text, _logStyle);

            GUI.color = prevColor;
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
    private static void DrawOpenMode()
    {
        Event e = Event.current;

        // ── Key handling ──
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        {
            if (_completion.IsActive)
                _completion.Dismiss();
            else
                IsOpen = false;
            e.Use();
            return;
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
        {
            if (_completion.HasCompletions)
            {
                var applied = _completion.TabCycle(reverse: e.shift);
                if (applied != null)
                {
                    input = applied;
                    moveCursor = true;
                }
            }
            e.Use();
        }

        bool submit = false;
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
        {
            submit = true;
            e.Use();
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow)
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
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow)
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

        if (justOpened && e.type == EventType.KeyDown && e.character == '/')
            e.Use();

        // ── Layout: config-based position and size ──
        float panelW = ConfigManager.ConsoleWidth.Value;
        float logAreaH = ConfigManager.ConsoleHeight.Value;
        float panelX = ConfigManager.ConsoleX.Value;
        // Auto-bottom when Y == -1
        float panelBottomY = ConfigManager.ConsoleY.Value < 0
            ? Screen.height - BottomMargin
            : ConfigManager.ConsoleY.Value + logAreaH + InputHeight;
        float inputY = panelBottomY - InputHeight;
        float logY = inputY - logAreaH;

        float totalH = logAreaH + InputHeight + DragHandleHeight;
        float dragY = logY - DragHandleHeight;

        // ── Drag handle ──
        var dragRect = new Rect(panelX, dragY, panelW, DragHandleHeight);
        GUI.DrawTexture(dragRect, _dragHandleTexture, ScaleMode.StretchToFill);

        // ── Font size buttons (right side of drag handle) ──
        float fontBtnW = DragHandleHeight * 2f;
        float fontBtnH = DragHandleHeight;
        if (GUI.Button(new Rect(panelX + panelW - fontBtnW * 2 - 2, dragY, fontBtnW, fontBtnH), "A−", _fontBtnStyle))
            AdjustFontSize(-2);
        if (GUI.Button(new Rect(panelX + panelW - fontBtnW, dragY, fontBtnW, fontBtnH), "A+", _fontBtnStyle))
            AdjustFontSize(2);

        // Drag logic
        if (e.type == EventType.MouseDown && dragRect.Contains(e.mousePosition))
        {
            _isDragging = true;
            _dragOffset = e.mousePosition - new Vector2(panelX, dragY);
            e.Use();
        }
        if (_isDragging)
        {
            if (e.type == EventType.MouseDrag)
            {
                float newX = e.mousePosition.x - _dragOffset.x;
                float newTopY = e.mousePosition.y - _dragOffset.y;
                ConfigManager.ConsoleX.Value = Mathf.Clamp(newX, 0, Screen.width - panelW);
                ConfigManager.ConsoleY.Value = Mathf.Clamp(newTopY, 0, Screen.height - totalH);
                e.Use();
            }
            if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
                e.Use();
            }
        }

        // Background behind log area + input
        GUI.DrawTexture(new Rect(panelX, logY, panelW, logAreaH + InputHeight), _bgTexture, ScaleMode.StretchToFill);

        // ── Resize handle (bottom-right corner) ──
        var resizeRect = new Rect(panelX + panelW - ResizeHandleSize, inputY + InputHeight - ResizeHandleSize,
            ResizeHandleSize, ResizeHandleSize);
        GUI.DrawTexture(resizeRect, _resizeHandleTexture, ScaleMode.StretchToFill);

        if (e.type == EventType.MouseDown && resizeRect.Contains(e.mousePosition))
        {
            _isResizing = true;
            _resizeStart = e.mousePosition;
            _resizeStartW = panelW;
            _resizeStartH = logAreaH;
            e.Use();
        }
        if (_isResizing)
        {
            if (e.type == EventType.MouseDrag)
            {
                float dw = e.mousePosition.x - _resizeStart.x;
                float dh = e.mousePosition.y - _resizeStart.y; // down = taller (console grows upward)
                ConfigManager.ConsoleWidth.Value = Mathf.Max(_resizeStartW + dw, MinPanelW);
                ConfigManager.ConsoleHeight.Value = Mathf.Max(_resizeStartH + dh, MinPanelH);
                e.Use();
            }
            if (e.type == EventType.MouseUp)
            {
                _isResizing = false;
                e.Use();
            }
        }

        // Log area (scrollable, bottom-aligned) — virtualized to avoid laying out
        // thousands of labels per frame. Uses GUI.BeginScrollView with absolute rects
        // so control count is constant (1 scroll view + 0 inner GUILayout controls).
        var logViewRect = new Rect(panelX + Padding, logY, panelW - Padding * 2, logAreaH);
        float contentWidth = logViewRect.width - 18f; // reserve scrollbar space

        int logCount = _logs.Count;
        float contentHeight = 0f;
        // Recompute heights (cached per entry/width/fontSize, so this is O(n) only when
        // width or font changes — and O(1) per entry otherwise).
        for (int i = 0; i < logCount; i++)
            contentHeight += GetEntryHeight(_logs[i], contentWidth);

        // Bottom-align: pad the top so content sits at the bottom of view when shorter.
        float topPad = Mathf.Max(0f, logAreaH - contentHeight);
        float totalContentH = contentHeight + topPad;

        var contentRect = new Rect(0, 0, contentWidth, totalContentH);
        scrollPosition = GUI.BeginScrollView(logViewRect, scrollPosition, contentRect);

        // Virtualize: only render entries whose rect intersects the visible viewport.
        float viewportTop = scrollPosition.y;
        float viewportBottom = viewportTop + logAreaH;
        float y = topPad;
        for (int i = 0; i < logCount; i++)
        {
            float h = _logs[i].CachedHeight;
            if (y + h >= viewportTop && y <= viewportBottom)
                GUI.Label(new Rect(0, y, contentWidth, h), _logs[i].Text, _logStyle);
            y += h;
            if (y > viewportBottom) break;
        }
        GUI.EndScrollView();

        // Auto-scroll to bottom
        if (_scrollToBottom && e.type == EventType.Repaint)
        {
            scrollPosition.y = Mathf.Max(0f, totalContentH - logAreaH);
            _scrollToBottom = false;
        }

        // Input bar background
        GUI.DrawTexture(new Rect(panelX, inputY, panelW, InputHeight), _inputBgTexture, ScaleMode.StretchToFill);

        // Input field
        GUI.SetNextControlName(TextFieldControlName);
        string prevInput = input;
        input = GUI.TextField(new Rect(panelX + Padding, inputY, panelW - Padding * 2, InputHeight), input, _inputStyle);

        if (input != prevInput)
            _completion.UpdateCompletions(input);

        if (focusTextField)
        {
            GUI.FocusControl(TextFieldControlName);
            focusTextField = false;
        }

        if (moveCursor && e.type == EventType.Repaint)
        {
            int id = GUIUtility.keyboardControl;
            var obj = GUIUtility.GetStateObject(Il2CppType.Of<TextEditor>(), id);
            if (obj != null)
            {
                var editor = obj.Cast<TextEditor>();
                editor.cursorIndex = input.Length;
                editor.selectIndex = input.Length;
            }
            moveCursor = false;
        }

        // Completion dropdown (above input bar within panel bounds)
        if (_completion.IsActive)
            DrawCompletionDropdown(panelX + Padding, inputY, panelW - Padding * 2);

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
        if (e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
            e.Use();
    }

    // ====================================================================
    // Completion dropdown (shared between modes)
    // ====================================================================
    private static void DrawCompletionDropdown(float x, float inputY, float width)
    {
        float itemHeight = (_completionStyle!.fontSize + 10);

        if (_completion.HasHint)
        {
            float hintHeight = itemHeight + 4;
            float hintY = inputY - hintHeight;
            GUI.DrawTexture(new Rect(x, hintY, width, hintHeight), _completionBgTexture, ScaleMode.StretchToFill);

            var hintStyle = new GUIStyle(_completionStyle)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.55f, 0.55f, 0.65f) }
            };
            GUI.Label(new Rect(x, hintY + 2, width, itemHeight),
                $"  {_completion.Hint}", hintStyle);
            return;
        }

        var completions = _completion.Completions;
        int totalCount = completions.Count;
        int maxVisible = Math.Min(totalCount, CompletionEngine.MaxVisibleItems);
        float dropdownHeight = maxVisible * itemHeight + 4;

        float dropY = inputY - dropdownHeight;
        GUI.DrawTexture(new Rect(x, dropY, width, dropdownHeight), _completionBgTexture, ScaleMode.StretchToFill);

        int offset = _completion.ScrollOffset;
        for (int i = 0; i < maxVisible; i++)
        {
            int itemIndex = offset + i;
            if (itemIndex >= totalCount) break;

            bool isSelected = itemIndex == _completion.SelectedIndex;
            var style = isSelected ? _completionSelectedStyle : _completionStyle;
            float itemY = dropY + 2 + i * itemHeight;

            string displayText = _completion.FormatWithHighlight(completions[itemIndex]);
            GUI.Label(new Rect(x, itemY, width, itemHeight), displayText, style);
        }

        if (totalCount > maxVisible)
        {
            string indicator = $"[{offset + 1}-{Math.Min(offset + maxVisible, totalCount)}/{totalCount}]";
            var indicatorStyle = new GUIStyle(_completionStyle)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.5f, 0.5f, 0.6f) }
            };
            GUI.Label(new Rect(x, dropY + dropdownHeight - itemHeight, width - 8, itemHeight), indicator, indicatorStyle);
        }
    }

    // ====================================================================
    // Logging
    // ====================================================================
    public static void LogToConsole(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string stamped = $"<color=#888899>[{timestamp}]</color> {message}";
        // Thread-safe: enqueue and let OnGUI drain on the Layout pass. This prevents
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
        => PluginManager.Instance.RunOnMainThread(() => ShowPassive(text));

    /// <summary>Log a green success message.</summary>
    public static void LogSuccess(string text)
        => LogToConsole($"<color=#66FF88>{text}</color>");

    /// <summary>Log a red error message.</summary>
    public static void LogError(string text)
        => LogToConsole($"<color=#FF6666>{text}</color>");

    private static void ExecuteCommand(string cmd, out bool closeConsole)
    {
        closeConsole = false;
        Log.LogMessage($"Console Command: {cmd}");

        // Command echo (disabled by default, kept for future use)
        const bool ShowCommandEcho = false;
        if (ShowCommandEcho)
            LogToConsole(TextId.CommandPrompt.Get(cmd));

        bool isMessage = cmd[0] != '/';
        if (isMessage)
        {
            string localName = MpManager.PlayerId ?? "Player";
            LogToConsole($"{localName}: {cmd}");

            if (MpManager.IsConnected)
                MessageAction.Send(cmd);

            closeConsole = true;
        }
        else
        {
            string commandInput = cmd[1..];
            closeConsole = CommandRegistry.Execute(commandInput, _consoleContext);
        }
    }
}
