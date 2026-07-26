#nullable enable

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameData.RunTime.Common;
using MetaMystia.UI;
using SgrYuki;
using SgrYuki.Utils;

namespace MetaMystia.ResourceEx.SpellCollection.Daiyousei;

/// <summary>
/// 大妖精红卡「全员在场」兜底效果：发放随机水果食材并播放图标飞入仓库的视觉动画。
/// 符卡内部特效子模块，仅由 Spell_Daiyousei 调用。
/// </summary>
internal static class DaiyouseiFruitEffect
{
    // 水果食材池：桃子(21)、葡萄(36)、柠檬(2001)
    private static readonly int[] FruitIngredientIds = { 21, 36, 2001 };

    // 覆盖层渲染顺序，需在游戏 UI 之上
    private const int OverlaySortingOrder = 100;
    // 水果图标显示尺寸（px）
    private const float FruitIconSize = 64f;
    // 飞行动画起点：屏幕中央（anchoredPosition 原点）
    private static readonly Vector2 FlyStartPosition = Vector2.zero;
    // 飞行动画终点屏幕比例：左下角仓库方位
    private const float FlyTargetXRatio = 0.08f;
    private const float FlyTargetYRatio = 0.32f;
    // 单个水果飞行时长与缩小淡出时长（秒）
    private const float FlyDurationSeconds = 0.5f;
    private const float ShrinkFadeDurationSeconds = 0.2f;

    private static readonly LogWrapper Log = new(BepInEx.Logging.Logger.CreateLogSource(nameof(DaiyouseiFruitEffect)), nameof(DaiyouseiFruitEffect));

    /// <summary>
    /// 发放指定数量的随机水果食材：逐个播放图标飞行动画后，将全部水果写入玩家仓库。
    /// </summary>
    /// <param name="fruitCount">发放的水果个数。</param>
    /// <returns>托管协程迭代器，由调用方 WrapToIl2Cpp 后交给游戏执行。</returns>
    internal static IEnumerator GrantFruitsRoutine(int fruitCount)
    {
        var grantedFruitIds = new List<int>(fruitCount);
        for (var i = 0; i < fruitCount; i++)
        {
            grantedFruitIds.Add(FruitIngredientIds[Random.Range(0, FruitIngredientIds.Length)]);
        }

        var overlayCanvas = CreateOverlayCanvas();
        var flyTargetPosition = new Vector2(Screen.width * FlyTargetXRatio, Screen.height * FlyTargetYRatio);

        foreach (var fruitId in grantedFruitIds)
        {
            var fruitSprite = GetIngredientSprite(fruitId);
            if (fruitSprite == null)
            {
                Log.LogWarning($"[DaiyouseiFruitEffect] 无法获取食材 id={fruitId} 的图标，跳过其动画。");
                continue;
            }

            var fruitImage = CreateFruitImage(overlayCanvas.transform, fruitSprite);
            yield return FlyImageRoutine(fruitImage.rectTransform, FlyStartPosition, flyTargetPosition, FlyDurationSeconds);
            yield return ShrinkAndFadeRoutine(fruitImage, ShrinkFadeDurationSeconds);
            Object.Destroy(fruitImage.gameObject);
        }

        GrantIngredients(grantedFruitIds);
        Object.Destroy(overlayCanvas.gameObject);
    }

    /// <summary>
    /// 创建屏幕空间覆盖层 Canvas，承载水果飞行动画。
    /// </summary>
    /// <returns>覆盖层 Canvas 组件。</returns>
    private static Canvas CreateOverlayCanvas()
    {
        var canvasObject = new GameObject("Daiyousei_FruitAnim");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = OverlaySortingOrder;
        return canvas;
    }

    /// <summary>
    /// 从语言数据库读取食材图标。
    /// </summary>
    /// <param name="ingredientId">食材 id。</param>
    /// <returns>食材图标，未收录或无图返回 null。</returns>
    private static Sprite? GetIngredientSprite(int ingredientId)
    {
        var ingredients = GameData.CoreLanguage.Collections.DataBaseLanguage.Ingredients;
        if (ingredients == null || !ingredients.ContainsKey(ingredientId)) return null;
        return ingredients[ingredientId]?.Visual;
    }

    /// <summary>
    /// 在覆盖层上创建一个水果图标 Image，置于飞行起点。
    /// </summary>
    /// <param name="overlayParent">覆盖层 Canvas 的 Transform。</param>
    /// <param name="fruitSprite">水果图标。</param>
    /// <returns>创建好的 Image 组件。</returns>
    private static UnityEngine.UI.Image CreateFruitImage(Transform overlayParent, Sprite fruitSprite)
    {
        var imageObject = new GameObject("Daiyousei_FruitIcon");
        imageObject.transform.SetParent(overlayParent, false);

        var fruitImage = imageObject.AddComponent<UnityEngine.UI.Image>();
        fruitImage.sprite = fruitSprite;
        fruitImage.preserveAspect = true;

        var rectTransform = fruitImage.rectTransform;
        rectTransform.sizeDelta = new Vector2(FruitIconSize, FruitIconSize);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = FlyStartPosition;
        return fruitImage;
    }

    /// <summary>
    /// 以缓出曲线将 RectTransform 从起点移动到终点。
    /// </summary>
    /// <param name="rectTransform">被移动的 RectTransform。</param>
    /// <param name="fromPosition">起点 anchoredPosition。</param>
    /// <param name="toPosition">终点 anchoredPosition。</param>
    /// <param name="durationSeconds">飞行时长（秒）。</param>
    /// <returns>托管协程迭代器。</returns>
    private static IEnumerator FlyImageRoutine(RectTransform rectTransform, Vector2 fromPosition, Vector2 toPosition, float durationSeconds)
    {
        for (var elapsed = 0f; elapsed < durationSeconds; elapsed += Time.deltaTime)
        {
            var linearProgress = elapsed / durationSeconds;
            var easedProgress = 1f - (1f - linearProgress) * (1f - linearProgress);
            rectTransform.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, easedProgress);
            yield return null;
        }
        rectTransform.anchoredPosition = toPosition;
    }

    /// <summary>
    /// 将 Image 缩小至零并淡出。
    /// </summary>
    /// <param name="image">目标 Image。</param>
    /// <param name="durationSeconds">动画时长（秒）。</param>
    /// <returns>托管协程迭代器。</returns>
    private static IEnumerator ShrinkAndFadeRoutine(UnityEngine.UI.Image image, float durationSeconds)
    {
        var rectTransform = image.rectTransform;
        var startSize = rectTransform.sizeDelta;
        var startColor = image.color;

        for (var elapsed = 0f; elapsed < durationSeconds; elapsed += Time.deltaTime)
        {
            var progress = elapsed / durationSeconds;
            rectTransform.sizeDelta = Vector2.Lerp(startSize, Vector2.zero, progress);
            image.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(startColor.a, 0f, progress));
            yield return null;
        }
    }

    /// <summary>
    /// 将水果食材写入玩家仓库，并弹出获得提示。
    /// </summary>
    /// <param name="fruitIds">发放的食材 id 列表。</param>
    private static void GrantIngredients(List<int> fruitIds)
    {
        var il2cppFruitIds = new Il2CppSystem.Collections.Generic.List<int>(fruitIds.Count);
        foreach (var fruitId in fruitIds) il2cppFruitIds.Add(fruitId);
        RunTimeStorage.IngredientInRange(il2cppFruitIds.ToIEnumerable(), false);
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage(TextId.Spell_Daiyousei_GrantFruit.Get());
        Log.LogInfo($"[DaiyouseiFruitEffect] 已发放 {fruitIds.Count} 个水果食材。");
    }
}
