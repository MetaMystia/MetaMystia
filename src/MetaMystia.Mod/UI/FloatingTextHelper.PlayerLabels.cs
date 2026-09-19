using System.Collections.Generic;

using TMPro;
using UnityEngine;

using MetaMystia.Multiplayer;

namespace MetaMystia.UI;

public static partial class FloatingTextHelper
{
    private static readonly Dictionary<int, TextMeshPro> playerLabels = new();

    public static void SetPlayerLabel(int uid, string displayName, Transform parent)
    {
        if (parent == null) return;
        if (!playerLabels.TryGetValue(uid, out var label) || label == null || label.transform.parent != parent)
        {
            RemovePlayerLabel(uid);
            var go = new GameObject($"MetaLabel_{uid}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 1.5f, 0);
            label = go.AddComponent<TextMeshPro>();
            ApplyStyle(label, 3.5f, new Color(1f, 1f, 0.7f, 0.85f));
            playerLabels[uid] = label;
        }
        if (label.text != displayName) label.text = displayName;
        bool visible = PluginManager.IsStatusVisible && GameSession.IsOnline;
        if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
    }

    public static void UpdatePlayerLabel(int uid, string displayName)
    {
        if (playerLabels.TryGetValue(uid, out var label) && label != null && label.text != displayName)
            label.text = displayName;
    }

    public static void RemovePlayerLabel(int uid)
    {
        if (playerLabels.Remove(uid, out var label) && label != null)
            Object.Destroy(label.gameObject);
    }

    public static void ClearAllLabels()
    {
        foreach (var label in playerLabels.Values)
            if (label != null) Object.Destroy(label.gameObject);
        playerLabels.Clear();
    }

    // 标签是角色的子对象，场景卸载时只释放登记，由游戏销毁对象。
    public static void ForgetPlayerLabels() => playerLabels.Clear();

    public static void SetLabelsVisible(bool visible)
    {
        foreach (var label in playerLabels.Values)
            if (label != null) label.gameObject.SetActive(visible && GameSession.IsOnline);
    }
}
