using System.Collections.Generic;

using Mystia.Assets;
using Mystia.Numerics;
using Mystia.Scenes;

using MetaMystia.Multiplayer;

namespace MetaMystia.UI;

public static partial class FloatingTextHelper
{
    /// <summary>头顶名牌的样式：宿主上方 1.5 格、淡黄 3.5 号字、八五成不透明（与原实现一致）。</summary>
    private static readonly FloatingLabelStyle NameStyle =
        new(new Vector3(0f, 1.5f, 0f), new Color(1f, 1f, 0.7f, 0.85f), 3.5f);

    /// <summary>一名玩家的名牌：宿主对象（框架据它取 transform）、期望文案与可见性。</summary>
    private sealed class PlayerLabel
    {
        public object Host;
        public IFloatingLabel? Label;
        public string Text = string.Empty;
        public bool Visible = true;
    }

    private static readonly Dictionary<int, PlayerLabel> playerLabels = new();

    /// <summary>
    /// 挂/刷新一名玩家的头顶名牌。<paramref name="host"/> 是角色（组件、游戏对象或 transform 均可），
    /// 框架的 <c>Bind</c> 自行取它的 transform；名牌只在场景循环的服务窗口内建立，因此这里只登记愿望，
    /// 真正的建立由 <see cref="ScenePresentation.Pump"/> 执行。
    /// </summary>
    public static void SetPlayerLabel(int uid, string displayName, object host)
    {
        if (host is null) return;

        if (!playerLabels.TryGetValue(uid, out var label) || label is null)
        {
            RemovePlayerLabel(uid);
            label = new PlayerLabel();
            playerLabels[uid] = label;
        }

        label.Host = host;
        label.Text = displayName;
        label.Visible = PluginManager.IsStatusVisible && GameSession.IsOnline;
        RequestFlush(uid);
    }

    public static void UpdatePlayerLabel(int uid, string displayName)
    {
        if (!playerLabels.TryGetValue(uid, out var label) || label is null) return;
        if (label.Text == displayName) return;

        label.Text = displayName;
        RequestFlush(uid);
    }
    public static void RemovePlayerLabel(int uid)
    {
        if (!playerLabels.Remove(uid, out var label) || label?.Label is null) return;
        label.Label.Stop();
    }

    public static void ClearAllLabels()
    {
        foreach (var label in playerLabels.Values)
            label?.Label?.Stop();
        playerLabels.Clear();
    }

    // 标签是角色的子对象，场景卸载时只释放登记，由游戏销毁对象。
    public static void ForgetPlayerLabels() => playerLabels.Clear();

    public static void SetLabelsVisible(bool visible)
    {
        foreach (var label in playerLabels.Values)
        {
            if (label is null) continue;
            label.Visible = visible && GameSession.IsOnline;
            label.Label?.SetVisible(label.Visible);
        }
    }

    /// <summary>
    /// 把「让这名玩家的名牌与登记状态一致」排进场景窗口。刷新是幂等的（句柄已存在就只改文案与可见性），
    /// 因此重复排队只是重复同一个动作，不会多建对象。
    /// </summary>
    private static void RequestFlush(int uid) =>
        ScenePresentation.Enqueue(services => FlushPlayerLabel(uid, services));

    private static void FlushPlayerLabel(int uid, IPresentationServices services)
    {
        if (!playerLabels.TryGetValue(uid, out var label) || label is null) return;

        if (label.Label is null)
        {
            if (services.Bind(label.Host) is not { } host) return;
            label.Label = services.AttachLabel(host, label.Text, NameStyle);
            if (label.Label is null) return;
        }
        else if (!label.Label.SetText(label.Text))
        {
            return;
        }

        label.Label.SetVisible(label.Visible);
    }
}
