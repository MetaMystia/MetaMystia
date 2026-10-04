using System;

using Mystia;
using Mystia.Imgui;
using Mystia.Scenes;

using Common.UI;

using MetaMystia.Multiplayer;

using Color = Mystia.Numerics.Color;
using Rect = Mystia.Numerics.Rect;
using Vector2 = Mystia.Numerics.Vector2;

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
    private static FontHandle? _font;
    private static TextStyleHandle? _lineStyle;
    private static TextStyleHandle? _fontBtnStyle;

    // 面板底色：框架只给一张白纹理，颜色由 drawer.Color 乘上去（原先各造一张 1x1 贴图）。
    private static readonly Color BgColor = new(0.05f, 0.05f, 0.08f, 0.55f);
    private static readonly Color DragHandleColor = new(0.3f, 0.3f, 0.4f, 0.6f);

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
            (ModRuntime.Input.IsKeyDown(MystiaKey.Return) || ModRuntime.Input.IsKeyDown(MystiaKey.KeypadEnter)))
            _visible = !_visible;
    }

    // ====================================================================
    // OnGui
    // ====================================================================
    public static void OnGui(IIMGUIDrawer drawer)
    {
        if (!GameSession.IsConnectingOrOnline || (!_visible && !InGameConsole.IsOpen))
            return;

        if (ConfigManager.PlayerListX.Value > drawer.ScreenSize.X * 0.95f || ConfigManager.PlayerListY.Value > drawer.ScreenSize.Y * 0.95f)
        {
            ConfigManager.PlayerListX.Value = (float)ConfigManager.PlayerListX.DefaultValue;
            ConfigManager.PlayerListY.Value = (float)ConfigManager.PlayerListY.DefaultValue;
        }

        InitStyles(drawer);

        ImguiEvent e = drawer.Current;
        int fontSize = _lineStyle!.FontSize;
        float lineH = fontSize + LinePadding * 2 + 2;

        // 收集要显示的行
        var lines = BuildLines();
        if (lines.Count == 0) return;

        // 计算面板宽度：取最宽行
        float maxW = 0f;
        foreach (var (text, _) in lines)
        {
            float w = _lineStyle!.CalcSize(StripRichText(text)).X + Padding * 2;
            if (w > maxW) maxW = w;
        }

        float panelW = Math.Max(maxW + Padding * 2, 200f);
        float panelH = lines.Count * lineH + DragHandleHeight + Padding * 2;
        float panelX = ConfigManager.PlayerListX.Value;
        float panelY = ConfigManager.PlayerListY.Value;

        // ── 拖拽手柄 ──
        var dragRect = new Rect(panelX, panelY, panelW, DragHandleHeight);
        Fill(drawer, dragRect, DragHandleColor);

        // ── 字体大小按钮（拖拽手柄右侧）──
        float fontBtnW = DragHandleHeight * 2f;
        float fontBtnH = DragHandleHeight;
        if (drawer.Button(new Rect(panelX + panelW - fontBtnW * 2 - 2, panelY, fontBtnW, fontBtnH), "A−", _fontBtnStyle!))
            AdjustFontSize(drawer, -2);
        if (drawer.Button(new Rect(panelX + panelW - fontBtnW, panelY, fontBtnW, fontBtnH), "A+", _fontBtnStyle!))
            AdjustFontSize(drawer, 2);

        if (e.Kind == ImguiEventKind.MouseDown && dragRect.Contains(e.MousePosition))
        {
            _isDragging = true;
            _dragOffset = e.MousePosition - new Vector2(panelX, panelY);
            e.Use();
        }
        if (_isDragging)
        {
            if (e.Kind == ImguiEventKind.MouseDrag)
            {
                float newX = e.MousePosition.X - _dragOffset.X;
                float newY = e.MousePosition.Y - _dragOffset.Y;
                // 用 Min/Max 而不是 Math.Clamp：窗口小于面板时上界会小于下界，Clamp 会抛。
                ConfigManager.PlayerListX.Value = Math.Min(Math.Max(newX, 0f), drawer.ScreenSize.X - panelW);
                ConfigManager.PlayerListY.Value = Math.Min(Math.Max(newY, 0f), drawer.ScreenSize.Y - panelH);
                e.Use();
            }
            if (e.Kind == ImguiEventKind.MouseUp)
            {
                _isDragging = false;
                e.Use();
            }
        }

        // ── 背景 ──
        var bgRect = new Rect(panelX, panelY + DragHandleHeight, panelW, panelH - DragHandleHeight);
        Fill(drawer, bgRect, BgColor);

        // ── 绘制行 ──
        float cy = panelY + DragHandleHeight + Padding;
        foreach (var (text, _) in lines)
        {
            drawer.Label(new Rect(panelX + Padding, cy, panelW - Padding * 2, lineH), text, _lineStyle!);
            cy += lineH;
        }
    }

    // ====================================================================
    // 构建行内容
    // ====================================================================
    private static System.Collections.Generic.List<(string text, int uid)> BuildLines()
    {
        var lines = new System.Collections.Generic.List<(string, int)>();
        var scene = GameFlow.LocalScene;

        // 仅在游戏场景中读取坐标/地图标签，避免在 MainScene 等场景中触发 GetCharacterUnit 警告
        bool needsGameplayData = scene is Scene.DayScene or Scene.WorkScene or Scene.IzakayaPrepScene;

        // 本地玩家与房间身份均来自服务器。
        // 坐标取出的同一行就换算成镜像 Vector2：NetPlayer.Position 仍是引擎向量，这里只取分量、
        // 不写出引擎类型名；三元条件照旧，未进入游戏场景时不读坐标。
        var local = PlayerManager.Local;
        string localLine = FormatPlayer(
            local.Uid, local.Id, scene,
            needsGameplayData ? PlayerManager.LocalMapLabel : MapLabel.Unknown,
            needsGameplayData ? local.Position : Vector2.Zero,
            local.IsDayOver, local.IsPrepOver,
            local.IzakayaMapLabel, local.IzakayaLevel,
            isSelf: true, isHost: GameSession.IsRoomHost);
        lines.Add((localLine, local.Uid));

        // Peers sorted by UID
        var sorted = new System.Collections.Generic.SortedDictionary<int, PeerPlayer>(PlayerManager.Peers);
        foreach (var kvp in sorted)
        {
            var peer = kvp.Value;
            string line = FormatPlayer(
                peer.Uid, peer.Id, peer.Scene,
                peer.HasMotion && peer.CanRender ? peer.MapLabel : MapLabel.Unknown,
                peer.HasMotion && peer.CanRender ? peer.Position : Vector2.Zero,
                peer.IsDayOver, peer.IsPrepOver,
                peer.IzakayaMapLabel, peer.IzakayaLevel,
                isSelf: false, isHost: kvp.Key == GameSession.Room?.Host,
                hasMotion: peer.HasMotion && peer.CanRender);
            lines.Add((line, kvp.Key));
        }

        var publicSorted = new System.Collections.Generic.SortedDictionary<int, PeerPlayer>(PlayerManager.PublicPeers);
        foreach (var kvp in publicSorted)
        {
            if (PlayerManager.Peers.ContainsKey(kvp.Key)) continue;
            var peer = kvp.Value;
            string line = FormatPlayer(
                peer.Uid, peer.Id, peer.Scene,
                peer.HasMotion && peer.CanRender ? peer.MapLabel : MapLabel.Unknown,
                peer.HasMotion && peer.CanRender ? peer.Position : Vector2.Zero,
                peer.IsDayOver, peer.IsPrepOver,
                peer.IzakayaMapLabel, peer.IzakayaLevel,
                isSelf: false, isHost: false,
                hasMotion: peer.HasMotion && peer.CanRender,
                scopeTag: "Online");
            lines.Add((line, kvp.Key));
        }

        return lines;
    }

    /// <summary>
    /// 位置由调用点从角色接口取分量换算成镜像 <c>Mystia.Numerics.Vector2</c>，渲染尺寸同样走镜像值类型；
    /// 本方法及以下格式化只用到镜像向量。
    /// </summary>
    private static string FormatPlayer(
        int uid, string id, Scene scene,
        MapLabel mapLabel, Vector2 pos,
        bool isDayOver, bool isPrepOver,
        MapLabel izakayaMapLabel, int izakayaLevel,
        bool isSelf, bool isHost, bool hasMotion = true,
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
            Scene.DayScene or Scene.WorkScene when !hasMotion => name,
            Scene.DayScene => FormatDayLine(name, dim, mapLabel, pos, isDayOver, izakayaMapLabel, izakayaLevel, uid, scopeTag == null),
            Scene.IzakayaPrepScene => scopeTag == null ? $"{name}  {ReadyTag(isPrepOver)}" : name,
            Scene.WorkScene when scopeTag == null && GameSession.HasRoomPeers
                && PrepSceneManager.IsYuyukoChallenge && PrepSceneManager.IsYuyukoPrepActive =>
                $"{name}  {ReadyTag(PrepSceneManager.IsYuyukoPrepReady(uid))}",
            Scene.WorkScene => $"{name}  <color={dim}>({pos.X:F2}, {pos.Y:F2})</color>",
            _ => name
        };
    }

    /// <summary>
    /// DayScene: 全员 DayOver 后显示选店信息，否则显示地图+坐标+状态
    /// </summary>
    private static string FormatDayLine(string name, string dim,
        MapLabel mapLabel, Vector2 pos, bool isDayOver,
        MapLabel izakayaMapLabel, int izakayaLevel, int uid, bool inRoom)
    {
        var destination = inRoom && GameSession.HasRoomPeers ? DayDestinationManager.GetIntent(uid) : DayDestination.None;
        if (destination != DayDestination.None)
            return $"{name}  <color={dim}>{mapLabel.GetDisplayName()}  ({pos.X:F2}, {pos.Y:F2})</color>  {DayDestinationManager.ReadyText(destination)}";
        if (!PlayerManager.AllDayOver)
        {
            // 仍在白天探索
            return $"{name}  <color={dim}>{mapLabel.GetDisplayName()}  ({pos.X:F2}, {pos.Y:F2})  {ReadyTag(isDayOver)}</color>";
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
        => $"#{(int)(c.R * 255):X2}{(int)(c.G * 255):X2}{(int)(c.B * 255):X2}";

    private static string StripRichText(string text)
        => System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");

    // ====================================================================
    // 样式初始化
    // ====================================================================
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

        int fontSize = ConfigManager.PlayerListFontSize.Value > 0
            ? ConfigManager.PlayerListFontSize.Value
            : Math.Min(Math.Max((int)drawer.ScreenSize.Y / 55, 12), 20);

        _lineStyle = drawer.Skin.Label;
        _lineStyle.Font = font;
        _lineStyle.FontSize = fontSize;
        _lineStyle.RichText = true;
        _lineStyle.WordWrap = false;
        _lineStyle.Normal.TextColor = Color.White;
        _lineStyle.Padding.Left = 4;
        _lineStyle.Padding.Right = 4;
        _lineStyle.Padding.Top = (int)LinePadding;
        _lineStyle.Padding.Bottom = (int)LinePadding;

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

    private static void AdjustFontSize(IIMGUIDrawer drawer, int delta)
    {
        int current = ConfigManager.PlayerListFontSize.Value;
        int effective = current > 0
            ? current
            : Math.Min(Math.Max((int)drawer.ScreenSize.Y / 55, 12), 20);
        int newSize = Math.Min(Math.Max(effective + delta, 10), 36);
        ConfigManager.PlayerListFontSize.Value = newSize;
        ResetStyles();
    }
}
