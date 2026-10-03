using System;
using System.Linq;

using TMPro;
using UnityEngine;

using MetaMystia.Multiplayer;

namespace MetaMystia.UI;

[AutoLog]
public static partial class FloatingTextHelper
{
    private static GameObject activeTextPeer;
    private static GameObject activeTextSelf;

    // Font
    private static TMP_FontAsset _cachedFont;
    private static Material _outlinedMaterial;
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

        if (_outlinedMaterial == null)
        {
            _outlinedMaterial = new Material(tmp.fontSharedMaterial);
            _outlinedMaterial.EnableKeyword("OUTLINE_ON");
            _outlinedMaterial.SetFloat("_OutlineWidth", OutlineWidthValue);
            _outlinedMaterial.SetColor("_OutlineColor", Color.black);
            _outlinedMaterial.EnableKeyword("UNDERLAY_ON");
            _outlinedMaterial.SetFloat("_UnderlayOffsetX", 0f);
            _outlinedMaterial.SetFloat("_UnderlayOffsetY", 0f);
            _outlinedMaterial.SetFloat("_UnderlayDilate", 0.3f);
            _outlinedMaterial.SetColor("_UnderlayColor", Color.black);
        }
        tmp.fontSharedMaterial = _outlinedMaterial;
    }


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
        // 挂在进程级的 owner 上：淡出不再随场景销毁而停止（对象被销毁时 FadeAndDestroy 自行结束）。
        var coroutines = ModRuntime.Coroutines;
        coroutines.StartOn(coroutines.Owner, _ => FadeAndDestroy(activeTextPeer.GetComponent<TextMeshPro>(), duration));
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
        // 同上：进程级 owner，淡出不再随场景销毁而停止。
        var coroutines = ModRuntime.Coroutines;
        coroutines.StartOn(coroutines.Owner, _ => FadeAndDestroy(activeTextSelf.GetComponent<TextMeshPro>(), duration));
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
            // 协程现在挂在进程级 owner 上，场景销毁不再打断它：文字对象随之消失时要自己收尾。
            if (tmp == null)
                yield break;
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
        PluginManager.RunOnMainThread(() => ShowFloatingText(component, Message));
    }

    public static void ShowFloatingTextSelfOnMainThread(string Message)
    {
        PluginManager.RunOnMainThread(() => ShowFloatingTextSelf(Message));
    }
}
