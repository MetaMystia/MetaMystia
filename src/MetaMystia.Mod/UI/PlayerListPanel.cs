using UnityEngine;
using Common.UI;

namespace MetaMystia.UI;

/// <summary>
/// 左上角玩家列表面板，IMGUI 实现，可拖拽移动。
/// 当 InGameConsole 打开或 Enter 被按下时显示。
/// </summary>
[AutoLog]
public static partial class PlayerListPanel
{
    // ── 可见状态 ──
    private static bool _visible = false;

    // ── 样式缓存 ──
    private static bool _stylesInitialized = false;
    private static Font _font;
    private static GUIStyle _lineStyle;
    private static GUIStyle _fontBtnStyle;
    private static Texture2D _bgTexture;
    private static Texture2D _dragHandleTexture;

    public static void ResetStyles() => _stylesInitialized = false;

    // ── 拖拽 ──
    private static bool _isDragging = false;
    private static Vector2 _dragOffset;
    private const float DragHandleHeight = 10f;
    private const float Padding = 6f;
    private const float LinePadding = 2f;

    // ── 颜色 ──
    private static readonly Color HostColor = new(0.55f, 0.85f, 1f);   // 淡蓝
    private static readonly Color SelfColor = new(0.40f, 1f, 0.55f);   // 淡绿
    private static readonly Color PeerColor = new(0.85f, 0.85f, 0.85f); // 浅灰
    private static readonly Color DimColor = new(0.6f, 0.6f, 0.6f);    // 暗灰
    private static readonly Color ReadyColor = new(0.40f, 1f, 0.55f);
    private static readonly Color NotReadyColor = new(1f, 0.65f, 0.3f);

    // ====================================================================
    // Update (MonoBehaviour Update)
    // ====================================================================
    public static void Update()
    {
        // Enter 键切换显示（控制台打开时不响应，避免与控制台 Enter 冲突）
        if (!InGameConsole.IsOpen &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            _visible = !_visible;
    }

    // ====================================================================
    // OnGUI
    // ====================================================================
    public static void OnGUI()
    {
        if (!MpManager.IsRunning || (!_visible && !InGameConsole.IsOpen))
            return;

        InitStyles();

        Event e = Event.current;
        int fontSize = _lineStyle.fontSize;
        float lineH = fontSize + LinePadding * 2 + 2;

        // 收集要显示的行
        var lines = BuildLines();
        if (lines.Count == 0) return;

        // 计算面板宽度：取最宽行
        float maxW = 0f;
        foreach (var (text, _) in lines)
        {
            var content = new GUIContent(StripRichText(text));
            float w = _lineStyle.CalcSize(content).x + Padding * 2;
            if (w > maxW) maxW = w;
        }

        float panelW = Mathf.Max(maxW + Padding * 2, 200f);
        float panelH = lines.Count * lineH + DragHandleHeight + Padding * 2;
        float panelX = ConfigManager.PlayerListX.Value;
        float panelY = ConfigManager.PlayerListY.Value;

        // ── 拖拽手柄 ──
        var dragRect = new Rect(panelX, panelY, panelW, DragHandleHeight);
        GUI.DrawTexture(dragRect, _dragHandleTexture, ScaleMode.StretchToFill);

        // ── 字体大小按钮（拖拽手柄右侧）──
        float fontBtnW = DragHandleHeight * 2f;
        float fontBtnH = DragHandleHeight;
        if (GUI.Button(new Rect(panelX + panelW - fontBtnW * 2 - 2, panelY, fontBtnW, fontBtnH), "A−", _fontBtnStyle))
            AdjustFontSize(-2);
        if (GUI.Button(new Rect(panelX + panelW - fontBtnW, panelY, fontBtnW, fontBtnH), "A+", _fontBtnStyle))
            AdjustFontSize(2);

        if (e.type == EventType.MouseDown && dragRect.Contains(e.mousePosition))
        {
            _isDragging = true;
            _dragOffset = e.mousePosition - new Vector2(panelX, panelY);
            e.Use();
        }
        if (_isDragging)
        {
            if (e.type == EventType.MouseDrag)
            {
                float newX = e.mousePosition.x - _dragOffset.x;
                float newY = e.mousePosition.y - _dragOffset.y;
                ConfigManager.PlayerListX.Value = Mathf.Clamp(newX, 0, Screen.width - panelW);
                ConfigManager.PlayerListY.Value = Mathf.Clamp(newY, 0, Screen.height - panelH);
                e.Use();
            }
            if (e.type == EventType.MouseUp)
            {
                _isDragging = false;
                e.Use();
            }
        }

        // ── 背景 ──
        var bgRect = new Rect(panelX, panelY + DragHandleHeight, panelW, panelH - DragHandleHeight);
        GUI.DrawTexture(bgRect, _bgTexture, ScaleMode.StretchToFill);

        // ── 绘制行 ──
        float cy = panelY + DragHandleHeight + Padding;
        foreach (var (text, _) in lines)
        {
            GUI.Label(new Rect(panelX + Padding, cy, panelW - Padding * 2, lineH), text, _lineStyle);
            cy += lineH;
        }
    }

    // ====================================================================
    // 构建行内容
    // ====================================================================
    private static System.Collections.Generic.List<(string text, int uid)> BuildLines()
    {
        var lines = new System.Collections.Generic.List<(string, int)>();
        var scene = MpManager.LocalScene;

        // 仅在游戏场景中读取坐标/地图标签，避免在 MainScene 等场景中触发 GetCharacterUnit 警告
        bool needsGameplayData = scene is Scene.DayScene or Scene.WorkScene or Scene.IzakayaPrepScene;

        // 本地玩家 (UID=0 if host, else assigned)
        var local = PlayerManager.Local;
        string localLine = FormatPlayer(
            local.Uid, local.Id, scene,
            needsGameplayData ? PlayerManager.LocalMapLabel : MapLabel.Unknown,
            needsGameplayData ? local.Position : Vector2.zero,
            local.IsDayOver, local.IsPrepOver,
            local.IzakayaMapLabel, local.IzakayaLevel,
            isSelf: true, isHost: MpManager.IsServer);
        lines.Add((localLine, local.Uid));

        // Peers sorted by UID
        var sorted = new System.Collections.Generic.SortedDictionary<int, PeerPlayer>(PlayerManager.Peers);
        foreach (var kvp in sorted)
        {
            var peer = kvp.Value;
            string line = FormatPlayer(
                peer.Uid, peer.Id, scene,
                needsGameplayData ? peer.MapLabel : MapLabel.Unknown,
                needsGameplayData ? peer.Position : Vector2.zero,
                peer.IsDayOver, peer.IsPrepOver,
                peer.IzakayaMapLabel, peer.IzakayaLevel,
                isSelf: false, isHost: kvp.Key == MpManager.HOST_UID);
            lines.Add((line, kvp.Key));
        }

        var publicSorted = new System.Collections.Generic.SortedDictionary<int, PeerPlayer>(PlayerManager.PublicPeers);
        foreach (var kvp in publicSorted)
        {
            if (PlayerManager.Peers.ContainsKey(kvp.Key)) continue;
            var peer = kvp.Value;
            string line = FormatPlayer(
                peer.Uid, peer.Id, scene,
                needsGameplayData ? peer.MapLabel : MapLabel.Unknown,
                needsGameplayData ? peer.Position : Vector2.zero,
                peer.IsDayOver, peer.IsPrepOver,
                peer.IzakayaMapLabel, peer.IzakayaLevel,
                isSelf: false, isHost: false,
                scopeTag: "Online");
            lines.Add((line, kvp.Key));
        }

        return lines;
    }

    private static string FormatPlayer(
        int uid, string id, Scene scene,
        MapLabel mapLabel, Vector2 pos,
        bool isDayOver, bool isPrepOver,
        MapLabel izakayaMapLabel, int izakayaLevel,
        bool isSelf, bool isHost,
        string scopeTag = null)
    {
        // 名字颜色
        string nameColor;
        if (isSelf && isHost) nameColor = ColorToHex(HostColor); // 自己且是主机
        else if (isSelf) nameColor = ColorToHex(SelfColor);
        else if (isHost) nameColor = ColorToHex(HostColor);
        else nameColor = ColorToHex(PeerColor);

        string selfTag = isSelf ? " <color=#66FF88>★</color>" : "";
        string suffix = string.IsNullOrEmpty(scopeTag) ? "" : $" <color={ColorToHex(DimColor)}>{scopeTag}</color>";
        string displayId = LiveModeManager.GetDisplayName(uid);
        string name = LiveModeManager.IsActive
            ? $"<color={nameColor}>{displayId}</color>{selfTag}{suffix}"
            : $"<color={nameColor}>[{uid}] {id}</color>{selfTag}{suffix}";
        string dim = ColorToHex(DimColor);

        return scene switch
        {
            Scene.DayScene => FormatDayLine(name, dim, mapLabel, pos, isDayOver, izakayaMapLabel, izakayaLevel),
            Scene.IzakayaPrepScene => $"{name}  <color={dim}>{ReadyTag(isPrepOver)}</color>",
            Scene.WorkScene => $"{name}  <color={dim}>({pos.x:F2}, {pos.y:F2})</color>",
            _ => name
        };
    }

    /// <summary>
    /// DayScene: 全员 DayOver 后显示选店信息，否则显示地图+坐标+状态
    /// </summary>
    private static string FormatDayLine(string name, string dim,
        MapLabel mapLabel, Vector2 pos, bool isDayOver,
        MapLabel izakayaMapLabel, int izakayaLevel)
    {
        if (!PlayerManager.AllDayOver)
        {
            // 仍在白天探索
            return $"{name}  <color={dim}>{mapLabel.GetDisplayName()}  ({pos.x:F2}, {pos.y:F2})  {ReadyTag(isDayOver)}</color>";
        }
        // 全员进入选店
        string map = izakayaMapLabel.IsSelected()
            ? izakayaMapLabel.GetDisplayName() : "…";
        string level = izakayaLevel > 0 ? $" Lv.{izakayaLevel}" : "";
        return $"{name}  <color={dim}>{map}{level}</color>";
    }

    private static string ReadyTag(bool ready)
    {
        return ready
            ? $"<color={ColorToHex(ReadyColor)}>✓</color>"
            : $"<color={ColorToHex(NotReadyColor)}>…</color>";
    }

    private static string ColorToHex(Color c)
        => $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";

    private static string StripRichText(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

    // ====================================================================
    // 样式初始化
    // ====================================================================
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
        _dragHandleTexture = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.4f, 0.6f));

        int fontSize = ConfigManager.PlayerListFontSize.Value > 0
            ? ConfigManager.PlayerListFontSize.Value
            : Mathf.Clamp(Screen.height / 55, 12, 20);

        _lineStyle = new GUIStyle(GUI.skin.label)
        {
            font = font,
            fontSize = fontSize,
            richText = true,
            wordWrap = false,
            normal = { textColor = Color.white },
        };
        _lineStyle.padding.left = 4;
        _lineStyle.padding.right = 4;
        _lineStyle.padding.top = (int)LinePadding;
        _lineStyle.padding.bottom = (int)LinePadding;

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

    private static void AdjustFontSize(int delta)
    {
        int current = ConfigManager.PlayerListFontSize.Value;
        int effective = current > 0
            ? current
            : Mathf.Clamp(Screen.height / 55, 12, 20);
        int newSize = Mathf.Clamp(effective + delta, 10, 36);
        ConfigManager.PlayerListFontSize.Value = newSize;
        ResetStyles();
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
}
