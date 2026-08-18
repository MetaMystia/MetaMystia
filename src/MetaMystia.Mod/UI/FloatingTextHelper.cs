using BepInEx.Unity.IL2CPP.Utils;
using MetaMystia;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace MetaMystia.UI;

[AutoLog]
public static partial class FloatingTextHelper
{
    private static GameObject activeTextPeer;
    private static GameObject activeTextSelf;

    // Font
    private static TMP_FontAsset _cachedFont;
    private static bool _fontSearched;
    private const float OutlineWidthValue = 0.05f;

    private static TMP_FontAsset GetFont()
    {
        if (_cachedFont != null) return _cachedFont;
        if (_fontSearched) return _cachedFont;
        _fontSearched = true;

        try
        {
            // 1) 尝试从系统字体创建 TMP 字体
            var osFont = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 48);
            if (osFont != null)
            {
                _cachedFont = TMP_FontAsset.CreateFontAsset(osFont);
                if (_cachedFont != null)
                {
                    Log.Info($"Created TMP font from OS 'Microsoft YaHei'");
                    return _cachedFont;
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning($"CreateFontAsset failed: {e.Message}");
        }

        try
        {
            // 2) fallback: 从游戏已加载资源中查找 TMP 字体
            var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            if (allFonts != null && allFonts.Length > 0)
            {
                // 优先查找名称含 YaHei/CJK
                _cachedFont = allFonts.FirstOrDefault(f =>
                    f?.name != null && (f.name.Contains("YaHei") || f.name.Contains("CJK")));
                // 退而求其次取第一个
                _cachedFont ??= allFonts.FirstOrDefault(f => f != null);
                if (_cachedFont != null)
                    Log.Info($"Using game TMP font: {_cachedFont.name}");
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Font resource search failed: {e.Message}");
        }

        return _cachedFont;
    }

    private static void ApplyStyle(TextMeshPro tmp, float fontSize, Color textColor)
    {
        var font = GetFont();
        if (font != null)
            tmp.font = font;

        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = textColor;

        // 描边: SDF outline + 额外偏移阴影作为后备
        try
        {
            var mat = new Material(tmp.fontMaterial);
            mat.EnableKeyword("OUTLINE_ON");
            mat.SetFloat("_OutlineWidth", OutlineWidthValue);
            mat.SetColor("_OutlineColor", Color.black);
            // 轻微 underlay（底层阴影）增强可读性
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetFloat("_UnderlayOffsetX", 0f);
            mat.SetFloat("_UnderlayOffsetY", 0f);
            mat.SetFloat("_UnderlayDilate", 0.3f);
            mat.SetColor("_UnderlayColor", Color.black);
            tmp.fontMaterial = mat;
        }
        catch (Exception e)
        {
            Log.Warning($"Outline setup failed: {e.Message}");
        }
    }

    #region 持久玩家标签（显示玩家 ID）

    private static readonly Dictionary<int, GameObject> playerLabels = new();

    /// <summary>
    /// 在角色脚下方创建/更新持久标签（显示玩家 ID）。
    /// 仅在联机且状态信息可见时显示。
    /// </summary>
    public static void SetPlayerLabel(int uid, string displayName, Transform parent)
    {
        if (parent == null) return;
        RemovePlayerLabel(uid);

        var go = new GameObject($"MetaLabel_{uid}");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 1.5f, 0);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = displayName;
        ApplyStyle(tmp, 3.5f, new Color(1f, 1f, 0.7f, 0.85f));

        go.SetActive(PluginManager.IsStatusVisible);
        playerLabels[uid] = go;
    }

    /// <summary>
    /// 更新已有标签的显示文本（例如玩家改名时），若标签不存在则忽略
    /// </summary>
    public static void UpdatePlayerLabel(int uid, string displayName)
    {
        if (playerLabels.TryGetValue(uid, out var go) && go != null)
        {
            var tmp = go.GetComponent<TextMeshPro>();
            if (tmp != null) tmp.text = displayName;
        }
    }

    public static void RemovePlayerLabel(int uid)
    {
        if (playerLabels.TryGetValue(uid, out var go))
        {
            if (go != null) UnityEngine.Object.Destroy(go);
            playerLabels.Remove(uid);
        }
    }

    public static void ClearAllLabels()
    {
        foreach (var go in playerLabels.Values)
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }
        playerLabels.Clear();
    }

    public static void SetLabelsVisible(bool visible)
    {
        foreach (var go in playerLabels.Values)
        {
            if (go != null) go.SetActive(visible);
        }
    }

    #endregion

    private static GameObject MakeFloatingText(Transform parent, string text)
    {
        var go = new GameObject("FloatingText");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0, 2.0f, 0);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        ApplyStyle(tmp, 5f, Color.white);

        return go;
    }

    private static void ShowFloatingText(Common.CharacterUtility.CharacterControllerUnit comp, string text, float duration = 5f)
    {
        if (activeTextPeer != null)
        {
            UnityEngine.Object.Destroy(activeTextPeer);
        }
        if (comp == null)
        {
            return;
        }
        activeTextPeer = MakeFloatingText(comp.transform, text);
        comp.StartCoroutine(FadeAndDestroy(activeTextPeer.GetComponent<TextMeshPro>(), duration));
    }

    private static void ShowFloatingTextSelf(string text, float duration = 5f)
    {
        if (activeTextSelf != null)
        {
            UnityEngine.Object.Destroy(activeTextSelf);
        }

        var character = PlayerManager.Local.GetCharacterUnit();
        if (character == null)
        {
            return;
        }
        activeTextSelf = MakeFloatingText(character.transform, text);
        character.StartCoroutine(FadeAndDestroy(activeTextSelf.GetComponent<TextMeshPro>(), duration));
    }

    private static System.Collections.IEnumerator FadeAndDestroy(TextMeshPro tmp, float duration)
    {
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        float fade = 0f;
        while (fade < 0.5f)
        {
            fade += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, fade / 0.5f);

            var c = tmp.color;
            c.a = alpha;
            tmp.color = c;

            yield return null;
        }

        if (tmp != null && tmp.gameObject != null)
        {
            GameObject.Destroy(tmp.gameObject);
        }
    }

    public static void ShowFloatingTextOnMainThread(Common.CharacterUtility.CharacterControllerUnit component, string Message)
    {
        PluginManager.Instance.RunOnMainThread(() => ShowFloatingText(component, Message));
    }

    public static void ShowFloatingTextSelfOnMainThread(string Message)
    {
        PluginManager.Instance.RunOnMainThread(() => ShowFloatingTextSelf(Message));
    }
}
