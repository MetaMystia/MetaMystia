#nullable enable
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using MetaMystia.ConsoleSystem;

namespace MetaMystia.UI;

/// <summary>
/// 控制台的 uGUI 视图。所有坐标以 1080p 为基准、原点在屏幕左下角，
/// 画布按屏幕高度缩放。鼠标命中检测在这里手工完成，不依赖 EventSystem。
/// </summary>
public static partial class InGameConsole
{
    // ── 配色 ──
    private static readonly Color PanelColor = new(0.075f, 0.075f, 0.095f, 0.88f);
    private static readonly Color InputColor = new(0f, 0f, 0f, 0.38f);
    private static readonly Color PopupColor = new(0.11f, 0.11f, 0.14f, 0.97f);
    private static readonly Color PassiveColor = new(0f, 0f, 0f, 0.55f);
    private static readonly Color Accent = new(0.88f, 0.64f, 0.35f, 1f);
    private static readonly Color TextColor = new(0.93f, 0.92f, 0.90f, 1f);
    private static readonly Color DimColor = new(0.56f, 0.58f, 0.64f, 1f);
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.06f);
    private static readonly Color OutlineColor = new(1f, 0.85f, 0.1f, 0.95f);
    private const string AccentHex = "#E0A458";

    private const float ReferenceHeight = 1080f;
    private const float MinPanelWidth = 360f;
    private const float RowPadX = 6f;

    // ── 布局（每帧计算，单位为基准像素） ──
    private static float _scale = 1f;
    private static Vector2 _refScreen;
    private static Rect PanelRect, TitleRect, GripRect, LogRect, InputRect, PopupRect, NewMessagesRect;
    private static float _pad, _titleH, _inputH, _promptW, _rowH;
    private static float LogViewHeight => LogRect.height;
    private static float LineHeight => _lineHeight > 0f ? _lineHeight : FontSize * 1.3f;
    private static float _lineHeight;
    private static int _measuredLineFont;

    // ── 对象 ──
    private static GameObject? _root;
    private static Canvas? _canvas;
    private static Sprite? _rounded;
    private static TMP_FontAsset? _font;
    private static int _appliedFont = -1;

    private static RectTransform _panel = null!, _passive = null!, _logViewport = null!, _inputViewport = null!;
    private static Image _panelBg = null!, _inputBg = null!, _hover = null!, _grip = null!, _pillBg = null!;
    private static Image _caret = null!, _selection = null!, _popupBg = null!, _popupSel = null!;
    private static TextMeshProUGUI _title = null!, _hint = null!, _prompt = null!, _inputText = null!, _placeholder = null!;
    private static TextMeshProUGUI _pill = null!, _signature = null!, _counter = null!, _measure = null!;
    private static readonly List<TextMeshProUGUI> _logRows = [];
    private static readonly List<(GameObject go, Image bg, TextMeshProUGUI text, CanvasGroup group)> _passiveRows = [];
    private static readonly List<(TextMeshProUGUI name, TextMeshProUGUI desc)> _popupRows = [];
    private static readonly Image[] _outline = new Image[4];
    private static readonly List<TextMeshProUGUI> _allTexts = [];

    // ── 每帧命中信息 ──
    private static readonly List<(Rect rect, LogEntry entry)> _visibleLogs = [];
    private static readonly List<(Rect rect, int index)> _visiblePopupRows = [];
    private static float _inputScroll;
    private static string _inputDisplay = "";
    private static LogEntry? _copiedEntry;
    private static float _copiedAt = -10f;

    // ====================================================================
    // Build
    // ====================================================================

    private static void EnsureView()
    {
        if (_root != null) return;
        _font = FloatingTextHelper.GetFont();
        _rounded = MakeRoundedSprite();
        _logRows.Clear();
        _passiveRows.Clear();
        _popupRows.Clear();
        _allTexts.Clear();

        _root = new GameObject("MetaMystiaConsole");
        UnityEngine.Object.DontDestroyOnLoad(_root);
        _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32000;
        var rootRect = _root.GetComponent<RectTransform>();

        _passive = NewRect("Passive", rootRect);

        _panel = NewRect("Panel", rootRect);
        _panelBg = NewImage("Background", _panel, PanelColor, 1.2f);
        _title = NewText("Title", _panel, TextColor, TextAlignmentOptions.MidlineLeft);
        _title.text = $"<b><color={AccentHex}>MetaMystia</color></b>";
        _hint = NewText("Hint", _panel, DimColor, TextAlignmentOptions.MidlineRight);
        _grip = NewImage("Grip", _panel, new Color(1f, 1f, 1f, 0.18f), 3f);

        _logViewport = NewRect("Logs", _panel);
        _logViewport.gameObject.AddComponent<RectMask2D>();
        _hover = NewImage("Hover", _logViewport, HoverColor, 3f);

        _pillBg = NewImage("NewMessages", _panel, Accent, 3f);
        _pill = NewText("Label", _pillBg.rectTransform, new Color(0.1f, 0.08f, 0.05f, 1f), TextAlignmentOptions.Center);
        Stretch(_pill.rectTransform);

        _inputBg = NewImage("Input", _panel, InputColor, 1.6f);
        _prompt = NewText("Prompt", _panel, Accent, TextAlignmentOptions.Center);
        _prompt.text = "›";
        _inputViewport = NewRect("InputViewport", _panel);
        _inputViewport.gameObject.AddComponent<RectMask2D>();
        _selection = NewImage("Selection", _inputViewport, new Color(Accent.r, Accent.g, Accent.b, 0.35f), 0f);
        _inputText = NewText("Text", _inputViewport, TextColor, TextAlignmentOptions.MidlineLeft);
        _placeholder = NewText("Placeholder", _inputViewport, DimColor, TextAlignmentOptions.MidlineLeft);
        _placeholder.fontStyle = FontStyles.Italic;
        _caret = NewImage("Caret", _inputViewport, Accent, 0f);

        _popupBg = NewImage("Popup", _panel, PopupColor, 1.6f);
        _popupSel = NewImage("Selected", _popupBg.rectTransform, new Color(Accent.r, Accent.g, Accent.b, 0.22f), 3f);
        _signature = NewText("Signature", _popupBg.rectTransform, TextColor, TextAlignmentOptions.MidlineLeft);
        _counter = NewText("Counter", _popupBg.rectTransform, DimColor, TextAlignmentOptions.MidlineRight);

        for (int i = 0; i < _outline.Length; i++)
            _outline[i] = NewImage("Outline", rootRect, OutlineColor, 0f);

        _measure = NewText("Measure", rootRect, Color.clear, TextAlignmentOptions.TopLeft);
        _measure.enableWordWrapping = true;
        Place(_measure, new Rect(-10000f, -10000f, 1f, 1f));

        _appliedFont = -1;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        return rect;
    }

    private static Image NewImage(string name, Transform parent, Color color, float roundness)
    {
        var image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (roundness > 0f && _rounded != null)
        {
            image.sprite = _rounded;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = roundness;
        }
        return image;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, Color color, TextAlignmentOptions alignment)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null) text.font = _font;
        text.color = color;
        text.alignment = alignment;
        text.richText = true;
        text.parseCtrlCharacters = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
        _allTexts.Add(text);
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Place(Component c, Rect r) => Place((RectTransform)c.transform, r);

    private static void Place(RectTransform rect, Rect r)
    {
        rect.anchoredPosition = r.position;
        rect.sizeDelta = r.size;
    }

    private static void SetActive(Component c, bool active)
    {
        var go = c.gameObject;
        if (go.activeSelf != active) go.SetActive(active);
    }

    /// <summary>圆角矩形贴图，九宫格拉伸。</summary>
    private static Sprite MakeRoundedSprite()
    {
        const int size = 32;
        const float radius = 10f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave,
        };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
            }
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // ====================================================================
    // Layout
    // ====================================================================

    private static void ComputeLayout()
    {
        _scale = Mathf.Max(0.5f, Screen.height / ReferenceHeight);
        _refScreen = new Vector2(Screen.width / _scale, Screen.height / _scale);

        int fs = FontSize;
        if (_measuredLineFont != fs) MeasureLineHeight(fs);
        _pad = Mathf.Round(fs * 0.5f);
        _titleH = Mathf.Round(fs * 1.6f);
        _inputH = Mathf.Round(fs * 2.1f);
        _promptW = Mathf.Round(fs * 1.4f);
        _rowH = Mathf.Round(LineHeight + fs * 0.35f);

        float minH = _titleH + _inputH + _pad * 2 + LineHeight * 3;
        float w = Mathf.Clamp(ConfigManager.ConsoleWidth.Value, MinPanelWidth, Mathf.Max(MinPanelWidth, _refScreen.x));
        float h = Mathf.Clamp(ConfigManager.ConsoleHeight.Value, minH, Mathf.Max(minH, _refScreen.y));
        float left = Mathf.Clamp(ConfigManager.ConsoleLeft.Value, 0f, Mathf.Max(0f, _refScreen.x - w));
        float bottom = Mathf.Clamp(ConfigManager.ConsoleBottom.Value, 0f, Mathf.Max(0f, _refScreen.y - h));

        PanelRect = new Rect(left, bottom, w, h);
        TitleRect = new Rect(left, bottom + h - _titleH, w, _titleH);
        GripRect = new Rect(left + w - _titleH, TitleRect.y, _titleH, _titleH);
        InputRect = new Rect(left + _pad, bottom + _pad, w - _pad * 2, _inputH);
        float logBottom = InputRect.yMax + _pad * 0.5f;
        LogRect = new Rect(left + _pad, logBottom, w - _pad * 2, TitleRect.y - logBottom);
    }

    private static void MeasureLineHeight(int fs)
    {
        _measuredLineFont = fs;
        if (_measure == null) return;
        _measure.fontSize = fs;
        _lineHeight = _measure.GetPreferredValues("Ag国", 10000f, 0f).y;
    }

    private static Vector2 MouseRef() => (Vector2)Input.mousePosition / _scale;

    private static void ApplyFontSize()
    {
        int fs = FontSize;
        if (_appliedFont == fs) return;
        _appliedFont = fs;
        foreach (var t in _allTexts) t.fontSize = fs;
        _hint.fontSize = _counter.fontSize = _pill.fontSize = Mathf.Max(MinFontSize - 2, fs - 4);
    }

    // ====================================================================
    // Measurement
    // ====================================================================

    private static float LogTextWidth => LogRect.width - RowPadX * 2;

    private static float MeasureLog(LogEntry entry)
    {
        if (_measure == null || LogRect.width <= 0f) return LineHeight;
        float width = LogTextWidth;
        int fs = FontSize;
        if (entry.MeasuredFont == fs && Mathf.Approximately(entry.MeasuredWidth, width))
            return entry.Height;
        _measure.fontSize = fs;
        entry.Height = Mathf.Ceil(_measure.GetPreferredValues(entry.Display, width, 0f).y);
        entry.MeasuredWidth = width;
        entry.MeasuredFont = fs;
        return entry.Height;
    }

    private static float EstimatedLogHeight(LogEntry entry) =>
        entry.MeasuredFont == FontSize && Mathf.Approximately(entry.MeasuredWidth, LogTextWidth) ? entry.Height : LineHeight;

    private static float MaxScroll()
    {
        float total = 0f;
        foreach (var e in _logs) total += EstimatedLogHeight(e);
        return Mathf.Max(0f, total - LogRect.height);
    }

    // ====================================================================
    // Render
    // ====================================================================

    private static void Render()
    {
        ComputeLayout();
        _canvas!.scaleFactor = _scale;
        ApplyFontSize();

        SetActive(_panel, IsOpen);
        SetActive(_passive, !IsOpen);
        if (IsOpen) RenderPanel();
        else RenderPassive();
        RenderOutline();
    }

    private static void RenderPanel()
    {
        Place(_panel, PanelRect);
        var origin = PanelRect.position;
        Rect Local(Rect r) => new(r.position - origin, r.size);

        Place(_panelBg, new Rect(0, 0, PanelRect.width, PanelRect.height));

        bool locked = LiveModeManager.LockConsoleUi;
        Place(_title, Local(new Rect(TitleRect.x + _pad + 2, TitleRect.y, TitleRect.width * 0.4f, TitleRect.height)));
        _hint.text = Time.unscaledTime - _copiedAt < 1.5f ? TextId.ConsoleCopied.Get() : TextId.ConsoleTitleHint.Get();
        Place(_hint, Local(new Rect(TitleRect.x, TitleRect.y, TitleRect.width - (locked ? _pad : GripRect.width), TitleRect.height)));
        SetActive(_grip, !locked);
        float gripSize = _titleH * 0.36f;
        Place(_grip, Local(new Rect(GripRect.center.x - gripSize / 2, GripRect.center.y - gripSize / 2, gripSize, gripSize)));

        RenderLogs();
        RenderInput();
        RenderPopup();
    }

    private static void RenderLogs()
    {
        Place(_logViewport, new Rect(LogRect.position - PanelRect.position, LogRect.size));
        _scroll = Mathf.Clamp(_scroll, 0f, MaxScroll());
        if (_scroll <= 0.5f) _unread = 0;

        _visibleLogs.Clear();
        float viewH = LogRect.height;
        float width = LogTextWidth;
        float y = -_scroll;
        int used = 0;
        for (int i = _logs.Count - 1; i >= 0 && y < viewH; i--)
        {
            var entry = _logs[i];
            float h = MeasureLog(entry);
            if (y + h > 0f)
            {
                var row = LogRow(used++);
                row.text = entry.Display;
                Place(row, new Rect(RowPadX, y, width, h));
                _visibleLogs.Add((new Rect(LogRect.x, LogRect.y + y, LogRect.width, h), entry));
            }
            y += h;
        }
        for (int i = 0; i < _logRows.Count; i++) SetActive(_logRows[i], i < used);

        // 悬停 / 复制反馈
        var mouse = MouseRef();
        Rect? highlight = null;
        bool flashing = false;
        foreach (var (rect, entry) in _visibleLogs)
        {
            if (entry == _copiedEntry && Time.unscaledTime - _copiedAt < 0.35f)
            {
                highlight = rect;
                flashing = true;
                break;
            }
            if (_drag == DragMode.None && !PopupRect.Contains(mouse) && rect.Contains(mouse) && LogRect.Contains(mouse))
                highlight = rect;
        }
        SetActive(_hover, highlight.HasValue);
        if (highlight.HasValue)
        {
            var r = highlight.Value;
            _hover.color = flashing ? new Color(Accent.r, Accent.g, Accent.b, 0.25f) : HoverColor;
            Place(_hover, new Rect(r.x - LogRect.x, r.y - LogRect.y, r.width, r.height));
        }

        SetActive(_pillBg, _unread > 0);
        if (_unread > 0)
        {
            _pill.text = TextId.ConsoleNewMessages.Get(_unread);
            float w = _pill.GetPreferredValues(_pill.text, 10000f, 0f).x + _pad * 2;
            float h = Mathf.Round(FontSize * 1.4f);
            NewMessagesRect = new Rect(LogRect.xMax - w - 4, LogRect.y + 4, w, h);
            Place(_pillBg, new Rect(NewMessagesRect.position - PanelRect.position, NewMessagesRect.size));
        }
        else NewMessagesRect = default;
    }

    private static TextMeshProUGUI LogRow(int index)
    {
        while (_logRows.Count <= index)
        {
            var row = NewText("Line", _logViewport, TextColor, TextAlignmentOptions.TopLeft);
            row.enableWordWrapping = true;
            row.fontSize = FontSize;
            _logRows.Add(row);
        }
        return _logRows[index];
    }

    private static void RenderInput()
    {
        var origin = PanelRect.position;
        Place(_inputBg, new Rect(InputRect.position - origin, InputRect.size));
        Place(_prompt, new Rect(InputRect.x - origin.x, InputRect.y - origin.y, _promptW, InputRect.height));

        var view = new Rect(InputRect.x + _promptW, InputRect.y, InputRect.width - _promptW - _pad, InputRect.height);
        Place(_inputViewport, new Rect(view.position - origin, view.size));

        string text = _editor.Text;
        string comp = Input.compositionString ?? "";
        int caret = _editor.Caret;
        string display = comp.Length > 0
            ? ConsoleText.Escape(text[..caret]) + "<u>" + ConsoleText.Escape(comp) + "</u>" + ConsoleText.Escape(text[caret..])
            : ConsoleText.Escape(text);
        if (display != _inputDisplay)
        {
            _inputDisplay = display;
            _inputText.text = display;
            _inputText.ForceMeshUpdate();
        }

        SetActive(_placeholder, text.Length == 0 && comp.Length == 0);
        _placeholder.text = TextId.ConsolePlaceholder.Get();
        Place(_placeholder, new Rect(0, 0, view.width, view.height));

        float caretX = CharX(Units(text, 0, caret) + Units(comp, 0, comp.Length));
        if (caretX - _inputScroll > view.width - 4f) _inputScroll = caretX - view.width + 4f;
        if (caretX < _inputScroll) _inputScroll = Mathf.Max(0f, caretX - view.width / 3f);
        if (text.Length == 0) _inputScroll = 0f;
        Place(_inputText, new Rect(-_inputScroll, 0, 100000f, view.height));

        bool selection = _editor.HasSelection && comp.Length == 0;
        SetActive(_selection, selection);
        float caretH = Mathf.Min(view.height - 4f, LineHeight * 1.05f);
        float caretY = (view.height - caretH) / 2f;
        if (selection)
        {
            float x1 = CharX(Units(text, 0, _editor.SelectionStart)) - _inputScroll;
            float x2 = CharX(Units(text, 0, _editor.SelectionEnd)) - _inputScroll;
            Place(_selection, new Rect(x1, caretY, Mathf.Max(1f, x2 - x1), caretH));
        }

        bool blinkOn = (Time.unscaledTime - _lastEditTime) % 1f < 0.55f;
        SetActive(_caret, blinkOn);
        Place(_caret, new Rect(caretX - _inputScroll, caretY, 2f, caretH));

        Input.compositionCursorPos = new Vector2((view.x + caretX - _inputScroll) * _scale, Screen.height - view.y * _scale);
    }

    /// <summary>输入框中第 unit 个字符前的 x 坐标（TMP 局部坐标）。</summary>
    private static float CharX(int unit)
    {
        var info = _inputText.textInfo;
        int count = info?.characterCount ?? 0;
        if (count == 0) return 0f;
        var chars = info!.characterInfo;
        return unit < count ? chars[unit].origin : chars[count - 1].xAdvance;
    }

    /// <summary>字符串区间内的字符数，代理对算一个（与 TMP characterInfo 对齐）。</summary>
    private static int Units(string s, int start, int end)
    {
        int n = 0;
        for (int i = start; i < end; i++)
        {
            if (i + 1 < end && char.IsSurrogatePair(s[i], s[i + 1])) i++;
            n++;
        }
        return n;
    }

    private static int CaretIndexAt(float mouseX)
    {
        if (!string.IsNullOrEmpty(Input.compositionString)) return _editor.Caret;
        string text = _editor.Text;
        float local = mouseX - (InputRect.x + _promptW) + _inputScroll;
        int count = Units(text, 0, text.Length);
        int best = 0;
        float bestDist = float.MaxValue;
        for (int u = 0; u <= count; u++)
        {
            float d = Mathf.Abs(CharX(u) - local);
            if (d < bestDist) { bestDist = d; best = u; }
        }
        int index = 0;
        for (int u = 0; u < best && index < text.Length; u++)
            index += index + 1 < text.Length && char.IsSurrogatePair(text[index], text[index + 1]) ? 2 : 1;
        return index;
    }

    private static void RenderPopup()
    {
        _visiblePopupRows.Clear();
        bool rows = _completion.PopupVisible;
        bool sig = _completion.SignatureVisible;
        SetActive(_popupBg, rows || sig);
        if (!rows && !sig)
        {
            PopupRect = default;
            return;
        }

        var items = _completion.Items;
        int visible = rows ? Math.Min(items.Count - _completion.ScrollOffset, CompletionEngine.MaxVisible) : 0;
        bool paged = rows && items.Count > CompletionEngine.MaxVisible;
        bool header = sig || paged;
        float inner = 4f;
        float height = visible * _rowH + (header ? _rowH : 0f) + inner * 2;
        PopupRect = new Rect(InputRect.x, InputRect.yMax + 4f, InputRect.width, height);
        Place(_popupBg, new Rect(PopupRect.position - PanelRect.position, PopupRect.size));

        float rowW = PopupRect.width - inner * 2;
        float top = height - inner;

        SetActive(_signature, sig);
        SetActive(_counter, paged);
        if (header)
        {
            top -= _rowH;
            _signature.text = _completion.Signature ?? "";
            Place(_signature, new Rect(inner + _pad, top, rowW - _pad * 2, _rowH));
            _counter.text = $"{_completion.Selected + 1}/{items.Count}";
            Place(_counter, new Rect(inner, top, rowW - _pad, _rowH));
        }

        float nameW = Mathf.Round(rowW * 0.4f);
        string token = _completion.Token;
        SetActive(_popupSel, false);
        for (int i = 0; i < visible; i++)
        {
            int index = _completion.ScrollOffset + i;
            var item = items[index];
            var (name, desc) = PopupRow(i);
            float y = top - (i + 1) * _rowH;
            var rowRect = new Rect(inner, y, rowW, _rowH);

            name.text = HighlightPrefix(item.Value, token);
            desc.text = ConsoleText.Escape(item.Description);
            Place(name, new Rect(inner + _pad, y, nameW - _pad, _rowH));
            Place(desc, new Rect(inner + nameW, y, rowW - nameW - _pad, _rowH));
            _visiblePopupRows.Add((new Rect(PopupRect.position + rowRect.position, rowRect.size), index));

            if (index == _completion.Selected)
            {
                SetActive(_popupSel, true);
                Place(_popupSel, rowRect);
            }
        }
        for (int i = 0; i < _popupRows.Count; i++)
        {
            SetActive(_popupRows[i].name, i < visible);
            SetActive(_popupRows[i].desc, i < visible);
        }
    }

    private static (TextMeshProUGUI name, TextMeshProUGUI desc) PopupRow(int index)
    {
        while (_popupRows.Count <= index)
        {
            var parent = _popupBg.rectTransform;
            var name = NewText("Name", parent, TextColor, TextAlignmentOptions.MidlineLeft);
            var desc = NewText("Desc", parent, DimColor, TextAlignmentOptions.MidlineLeft);
            name.overflowMode = desc.overflowMode = TextOverflowModes.Ellipsis;
            name.fontSize = desc.fontSize = FontSize;
            _popupRows.Add((name, desc));
        }
        return _popupRows[index];
    }

    private static string HighlightPrefix(string value, string prefix)
    {
        if (prefix.Length == 0 || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return ConsoleText.Escape(value);
        return $"<color={AccentHex}>{ConsoleText.Escape(value[..prefix.Length])}</color>{ConsoleText.Escape(value[prefix.Length..])}";
    }

    private static void RenderPassive()
    {
        float now = Time.unscaledTime;
        float linger = ConfigManager.ConsolePassiveLingerTime.Value;
        float fade = Mathf.Max(0.01f, ConfigManager.ConsolePassiveFadeTime.Value);
        float maxW = LogRect.width;
        int fs = FontSize;

        Place(_passive, new Rect(LogRect.x, LogRect.y, maxW, LogRect.height));
        float y = 0f;
        int used = 0;
        for (int i = _logs.Count - 1; i >= 0 && used < PassiveMaxLines; i--)
        {
            var entry = _logs[i];
            float age = now - entry.Created;
            if (age >= linger + fade) break;

            if (entry.PassiveFont != fs || !Mathf.Approximately(entry.PassiveMaxWidth, maxW))
            {
                _measure.fontSize = fs;
                var size = _measure.GetPreferredValues(entry.Display, maxW - RowPadX * 2, 0f);
                entry.PassiveSize = new Vector2(Mathf.Min(Mathf.Ceil(size.x) + RowPadX * 2, maxW), Mathf.Ceil(size.y));
                entry.PassiveFont = fs;
                entry.PassiveMaxWidth = maxW;
            }

            var row = PassiveRow(used++);
            row.text.text = entry.Display;
            row.group.alpha = age < linger ? 1f : 1f - Mathf.Clamp01((age - linger) / fade);
            Place(row.go.GetComponent<RectTransform>(), new Rect(0, y, entry.PassiveSize.x, entry.PassiveSize.y));
            Place(row.text, new Rect(RowPadX, 0, maxW - RowPadX * 2, entry.PassiveSize.y));
            y += entry.PassiveSize.y + 2f;
        }
        for (int i = 0; i < _passiveRows.Count; i++)
            if (_passiveRows[i].go.activeSelf != i < used) _passiveRows[i].go.SetActive(i < used);
    }

    private static (GameObject go, Image bg, TextMeshProUGUI text, CanvasGroup group) PassiveRow(int index)
    {
        while (_passiveRows.Count <= index)
        {
            var bg = NewImage("Bubble", _passive, PassiveColor, 3f);
            var group = bg.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var text = NewText("Text", bg.rectTransform, TextColor, TextAlignmentOptions.TopLeft);
            text.enableWordWrapping = true;
            text.fontSize = FontSize;
            _passiveRows.Add((bg.gameObject, bg, text, group));
        }
        return _passiveRows[index];
    }

    private static void RenderOutline()
    {
        bool show = LiveModeManager.ShowUntrustedZoneOutline;
        const float t = 3f;
        var r = PanelRect;
        var sides = new[]
        {
            new Rect(r.x, r.yMax - t, r.width, t), new Rect(r.x, r.y, r.width, t),
            new Rect(r.x, r.y, t, r.height), new Rect(r.xMax - t, r.y, t, r.height),
        };
        for (int i = 0; i < _outline.Length; i++)
        {
            SetActive(_outline[i], show);
            if (show) Place(_outline[i], sides[i]);
        }
    }

    // ====================================================================
    // Hit tests
    // ====================================================================

    private static bool TryPopupRowAt(Vector2 mouse, out int index)
    {
        foreach (var (rect, i) in _visiblePopupRows)
            if (rect.Contains(mouse)) { index = i; return true; }
        index = -1;
        return false;
    }

    private static bool TryLogAt(Vector2 mouse, out LogEntry entry)
    {
        if (LogRect.Contains(mouse) && !PopupRect.Contains(mouse))
            foreach (var (rect, e) in _visibleLogs)
                if (rect.Contains(mouse)) { entry = e; return true; }
        entry = null!;
        return false;
    }

    private static void FlashCopied(LogEntry entry)
    {
        _copiedEntry = entry;
        _copiedAt = Time.unscaledTime;
    }
}
