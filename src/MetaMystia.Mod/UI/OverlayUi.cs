#nullable enable
using System;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MetaMystia.UI;

/// <summary>
/// 模组界面共用的覆盖层画布、字体与配色。尺寸以 1080 高的参考分辨率计，随屏幕高度缩放。
/// </summary>
[AutoLog]
public static partial class OverlayUi
{
    // 高于各场景的主画布（最高 32500），低于 RootCanvas（32600，暂停菜单、淡入淡出、光标），光标不被遮挡。
    private const int SortingOrder = 32550;
    private const float ReferenceHeight = 1080f;
    private const int SpriteRadius = 16;

    public static readonly Color Surface = new(0.071f, 0.063f, 0.09f, 0.86f);
    public static readonly Color Raised = new(0.13f, 0.118f, 0.16f, 0.97f);
    public static readonly Color Field = new(0f, 0f, 0f, 0.32f);
    public static readonly Color Bubble = new(0.05f, 0.045f, 0.065f, 0.6f);
    public static readonly Color Text = new(0.93f, 0.91f, 0.88f);
    public static readonly Color Dim = new(0.55f, 0.53f, 0.6f);
    public static readonly Color Accent = new(1f, 0.83f, 0.5f);
    public static readonly Color CommandColor = new(0.49f, 0.78f, 1f);
    public static readonly Color Selection = new(1f, 0.83f, 0.5f, 0.16f);
    public static readonly Color Outline = new(1f, 0.85f, 0.1f, 0.95f);

    public static RectTransform Root { get; private set; } = null!;
    public static TMP_FontAsset Font { get; private set; } = null!;
    private static Sprite _round = null!;
    private static TextMeshProUGUI _measure = null!;
    private static float _nextFontSearch;

    public static Vector2 Size => Root.rect.size;

    /// <summary>创建画布。requireGameFont 为真时，游戏字体尚未加载则暂不创建。</summary>
    public static bool EnsureCreated(bool requireGameFont)
    {
        if (Root != null) return true;
        if (requireGameFont && Time.unscaledTime < _nextFontSearch) return false;
        _nextFontSearch = Time.unscaledTime + 0.5f;
        var font = FindGameFont();
        if (font == null && requireGameFont) return false;
        Font = font ?? CreateSystemFont();
        Log.Info($"Overlay font: {Font.name}");

        var go = new GameObject("MetaMystiaOverlay");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.layer = 5;
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        scaler.referenceResolution = new Vector2(ReferenceHeight * 16f / 9f, ReferenceHeight);
        go.AddComponent<GraphicRaycaster>();
        Root = go.GetComponent<RectTransform>();
        _round = CreateRoundSprite();
        _measure = Label("Measure", Root, 20f, Color.clear);
        _measure.enabled = false;
        return true;
    }

    /// <summary>游戏语言变化后换用对应的思源黑体。</summary>
    public static void RefreshFont()
    {
        if (Root == null || FindGameFont() is not { } font || font == Font) return;
        Font = font;
        foreach (var text in Root.GetComponentsInChildren<TMP_Text>(true))
            text.font = font;
        Log.Info($"Overlay font: {font.name}");
    }

    // 游戏自带的思源黑体 SDF 为动态图集并带完整字库，可显示任意中日韩字符。
    private static TMP_FontAsset? FindGameFont()
    {
        string wanted = L10n.Language switch
        {
            Language.ChineseTraditional => "SourceHanSansTC-Regular SDF",
            Language.Korean => "SourceHanSansKR-Regular SDF",
            _ => "SourceHanSansSC-Regular SDF",
        };
        TMP_FontAsset? fallback = null;
        foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
        {
            if (font.name == wanted) return font;
            if (font.name == "SourceHanSansSC-Regular SDF") fallback = font;
        }
        return fallback;
    }

    private static TMP_FontAsset CreateSystemFont()
    {
        var font = UnityEngine.Font.CreateDynamicFontFromOSFont(
            new[] { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Source Han Sans SC", "WenQuanYi Micro Hei", "Arial" }, 48);
        var asset = TMP_FontAsset.CreateFontAsset(font);
        asset.hideFlags = HideFlags.HideAndDontSave;
        return asset;
    }

    private static Sprite CreateRoundSprite()
    {
        const int size = SpriteRadius * 2 + 2;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Math.Max(0f, Math.Abs(x + 0.5f - size / 2f) - (size / 2f - SpriteRadius));
                float dy = Math.Max(0f, Math.Abs(y + 0.5f - size / 2f) - (size / 2f - SpriteRadius));
                float alpha = Mathf.Clamp01(SpriteRadius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(SpriteRadius, SpriteRadius, SpriteRadius, SpriteRadius));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    public static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.layer = 5;
        var rect = go.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>纯色或圆角矩形；radius 为参考分辨率下的圆角半径。</summary>
    public static Image Box(string name, Transform parent, Color color, float radius = 0f, bool raycast = false)
    {
        var image = Node(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        if (radius > 0f)
        {
            image.sprite = _round;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = SpriteRadius / radius;
        }
        return image;
    }

    public static TextMeshProUGUI Label(string name, Transform parent, float size, Color color)
    {
        var text = Node(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Font;
        text.fontSize = size;
        text.color = color;
        text.richText = true;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    /// <summary>测量文本在给定宽度下的首选尺寸。</summary>
    public static Vector2 Measure(string text, float size, float width, bool wrap = true)
    {
        _measure.fontSize = size;
        _measure.enableWordWrapping = wrap;
        return _measure.GetPreferredValues(text, width, 0f);
    }

    /// <summary>四边拉伸，参数为距父节点各边的距离。</summary>
    public static void Fill(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>以同一点为锚点和轴心，按位置与尺寸放置。</summary>
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 覆盖层画布的世界坐标即屏幕坐标。
    public static bool IsPointerOver(RectTransform rect)
        => rect.gameObject.activeInHierarchy && rect.rect.Contains((Vector2)rect.InverseTransformPoint(Input.mousePosition));

    /// <summary>指针相对于给定锚点的位置（参考分辨率单位）。</summary>
    public static Vector2 Pointer(Vector2 anchor)
        => (Vector2)Root.InverseTransformPoint(Input.mousePosition) - (anchor - new Vector2(0.5f, 0.5f)) * Size;

    /// <summary>限制锚点与轴心相同的面板完全处于屏幕内。</summary>
    public static Vector2 Clamp(Vector2 anchor, Vector2 position, Vector2 size)
    {
        var screen = Size;
        return new Vector2(
            Mathf.Clamp(position.x, anchor.x * (size.x - screen.x), (1f - anchor.x) * (screen.x - size.x)),
            Mathf.Clamp(position.y, anchor.y * (size.y - screen.y), (1f - anchor.y) * (screen.y - size.y)));
    }

    // ColorUtility 在游戏中被裁剪。
    public static string Hex(Color color) => $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
}
