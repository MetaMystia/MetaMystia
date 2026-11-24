using System.Collections.Generic;
using System.Linq;

using TMPro;
using UnityEngine;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Network;

namespace MetaMystia.UI;

/// <summary>
/// 玩家列表，可拖动。联机时在控制台打开或按回车后显示。
/// </summary>
[AutoLog]
public static partial class PlayerListPanel
{
    private const float HeaderHeight = 26f;
    private const float Pad = 10f;
    private const float MinWidth = 220f;
    private const float DefaultFontSize = 17f;

    private static readonly Color HostColor = new(0.56f, 0.83f, 1f);
    private static readonly Color SelfColor = new(0.55f, 0.88f, 0.64f);
    private static readonly Color PeerColor = new(0.9f, 0.88f, 0.85f);
    private static readonly Color ReadyColor = new(0.49f, 0.88f, 0.63f);
    private static readonly Color NotReadyColor = new(1f, 0.69f, 0.38f);

    private static bool _visible;
    private static RectTransform _root;
    private static RectTransform _header;
    private static TextMeshProUGUI _title;
    private static RectTransform _fontDown;
    private static RectTransform _fontUp;
    private static TextMeshProUGUI _body;
    private static string _shown = "";
    private static float _shownSize;
    private static float _nextRefresh;
    private static bool _dragging;
    private static Vector2 _dragOffset;
    private static Vector2 _position;

    private static float FontSize => ConfigManager.PlayerListFontSize.Value > 0 ? ConfigManager.PlayerListFontSize.Value : DefaultFontSize;
    // 以右上角定位：宽度随内容和字号变化时，标题栏右侧的字号按钮不移动。
    private static Vector2 Anchor => Vector2.one;

    public static void Update()
    {
        if (!InGameConsole.HasKeyboardFocus && Hotkeys.PlayerList.WasPressedThisFrame())
            _visible = !_visible;

        bool show = GameSession.IsConnectingOrOnline && (_visible || InGameConsole.IsOpen);
        if (_root == null)
        {
            if (!show || !OverlayUi.EnsureCreated(loadFont: true)) return;
            Build();
        }
        _root.gameObject.SetActive(show);
        if (!show)
        {
            _dragging = false;
            return;
        }
        if (Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 0.1f;
            Refresh();
        }
        HandleDrag();
    }

    private static void Build()
    {
        var panel = OverlayUi.Box("PlayerList", OverlayUi.Root, OverlayUi.Surface, 10f, raycast: true);
        _root = panel.rectTransform;
        _header = OverlayUi.Node("Header", _root);
        _header.anchorMin = new Vector2(0, 1);
        _header.anchorMax = Vector2.one;
        _header.pivot = new Vector2(0.5f, 1);
        _header.sizeDelta = new Vector2(0, HeaderHeight);
        _title = OverlayUi.Label("Title", _header, 14f, OverlayUi.Dim);
        _title.alignment = TextAlignmentOptions.MidlineLeft;
        _title.enableWordWrapping = false;
        OverlayUi.Fill(_title.rectTransform, Pad + 2, 0, Pad + 2 * OverlayUi.FontButtonWidth, 0);
        _fontDown = OverlayUi.FontButton(_header, "A−", -6 - OverlayUi.FontButtonWidth);
        _fontUp = OverlayUi.FontButton(_header, "A+", -6);
        _body = OverlayUi.Label("Body", _root, FontSize, OverlayUi.Text);
        _body.enableWordWrapping = false;
        _body.lineSpacing = 8f;
        OverlayUi.Fill(_body.rectTransform, Pad + 2, Pad, Pad, HeaderHeight + 4);
    }

    private static void Refresh()
    {
        var lines = BuildLines();
        string text = string.Join("\n", lines);
        float fontSize = FontSize;
        if (text != _shown || fontSize != _shownSize)
        {
            _shown = text;
            _shownSize = fontSize;
            _body.fontSize = fontSize;
            _body.text = text;
            int players = PlayerManager.Peers.Keys.Union(PlayerManager.PublicPeers.Keys).Count() + 1;
            _title.text = $"{TextId.PlayerListTitle.Get()}  <color=#6F6A7A>{players}</color>";
        }
        var measured = _body.GetPreferredValues(text, 4000f, 0f);
        var size = new Vector2(Mathf.Clamp(measured.x + 2 * Pad + 4, MinWidth, OverlayUi.Size.x * 0.6f), HeaderHeight + 4 + measured.y + Pad);
        var requested = _dragging ? _position : new Vector2(-ConfigManager.PlayerListX.Value, -ConfigManager.PlayerListY.Value);
        _position = OverlayUi.Clamp(Anchor, requested, size);
        OverlayUi.Place(_root, Anchor, _position, size);
    }

    private static void HandleDrag()
    {
        if (Input.GetMouseButtonDown(0) && (OverlayUi.IsPointerOver(_fontDown) || OverlayUi.IsPointerOver(_fontUp)))
        {
            OverlayUi.AdjustFontSize(ConfigManager.PlayerListFontSize, (int)DefaultFontSize, OverlayUi.IsPointerOver(_fontUp) ? 2 : -2);
            _nextRefresh = 0f;
        }
        else if (Input.GetMouseButtonDown(0) && OverlayUi.IsPointerOver(_header))
        {
            _dragging = true;
            _dragOffset = _position - OverlayUi.Pointer(Anchor);
        }
        if (!_dragging) return;
        if (Input.GetMouseButton(0))
        {
            _position = OverlayUi.Clamp(Anchor, OverlayUi.Pointer(Anchor) + _dragOffset, _root.sizeDelta);
            _root.anchoredPosition = _position;
            return;
        }
        _dragging = false;
        ConfigManager.Config.SaveOnConfigSet = false;
        ConfigManager.PlayerListX.Value = Mathf.Round(-_position.x);
        ConfigManager.PlayerListY.Value = Mathf.Round(-_position.y);
        ConfigManager.Config.SaveOnConfigSet = true;
        ConfigManager.Config.Save();
    }

    private static List<string> BuildLines()
    {
        var lines = new List<string>();
        var scene = GameFlow.LocalScene;

        // 仅在游戏场景中读取坐标/地图标签，避免在 MainScene 等场景中触发 GetCharacterUnit 警告
        bool needsGameplayData = GameFlow.CharactersReady && scene is Scene.DayScene or Scene.WorkScene;

        // 本地玩家与房间身份均来自服务器。
        var local = PlayerManager.Local;
        int uidWidth = PlayerManager.Peers.Keys.Concat(PlayerManager.PublicPeers.Keys).Append(local.Uid)
            .Max(uid => uid.ToString().Length);
        string localLine = FormatPlayer(
            local.Uid, uidWidth, local.Id, scene, GameFlow.Stage,
            needsGameplayData && scene == Scene.DayScene ? PlayerManager.LocalMapLabel : MapLabel.Unknown,
            needsGameplayData ? local.Position : Vector2.zero,
            local.IsDayOver, local.IsPrepOver,
            local.IzakayaMapLabel, local.IzakayaLevel,
            isSelf: true, isHost: GameSession.IsRoomHost, hasMotion: needsGameplayData);
        lines.Add(localLine);

        // 同房间优先，各组按 UID 排序。
        bool publicGroupStarted = false;
        foreach (var peer in PlayerManager.Peers.Values.OrderBy(p => p.Uid)
            .Concat(PlayerManager.PublicPeers.Values.OrderBy(p => p.Uid)))
        {
            bool inRoom = PlayerManager.Peers.ContainsKey(peer.Uid);
            if (!inRoom && !publicGroupStarted)
            {
                lines.Add($"<size=80%><color=#6F6A7A>{TextId.PlayerListOnline.Get()}</color></size>");
                publicGroupStarted = true;
            }
            var motion = peer.NetworkState?.Motion;
            // 与服务器的位置转发范围一致；跨场景缓存不作为实时坐标显示。
            bool hasMotion = peer.HasMotion && needsGameplayData && peer.Scene == scene
                && (scene == Scene.DayScene || inRoom);
            var position = peer.HasMotion ? new Vector2(motion.X, motion.Y) : Vector2.zero;
            // 同地图使用本地角色的连续位置；切图或角色尚未创建时保留网络坐标。
            bool sameMap = hasMotion && (scene == Scene.WorkScene || (scene == Scene.DayScene
                && !DayScene.SceneManager.Instance.IsMapSwapping && peer.IsSameMapAsLocal
                && peer.NetworkState.Map == peer.MapLabel));
            if (hasMotion && sameMap && peer.CanRender && peer.unit != null && peer.rb2d != null)
                position = peer.Position;
            string line = FormatPlayer(
                peer.Uid, uidWidth, peer.Id, peer.Scene, peer.NetworkState?.Stage ?? GameStage.Unavailable,
                peer.NetworkState?.Map ?? MapLabel.Unknown,
                position,
                peer.IsDayOver, peer.IsPrepOver,
                peer.IzakayaMapLabel, peer.IzakayaLevel,
                isSelf: false, isHost: peer.Uid == GameSession.Room?.Host,
                hasMotion: hasMotion, inRoom: inRoom);
            lines.Add(line);
        }

        return lines;
    }

    private static string FormatPlayer(
        int uid, int uidWidth, string id, Scene scene, GameStage stage,
        MapLabel mapLabel, Vector2 pos,
        bool isDayOver, bool isPrepOver,
        MapLabel izakayaMapLabel, int izakayaLevel,
        bool isSelf, bool isHost, bool hasMotion, bool inRoom = true)
    {
        // 名字颜色
        string nameColor;
        if (isSelf) nameColor = OverlayUi.Hex(SelfColor);
        else if (isHost) nameColor = OverlayUi.Hex(HostColor);
        else nameColor = OverlayUi.Hex(PeerColor);

        string displayId = LiveModeManager.GetDisplayName(uid);
        string name = LiveModeManager.IsActive
            ? $"<color={nameColor}>{displayId}</color>"
            // UID 等宽并右对齐到最长的位数，名字从同一位置开始。
            : $"<color=#6F6A7A><mspace=0.55em>{uid.ToString().PadLeft(uidWidth)}</mspace></color> <color={nameColor}>{id}</color>";
        string dim = OverlayUi.Hex(OverlayUi.Dim);
        // 只展示本人已提交的选店结果，不用全房间进度推断个人面板。
        if (scene == Scene.DayScene && inRoom && izakayaMapLabel.IsSelected() && izakayaLevel > 0)
            return $"{name}  <color={dim}>{izakayaMapLabel.GetDisplayName()} Lv.{izakayaLevel}</color>";
        string sceneText = scene == Scene.WorkScene && stage == GameStage.Preparation
            ? TextId.PlayerListPreparation.Get() : SceneText(scene);
        name += $"  <color={dim}>{sceneText}</color>";
        string location = scene == Scene.DayScene && mapLabel != MapLabel.Unknown ? mapLabel.GetDisplayName() : "";
        if (hasMotion && !(scene == Scene.DayScene && isDayOver))
        {
            string position = $"({pos.x:F2}, {pos.y:F2})";
            location = string.IsNullOrEmpty(location) ? position : $"{location}  {position}";
        }
        if (!string.IsNullOrEmpty(location)) name += $"  <color={dim}>{location}</color>";

        return scene switch
        {
            Scene.DayScene when inRoom => FormatDayLine(name, isDayOver, uid),
            Scene.IzakayaPrepScene when inRoom => $"{name}  {ReadyTag(isPrepOver)}",
            Scene.WorkScene when inRoom && stage == GameStage.Preparation =>
                $"{name}  {ReadyTag(isPrepOver)}",
            _ => name
        };
    }

    private static string SceneText(Scene scene) => (scene switch
    {
        Scene.MainScene => TextId.PlayerListMainMenu,
        Scene.DayScene => TextId.PlayerListDay,
        Scene.IzakayaPrepScene => TextId.PlayerListPreparation,
        Scene.WorkScene => TextId.PlayerListWork,
        Scene.ResultScene => TextId.PlayerListResult,
        Scene.LoadScene => TextId.PlayerListLoading,
        Scene.StaffScene => TextId.PlayerListStaff,
        _ => TextId.PlayerListUnknown,
    }).Get();

    /// <summary>
    /// 白天只为已提交入口或已结束探索的玩家显示准备标记。
    /// </summary>
    private static string FormatDayLine(string name, bool isDayOver, int uid)
    {
        var destination = GameSession.HasRoomPeers ? DayDestinationManager.GetIntent(uid) : DayDestination.None;
        if (destination != DayDestination.None)
            return $"{name}  {ReadyTag(true)}";
        return isDayOver ? $"{name}  {ReadyTag(true)}" : name;
    }

    private static string ReadyTag(bool ready)
    {
        return ready
            ? $"<color={OverlayUi.Hex(ReadyColor)}>✓</color>"
            : $"<color={OverlayUi.Hex(NotReadyColor)}>…</color>";
    }
}
