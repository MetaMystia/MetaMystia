#nullable enable
using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

using MetaMystia.ConsoleSystem;
using MetaMystia.Network;

namespace MetaMystia.UI;

public static partial class InGameConsole
{
    private enum DragMode { None, Move, Resize, Scrollbar }

    private sealed class PopupRow
    {
        public RectTransform Rect = null!;
        public Image Background = null!;
        public TextMeshProUGUI Text = null!;
    }

    private const float HeaderHeight = 30f;
    private const float Pad = 8f;
    private const float Radius = 10f;
    private const float RowGap = 4f;
    private const float RowInset = 4f;
    private const float ScrollbarSpace = 10f;
    private const float DefaultBottom = 64f;
    private const float MinWidth = 360f;
    private const float MinLogHeight = 100f;
    private const float EdgeGrab = 6f;
    private const float CornerGrab = 28f;
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.06f);

    private static RectTransform? _root;
    private static GameObject _blocker = null!;
    private static GameObject _panel = null!;
    private static RectTransform _header = null!;
    private static RectTransform _fontDown = null!;
    private static RectTransform _fontUp = null!;
    private static TextMeshProUGUI _hints = null!;
    private static RectTransform _viewport = null!;
    private static RectTransform _content = null!;
    private static RectTransform _scrollbar = null!;
    private static RectTransform _unreadPill = null!;
    private static TextMeshProUGUI _unreadLabel = null!;
    private static RectTransform _inputBar = null!;
    private static Image _stripe = null!;
    private static TMP_InputField _field = null!;
    private static TextMeshProUGUI _placeholder = null!;
    private static RectTransform _popup = null!;
    private static GameObject _outline = null!;
    private static readonly List<PopupRow> _popupRows = [];
    private static bool _fadeFull = true;
    private static UnityAction<string> _onSubmit = null!;
    private static UnityAction<string> _onEndEdit = null!;

    private static string _text = "";
    private static bool _caretToEnd;
    private static int _caretRestore = -1;
    private static int _caretBefore;
    private static bool _layoutDirty = true;
    private static bool _popupDirty;
    private static float _scroll;
    private static float _contentHeight;
    private static float _viewHeight;
    private static bool _follow = true;
    private static int _unread;
    private static DragMode _drag;
    private static Vector2 _dragOffset;
    private static Vector2 _dragStart;
    private static Vector2 _resizeAxes;
    private static int _popupHover = -1;
    private static int _unreadShown;

    private static Vector2 _requested;
    private static Vector2 _position;
    private static float _width;
    private static float _logHeight;
    private static float _fontSize;
    private static Vector2 _screen;
    private static float _rowWidth;

    private const float DefaultFontSize = 20f;
    private static float FontSize => ConfigManager.ConsoleFontSize.Value > 0 ? ConfigManager.ConsoleFontSize.Value : DefaultFontSize;
    private static float InputHeight => Mathf.Round(_fontSize * 1.9f);
    private static float TotalHeight => Pad + InputHeight + Pad + _logHeight + HeaderHeight;

    private static bool EnsureView()
    {
        if (_root != null) return true;
        if (!OverlayUi.EnsureCreated(requireGameFont: !_deferredFlushed)) return false;
        Build();
        return true;
    }

    private static void Build()
    {
        _fontSize = FontSize;
        _blocker = OverlayUi.Box("ConsoleBlocker", OverlayUi.Root, Color.clear, raycast: true).gameObject;
        OverlayUi.Fill(_blocker.GetComponent<RectTransform>(), 0, 0, 0, 0);

        _root = OverlayUi.Node("Console", OverlayUi.Root);
        _root.gameObject.SetActive(false);

        var panel = OverlayUi.Box("Panel", _root, OverlayUi.Surface, Radius, raycast: true);
        _panel = panel.gameObject;
        OverlayUi.Fill(panel.rectTransform, 0, 0, 0, 0);

        _header = OverlayUi.Node("Header", _panel.transform);
        _header.anchorMin = new Vector2(0, 1);
        _header.anchorMax = Vector2.one;
        _header.pivot = new Vector2(0.5f, 1);
        _header.sizeDelta = new Vector2(0, HeaderHeight);
        var title = OverlayUi.Label("Title", _header, 14f, OverlayUi.Dim);
        title.text = "MetaMystia";
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.enableWordWrapping = false;
        OverlayUi.Fill(title.rectTransform, 12, 0, 30, 0);
        _hints = OverlayUi.Label("Hints", _header, 13f, new Color(OverlayUi.Dim.r, OverlayUi.Dim.g, OverlayUi.Dim.b, 0.75f));
        _hints.alignment = TextAlignmentOptions.MidlineRight;
        _hints.enableWordWrapping = false;
        _hints.overflowMode = TextOverflowModes.Ellipsis;
        OverlayUi.Fill(_hints.rectTransform, 110, 0, 36 + 2 * OverlayUi.FontButtonWidth, 0);
        _fontDown = OverlayUi.FontButton(_header, "A−", -32 - OverlayUi.FontButtonWidth);
        _fontUp = OverlayUi.FontButton(_header, "A+", -32);
        var grip = OverlayUi.Node("Grip", _header);
        OverlayUi.Place(grip, Vector2.one, new Vector2(-4, -4), new Vector2(22, 22));
        for (int row = 0; row < 3; row++)
            for (int column = row; column < 3; column++)
            {
                var dot = OverlayUi.Box("Dot", grip, OverlayUi.Dim);
                OverlayUi.Place(dot.rectTransform, Vector2.one, new Vector2(-4 - (2 - column) * 5, -4 - row * 5), new Vector2(2.5f, 2.5f));
            }

        // 日志区不属于面板，关闭时仍在原位置显示最近的消息。
        _viewport = OverlayUi.Node("Viewport", _root);
        _viewport.gameObject.AddComponent<RectMask2D>();
        _content = OverlayUi.Node("Content", _viewport);
        _content.anchorMin = _content.anchorMax = _content.pivot = new Vector2(0, 1);
        _scrollbar = OverlayUi.Box("Scrollbar", _viewport, new Color(1, 1, 1, 0.22f), 2f).rectTransform;
        var pill = OverlayUi.Box("Unread", _viewport, OverlayUi.Accent, 11f);
        _unreadPill = pill.rectTransform;
        _unreadLabel = OverlayUi.Label("Label", _unreadPill, 14f, new Color(0.12f, 0.1f, 0.08f));
        _unreadLabel.alignment = TextAlignmentOptions.Center;
        _unreadLabel.enableWordWrapping = false;
        OverlayUi.Fill(_unreadLabel.rectTransform, 0, 0, 0, 0);

        _inputBar = OverlayUi.Box("Input", _panel.transform, OverlayUi.Field, 7f).rectTransform;
        _stripe = OverlayUi.Box("Stripe", _inputBar, OverlayUi.Dim, 1.5f);
        var area = OverlayUi.Node("TextArea", _inputBar);
        OverlayUi.Fill(area, 18, 2, 10, 2);
        area.gameObject.AddComponent<RectMask2D>();
        _placeholder = OverlayUi.Label("Placeholder", area, _fontSize, OverlayUi.Dim);
        _placeholder.fontStyle = FontStyles.Italic;
        var text = OverlayUi.Label("Text", area, _fontSize, OverlayUi.Text);
        foreach (var label in new[] { _placeholder, text })
        {
            label.enableWordWrapping = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            OverlayUi.Fill(label.rectTransform, 0, 0, 0, 0);
        }
        _placeholder.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = false;
        _field = _inputBar.gameObject.AddComponent<TMP_InputField>();
        _field.transition = Selectable.Transition.None;
        _field.textViewport = area;
        _field.textComponent = text;
        _field.placeholder = _placeholder;
        _field.fontAsset = OverlayUi.Font;
        _field.pointSize = _fontSize;
        _field.lineType = TMP_InputField.LineType.SingleLine;
        _field.characterLimit = ChatPayload.MaxLength;
        _field.richText = false;
        _field.onFocusSelectAll = false;
        _field.resetOnDeActivation = false;
        _field.restoreOriginalTextOnEscape = false;
        _field.customCaretColor = true;
        _field.caretColor = OverlayUi.Accent;
        _field.caretWidth = 2;
        _field.selectionColor = new Color(OverlayUi.Accent.r, OverlayUi.Accent.g, OverlayUi.Accent.b, 0.35f);
        _onSubmit = DelegateSupport.ConvertDelegate<UnityAction<string>>(new Action<string>(OnSubmit))!;
        _onEndEdit = DelegateSupport.ConvertDelegate<UnityAction<string>>(new Action<string>(OnEndEdit))!;
        _field.onSubmit.AddListener(_onSubmit);
        _field.onEndEdit.AddListener(_onEndEdit);

        _popup = OverlayUi.Box("Popup", _root, OverlayUi.Raised, 8f).rectTransform;
        for (int i = 0; i < CompletionEngine.MaxVisible + 2; i++)
        {
            var background = OverlayUi.Box("Row", _popup, Color.clear, 5f);
            var label = OverlayUi.Label("Text", background.rectTransform, _fontSize, OverlayUi.Text);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            OverlayUi.Fill(label.rectTransform, 10, 0, 10, 0);
            _popupRows.Add(new PopupRow { Rect = background.rectTransform, Background = background, Text = label });
        }

        _outline = OverlayUi.Node("Outline", _root).gameObject;
        OverlayUi.Fill(_outline.GetComponent<RectTransform>(), 0, 0, 0, 0);
        foreach (var (min, max, offsetMin, offsetMax) in new[]
        {
            (new Vector2(0, 1), Vector2.one, new Vector2(-3, 0), new Vector2(3, 3)),
            (Vector2.zero, new Vector2(1, 0), new Vector2(-3, -3), new Vector2(3, 0)),
            (Vector2.zero, new Vector2(0, 1), new Vector2(-3, 0), new Vector2(0, 0)),
            (new Vector2(1, 0), Vector2.one, new Vector2(0, 0), new Vector2(3, 0)),
        })
        {
            var edge = OverlayUi.Box("Edge", _outline.transform, OverlayUi.Outline).rectTransform;
            edge.anchorMin = min;
            edge.anchorMax = max;
            edge.offsetMin = offsetMin;
            edge.offsetMax = offsetMax;
        }

        _root.gameObject.SetActive(true);
        ShowOpen(false);
        foreach (var entry in _entries)
            UpdateRow(entry);
        ApplyLayout(force: true);
    }

    private static void ShowOpen(bool open)
    {
        _blocker.SetActive(open);
        _panel.SetActive(open);
        _popup.gameObject.SetActive(false);
        _fadeFull = true;
        if (!open) return;
        _placeholder.text = TextId.ConsoleInputPlaceholder.Get();
        _hints.text = TextId.ConsoleKeyHints.Get();
        _layoutDirty = true;
    }

    private static void UpdateRow(Entry entry)
    {
        entry.Height = 0f;
        if (_root == null) return;
        if (entry.Row == null)
        {
            // 底色只在关闭时显示。
            entry.Bubble = OverlayUi.Box("Line", _content, _open ? Color.clear : OverlayUi.Bubble, 4f);
            entry.Row = entry.Bubble.rectTransform;
            entry.Row.anchorMin = entry.Row.anchorMax = entry.Row.pivot = new Vector2(0, 1);
            entry.Group = entry.Bubble.gameObject.AddComponent<CanvasGroup>();
            entry.Group.blocksRaycasts = false;
            entry.Text = OverlayUi.Label("Text", entry.Row, _fontSize, OverlayUi.Text);
            var rect = entry.Text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(RowInset, -(RowGap - 1f) / 2f);
        }
        entry.Text.text = RowText(entry);
    }

    // ── 布局 ──

    private static void UpdateView()
    {
        ApplyLayout(force: false);
        if (_layoutDirty) LayoutRows();
        ApplyScroll();
        UpdateFade();
        if (_open)
        {
            if (_popupDirty) RenderPopup();
            _stripe.color = _text.StartsWith('/') ? OverlayUi.CommandColor : new Color(OverlayUi.Dim.r, OverlayUi.Dim.g, OverlayUi.Dim.b, 0.5f);
        }
        _outline.SetActive(LiveModeManager.ShowUntrustedZoneOutline);
    }

    private static void ApplyLayout(bool force)
    {
        var requested = _drag == DragMode.Move ? _position
            : new Vector2(ConfigManager.ConsoleX.Value, ConfigManager.ConsoleY.Value < 0 ? DefaultBottom : ConfigManager.ConsoleY.Value);
        var screen = OverlayUi.Size;
        float fontSize = FontSize;
        float chrome = Pad + Mathf.Round(fontSize * 1.9f) + Pad + HeaderHeight;
        float width = Mathf.Clamp(ConfigManager.ConsoleWidth.Value, MinWidth, screen.x);
        float logHeight = Mathf.Clamp(ConfigManager.ConsoleHeight.Value, MinLogHeight, Mathf.Max(MinLogHeight, screen.y - chrome));
        if (!force && requested == _requested && width == _width && logHeight == _logHeight && fontSize == _fontSize && screen == _screen)
            return;

        bool reflow = force || width != _width || fontSize != _fontSize;
        _requested = requested;
        _width = width;
        _logHeight = logHeight;
        _fontSize = fontSize;
        _screen = screen;
        _position = OverlayUi.Clamp(Vector2.zero, requested, new Vector2(_width, TotalHeight));
        OverlayUi.Place(_root!, Vector2.zero, _position, new Vector2(_width, TotalHeight));

        OverlayUi.Fill(_viewport, Pad, Pad + InputHeight + Pad, Pad, HeaderHeight);
        _viewHeight = _logHeight;
        _inputBar.anchorMin = Vector2.zero;
        _inputBar.anchorMax = new Vector2(1, 0);
        _inputBar.pivot = new Vector2(0.5f, 0);
        _inputBar.sizeDelta = new Vector2(-2 * Pad, InputHeight);
        _inputBar.anchoredPosition = new Vector2(0, Pad);
        OverlayUi.Place(_stripe.rectTransform, new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(3, InputHeight - 16));
        _field.pointSize = _fontSize;
        _rowWidth = _width - 2 * Pad - ScrollbarSpace - RowInset;

        if (reflow)
        {
            foreach (var entry in _entries)
            {
                entry.Height = 0f;
                if (entry.Row != null) entry.Text.fontSize = _fontSize;
            }
            foreach (var row in _popupRows) row.Text.fontSize = _fontSize;
        }
        _layoutDirty = true;
        _popupDirty = true;
    }

    private static void LayoutRows()
    {
        _layoutDirty = false;
        float y = 0f;
        foreach (var entry in _entries)
        {
            if (entry.Row == null) continue;
            if (entry.Height <= 0f)
            {
                var size = OverlayUi.Measure(entry.Text.text, _fontSize, _rowWidth);
                entry.Height = size.y;
                entry.Width = Mathf.Min(size.x, _rowWidth);
            }
            entry.Top = y;
            // 底色比文字多出半个行距，相邻两行之间留 1 像素。
            entry.Row.anchoredPosition = new Vector2(0, -y);
            entry.Row.sizeDelta = new Vector2(entry.Width + 2 * RowInset, entry.Height + RowGap - 1f);
            entry.Text.rectTransform.sizeDelta = new Vector2(_rowWidth, entry.Height);
            y += entry.Height + RowGap;
        }
        _contentHeight = Mathf.Max(0f, y - 1f);
        _content.sizeDelta = new Vector2(_rowWidth + RowInset, _contentHeight);
    }

    private static float MaxScroll => Mathf.Max(0f, _contentHeight - _viewHeight);

    private static void ApplyScroll()
    {
        if (_follow)
        {
            _scroll = MaxScroll;
            _unread = 0;
        }
        _scroll = Mathf.Clamp(_scroll, 0f, MaxScroll);
        _content.anchoredPosition = new Vector2(0, _contentHeight <= _viewHeight ? _contentHeight - _viewHeight : _scroll);

        bool scrollable = _contentHeight > _viewHeight;
        _scrollbar.gameObject.SetActive(_open && scrollable);
        if (scrollable)
        {
            float handle = Mathf.Max(24f, _viewHeight * _viewHeight / _contentHeight);
            float top = (_viewHeight - handle) * (_scroll / MaxScroll);
            OverlayUi.Place(_scrollbar, Vector2.one, new Vector2(-3, -top), new Vector2(4, handle));
        }

        _unreadPill.gameObject.SetActive(_open && _unread > 0);
        if (_unread > 0 && _unread != _unreadShown)
        {
            _unreadShown = _unread;
            _unreadLabel.text = TextId.ConsoleNewMessages.Get(_unread);
            float width = OverlayUi.Measure(_unreadLabel.text, 14f, 1000f, wrap: false).x + 24f;
            OverlayUi.Place(_unreadPill, new Vector2(1, 0), new Vector2(-ScrollbarSpace - 4, 6), new Vector2(width, 22));
        }
    }

    private static void ScrollToLatest()
    {
        _follow = true;
        _unread = 0;
    }

    private static void ScrollBy(float delta)
    {
        _scroll = Mathf.Clamp(_scroll + delta, 0f, MaxScroll);
        _follow = _scroll >= MaxScroll - 1f;
        if (_follow) _unread = 0;
    }

    // ── 关闭时的消息 ──

    /// <summary>关闭时只显示最近的消息并渐隐，位置与打开时一致。</summary>
    private static void UpdateFade()
    {
        float now = Time.unscaledTime;
        float linger = ConfigManager.ConsolePassiveLingerTime.Value;
        float fade = Mathf.Max(0.01f, ConfigManager.ConsolePassiveFadeTime.Value);
        bool full = _fadeFull;
        _fadeFull = false;
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (entry.Row == null) continue;
            float alpha = _open ? 1f : Mathf.Clamp01(1f - (now - entry.Shown - linger) / fade);
            if (full) entry.Bubble.color = _open ? Color.clear : OverlayUi.Bubble;
            // 更早的消息已是同样的透明度。
            else if (alpha == entry.Alpha && (_open || alpha == 0f)) break;
            if (alpha == entry.Alpha) continue;
            entry.Alpha = alpha;
            entry.Group.alpha = alpha;
        }
    }

    // ── 补全弹层 ──

    private static void RenderPopup()
    {
        _popupDirty = false;
        if (!_open || !_completion.IsVisible)
        {
            _popup.gameObject.SetActive(false);
            return;
        }

        var lines = new List<(string Text, bool Selected)>();
        if (_completion.Usage != null) lines.Add((_completion.Usage, false));
        var items = _completion.HasItems ? _completion.Items : [];
        int visible = Math.Min(items.Count, CompletionEngine.MaxVisible);
        for (int i = 0; i < visible; i++)
        {
            int index = _completion.Offset + i;
            lines.Add((ItemText(items[index]), index == _completion.Selected));
        }
        bool footer = items.Count > visible;
        if (footer)
            lines.Add((ConsoleFormat.Dim($"<size=80%>{_completion.Offset + 1}–{_completion.Offset + visible} / {items.Count}</size>"), false));

        float rowHeight = Mathf.Round(_fontSize * 1.55f);
        float width = 0f;
        foreach (var line in lines)
            width = Mathf.Max(width, OverlayUi.Measure(line.Text, _fontSize, 4000f, wrap: false).x);
        width = Mathf.Clamp(width + 28f, 180f, _width - 2 * Pad);
        float height = lines.Count * rowHeight + 8f;

        for (int i = 0; i < _popupRows.Count; i++)
        {
            var row = _popupRows[i];
            row.Rect.gameObject.SetActive(i < lines.Count);
            if (i >= lines.Count) continue;
            row.Text.text = lines[i].Text;
            int item = i - (_completion.Usage != null ? 1 : 0);
            row.Background.color = lines[i].Selected ? OverlayUi.Selection
                : item >= 0 && item < visible && item == _popupHover ? HoverColor : Color.clear;
            row.Text.alignment = footer && i == lines.Count - 1 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
            OverlayUi.Place(row.Rect, new Vector2(0, 1), new Vector2(4, -4 - i * rowHeight), new Vector2(width - 8, rowHeight));
        }

        float tokenX = OverlayUi.Measure(_text[.._completion.Start], _fontSize, 4000f, wrap: false).x;
        float x = Mathf.Clamp(Pad + 18f + tokenX - 10f, Pad, _width - Pad - width);
        OverlayUi.Place(_popup, Vector2.zero, new Vector2(x, Pad + InputHeight + 4f), new Vector2(width, height));
        _popup.gameObject.SetActive(true);
    }

    private static string ItemText(Suggestion item)
    {
        string partial = _completion.Partial;
        string label = partial.Length > 0 && item.Label.StartsWith(partial, StringComparison.OrdinalIgnoreCase)
            ? $"<color={OverlayUi.Hex(OverlayUi.Accent)}><b>{ConsoleFormat.Plain(item.Label[..partial.Length])}</b></color>{ConsoleFormat.Plain(item.Label[partial.Length..])}"
            : ConsoleFormat.Plain(item.Label);
        return string.IsNullOrEmpty(item.Detail) ? label : $"{label}   {ConsoleFormat.Dim("<size=85%>" + item.Detail + "</size>")}";
    }

    // ── 指针 ──

    private static void HandlePointer()
    {
        int hover = HoveredItem();
        if (hover != _popupHover)
        {
            _popupHover = hover;
            _popupDirty = true;
        }
        if (Input.GetMouseButtonDown(0)) PointerDown();
        if (_drag != DragMode.None)
        {
            if (Input.GetMouseButton(0)) Drag();
            else EndDrag();
        }
        float wheel = Input.mouseScrollDelta.y;
        if (wheel == 0f) return;
        if (_popup.gameObject.activeSelf && _completion.HasItems && OverlayUi.IsPointerOver(_popup))
        {
            _completion.Scroll(wheel > 0f ? -1 : 1);
            _popupDirty = true;
        }
        else if (OverlayUi.IsPointerOver(_viewport))
        {
            ScrollBy(-wheel * _fontSize * 3f);
        }
    }

    /// <summary>指针所在的候选在可见行中的序号，没有时为 -1。</summary>
    private static int HoveredItem()
    {
        if (!_popup.gameObject.activeSelf || !_completion.HasItems) return -1;
        int first = _completion.Usage != null ? 1 : 0;
        int visible = Math.Min(_completion.Items.Count, CompletionEngine.MaxVisible);
        for (int i = 0; i < visible; i++)
            if (OverlayUi.IsPointerOver(_popupRows[first + i].Rect)) return i;
        return -1;
    }

    private static void PointerDown()
    {
        if (_popupHover >= 0)
        {
            SetText(_completion.Accept(_completion.Offset + _popupHover), refresh: false);
            return;
        }
        if (_unreadPill.gameObject.activeSelf && OverlayUi.IsPointerOver(_unreadPill))
        {
            ScrollToLatest();
            return;
        }
        if (_scrollbar.gameObject.activeSelf && OverlayUi.IsPointerOver(_scrollbar))
        {
            _drag = DragMode.Scrollbar;
            _dragOffset = new Vector2(0, _scroll);
            _dragStart = OverlayUi.Pointer(Vector2.zero);
            return;
        }
        if (LiveModeManager.LockConsoleUi) return;
        if (OverlayUi.IsPointerOver(_fontDown) || OverlayUi.IsPointerOver(_fontUp))
        {
            OverlayUi.AdjustFontSize(ConfigManager.ConsoleFontSize, (int)DefaultFontSize, OverlayUi.IsPointerOver(_fontUp) ? 2 : -2);
            return;
        }
        var pointer = OverlayUi.Pointer(Vector2.zero);
        var size = new Vector2(_width, TotalHeight);
        // 右上角（面板以左下角定位）：指针到右边缘、上边缘的距离，面板外为负。
        var toEdge = _position + size - pointer;
        if (toEdge.x >= -EdgeGrab && toEdge.y >= -EdgeGrab && pointer.x >= _position.x && pointer.y >= _position.y)
        {
            _resizeAxes = toEdge.x <= CornerGrab && toEdge.y <= CornerGrab ? Vector2.one
                : new Vector2(toEdge.x <= EdgeGrab ? 1f : 0f, toEdge.y <= EdgeGrab ? 1f : 0f);
            if (_resizeAxes != Vector2.zero)
            {
                _drag = DragMode.Resize;
                _dragOffset = toEdge;
                return;
            }
        }
        if (OverlayUi.IsPointerOver(_header)) _drag = DragMode.Move;
        _dragOffset = _position - pointer;
    }

    private static void Drag()
    {
        var pointer = OverlayUi.Pointer(Vector2.zero);
        switch (_drag)
        {
            case DragMode.Move:
                _position = OverlayUi.Clamp(Vector2.zero, pointer + _dragOffset, new Vector2(_width, TotalHeight));
                _root!.anchoredPosition = _position;
                break;
            case DragMode.Resize:
                // 保持按下时指针与边缘的距离，按下瞬间尺寸不变。
                var screen = OverlayUi.Size;
                float chrome = TotalHeight - _logHeight;
                var target = pointer + _dragOffset - _position;
                ConfigManager.Config.SaveOnConfigSet = false;
                if (_resizeAxes.x > 0f)
                    ConfigManager.ConsoleWidth.Value = Mathf.Round(Mathf.Clamp(target.x, MinWidth, screen.x - _position.x));
                if (_resizeAxes.y > 0f)
                    ConfigManager.ConsoleHeight.Value = Mathf.Round(Mathf.Clamp(target.y - chrome, MinLogHeight, screen.y - _position.y - chrome));
                break;
            case DragMode.Scrollbar:
                float handle = Mathf.Max(24f, _viewHeight * _viewHeight / _contentHeight);
                float ratio = MaxScroll / Mathf.Max(1f, _viewHeight - handle);
                ScrollBy(_dragOffset.y - (pointer.y - _dragStart.y) * ratio - _scroll);
                break;
        }
    }

    private static void EndDrag()
    {
        if (_drag == DragMode.Move)
        {
            ConfigManager.Config.SaveOnConfigSet = false;
            ConfigManager.ConsoleX.Value = Mathf.Round(_position.x);
            ConfigManager.ConsoleY.Value = Mathf.Round(_position.y);
        }
        if (_drag is DragMode.Move or DragMode.Resize)
        {
            ConfigManager.Config.SaveOnConfigSet = true;
            ConfigManager.Config.Save();
        }
        _drag = DragMode.None;
    }
}
