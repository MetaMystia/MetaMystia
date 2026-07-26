#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

using MetaMystia;
using SgrYuki;

namespace MetaMystia.ResourceEx.SpellCollection.Daiyousei;

/// <summary>
/// 大妖精黑卡「雾符【妖精的薄雾】」的屏幕空间雾气视觉：在用餐区叠加半透明白雾并缓慢飘动 30 秒。
/// 符卡内部特效子模块，仅由 Spell_Daiyousei 调用（武器内聚特效不加 -API）。
/// </summary>
internal static class DaiyouseiFogEffect
{
    // 雾气图层数量（密度），避免魔法数字
    private const int FogLayerCount = 18;
    // 覆盖层渲染顺序：须在游戏 UI 之下、世界之上（与 poc 一致），不遮挡 Buff 栏文字
    private const int FogSortingOrder = -5;
    // 单层雾气淡入 / 淡出时长（秒）
    private const float FogFadeInDurationSeconds = 1.0f;
    private const float FogFadeOutDurationSeconds = 1.5f;
    // 雾气目标不透明度（0~1）
    private const float FogTargetAlpha = 0.8f;
    // 雾气尺寸随机范围（px）
    private const float FogSizeMin = 150f;
    private const float FogSizeMax = 300f;
    // 雾气漂移速度 / 位移范围随机区间
    private const float FogDriftSpeedMin = 0.1f;
    private const float FogDriftSpeedMax = 0.3f;
    private const float FogDriftRangeMin = 20f;
    private const float FogDriftRangeMax = 50f;
    // 雾气生成区域（屏幕锚点比例，定位雾团中心）
    private const float FogRegionMinX = 0.15f;
    private const float FogRegionMaxX = 0.43f;
    private const float FogRegionMinY = 0.31f;
    private const float FogRegionMaxY = 0.79f;
    // 雾气贴图分辨率（px）
    private const int FogTextureSize = 128;
    // 雾气图层锚点半尺寸比例：以区域中心为基准向外延展的矩形半边长
    private const float FogHalfSizeAnchorRatio = 0.1f;
    // 雾气漂移时间偏移随机上限（秒），错开各层相位避免同相飘动
    private const float FogTimeOffsetMax = 1000f;
    // 雾气贴图 Sprite 每单位像素数
    private const float FogSpritePixelsPerUnit = 100f;
    // 雾气纵向漂移频率相对横向的衰减系数
    private const float FogDriftYFrequencyFactor = 0.7f;
    // 雾气初始颜色（不透明白、初始全透明）
    private static readonly Color FogInitialColor = new(1f, 1f, 1f, 0f);
    // UI 轴心中心（0.5, 0.5）
    private static readonly Vector2 FogCenterPivot = new(0.5f, 0.5f);

    private static readonly LogWrapper Log = new(BepInEx.Logging.Logger.CreateLogSource(nameof(DaiyouseiFogEffect)), nameof(DaiyouseiFogEffect));

    // 雾气径向渐变贴图：会话内缓存复用，避免重复生成
    private static Sprite? _fogSprite;

    /// <summary>
    /// 启动持续 durationSeconds 秒的屏幕空间雾气视觉
    /// </summary>
    /// <param name="durationSeconds">雾气持续秒数，须为正数。</param>
    /// <returns>托管协程迭代器，由调用方 WrapToIl2Cpp / StartManagedCoroutine 驱动。</returns>
    internal static IEnumerator StartFogRoutine(float durationSeconds)
    {
        var fogSprite = GetFogSprite();
        if (fogSprite == null)
        {
            Log.LogError("[DaiyouseiFogEffect] 雾气贴图创建失败，跳过雾气视觉（Buff 栏条目仍生效）。");
            yield break;
        }

        var canvas = CreateFogCanvas();
        if (canvas == null) yield break;

        var layers = new List<FogLayer>(FogLayerCount);
        for (var i = 0; i < FogLayerCount; i++)
        {
            layers.Add(SpawnFogLayer(canvas.transform, fogSprite));
        }

        yield return FadeLayersRoutine(layers, FogTargetAlpha, FogFadeInDurationSeconds);

        var driftElapsed = 0f;
        while (driftElapsed < durationSeconds)
        {
            driftElapsed += Time.deltaTime;
            for (var i = 0; i < layers.Count; i++)
            {
                layers[i].UpdateDrift(driftElapsed);
            }
            yield return null;
        }

        yield return FadeLayersRoutine(layers, 0f, FogFadeOutDurationSeconds);
        Object.Destroy(canvas.gameObject);
    }

    /// <summary>
    /// 创建屏幕空间覆盖层 Canvas，承载雾气图层。
    /// </summary>
    /// <returns>覆盖层 Canvas；创建异常时返回 null。</returns>
    private static Canvas? CreateFogCanvas()
    {
        try
        {
            var canvasObject = new GameObject("Daiyousei_ScreenFog");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = FogSortingOrder;
            return canvas;
        }
        catch (Exception ex)
        {
            Log.LogError($"[DaiyouseiFogEffect] 创建雾气 Canvas 失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 在覆盖层上生成一个雾气图层：径向渐变贴图 + 随机位置 / 尺寸 / 漂移参数，初始透明。
    /// </summary>
    /// <param name="parent">覆盖层 Canvas 的 Transform。</param>
    /// <param name="fogSprite">雾气径向渐变贴图。</param>
    /// <returns>创建好的雾气图层状态。</returns>
    private static FogLayer SpawnFogLayer(Transform parent, Sprite fogSprite)
    {
        var layerObject = new GameObject("Daiyousei_FogLayer");
        layerObject.transform.SetParent(parent, false);

        var image = layerObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = fogSprite;
        image.color = FogInitialColor;
        image.preserveAspect = false;

        var rect = image.rectTransform;
        var centerX = Random.Range(FogRegionMinX, FogRegionMaxX);
        var centerY = Random.Range(FogRegionMinY, FogRegionMaxY);
        var size = Random.Range(FogSizeMin, FogSizeMax);
        rect.anchorMin = new Vector2(centerX - FogHalfSizeAnchorRatio, centerY - FogHalfSizeAnchorRatio);
        rect.anchorMax = new Vector2(centerX + FogHalfSizeAnchorRatio, centerY + FogHalfSizeAnchorRatio);
        rect.pivot = FogCenterPivot;
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;

        return new FogLayer(
            image,
            rect,
            Random.Range(FogDriftSpeedMin, FogDriftSpeedMax),
            Random.Range(FogDriftRangeMin, FogDriftRangeMax),
            Random.Range(0f, FogTimeOffsetMax));
    }

    /// <summary>
    /// 将全部雾气图层的不透明度在 durationSeconds 内从各自当前值缓动到 targetAlpha。
    /// </summary>
    /// <param name="layers">雾气图层集合。</param>
    /// <param name="targetAlpha">目标不透明度。</param>
    /// <param name="durationSeconds">缓动时长（秒），<=0 时直接置位。</param>
    /// <returns>托管协程迭代器。</returns>
    private static IEnumerator FadeLayersRoutine(IReadOnlyList<FogLayer> layers, float targetAlpha, float durationSeconds)
    {
        var startAlphas = new float[layers.Count];
        for (var i = 0; i < layers.Count; i++)
        {
            var image = layers[i].Image;
            startAlphas[i] = image != null ? image.color.a : 0f;
        }

        if (durationSeconds <= 0f)
        {
            for (var i = 0; i < layers.Count; i++) SetLayerAlpha(layers[i], targetAlpha);
            yield break;
        }

        for (var elapsed = 0f; elapsed < durationSeconds; elapsed += Time.deltaTime)
        {
            var progress = Mathf.Clamp01(elapsed / durationSeconds);
            for (var i = 0; i < layers.Count; i++)
            {
                SetLayerAlpha(layers[i], Mathf.Lerp(startAlphas[i], targetAlpha, progress));
            }
            yield return null;
        }

        for (var i = 0; i < layers.Count; i++) SetLayerAlpha(layers[i], targetAlpha);
    }

    /// <summary>
    /// 设置单个雾气图层的不透明度。
    /// </summary>
    /// <param name="layer">目标图层。</param>
    /// <param name="alpha">不透明度。</param>
    private static void SetLayerAlpha(FogLayer layer, float alpha)
    {
        var image = layer.Image;
        if (image == null) return;
        var color = image.color;
        image.color = new Color(color.r, color.g, color.b, alpha);
    }

    /// <summary>
    /// 获取（按需生成）雾气径向渐变贴图：中心不透明、边缘平滑归零的白色圆形。
    /// </summary>
    /// <returns>雾气贴图；生成异常时返回 null。</returns>
    private static Sprite? GetFogSprite()
    {
        if (_fogSprite != null) return _fogSprite;

        try
        {
            var size = FogTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            var center = new Vector2(size / 2f, size / 2f);
            var maxDist = size / 2f;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x, y), center);
                    var alpha = Mathf.Clamp01(1f - dist / maxDist);
                    alpha *= alpha;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            _fogSprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                FogSpritePixelsPerUnit);
            _fogSprite.name = "DaiyouseiFogSprite";
            return _fogSprite;
        }
        catch (Exception ex)
        {
            Log.LogError($"[DaiyouseiFogEffect] 创建雾气贴图失败: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 单个雾气图层的状态：图像引用与漂移参数，飘动由 UpdateDrift 每帧驱动。
    /// </summary>
    private readonly struct FogLayer
    {
        public readonly UnityEngine.UI.Image Image;
        public readonly RectTransform Rect;
        private readonly float _driftSpeed;
        private readonly float _driftRange;
        private readonly float _timeOffset;
        private readonly Vector2 _startPos;

        public FogLayer(UnityEngine.UI.Image image, RectTransform rect, float driftSpeed, float driftRange, float timeOffset)
        {
            Image = image;
            Rect = rect;
            _driftSpeed = driftSpeed;
            _driftRange = driftRange;
            _timeOffset = timeOffset;
            _startPos = rect.anchoredPosition;
        }

        /// <summary>
        /// 按 elapsed 时间以正弦/余弦合成偏移更新图层位置，实现缓慢飘动。
        /// </summary>
        /// <param name="elapsed">已飘动秒数。</param>
        public void UpdateDrift(float elapsed)
        {
            if (Rect == null) return;
            var t = elapsed + _timeOffset;
            var offsetX = Mathf.Sin(t * _driftSpeed) * _driftRange;
            var offsetY = Mathf.Cos(t * _driftSpeed * FogDriftYFrequencyFactor) * _driftRange * 0.5f;
            Rect.anchoredPosition = _startPos + new Vector2(offsetX, offsetY);
        }
    }
}
