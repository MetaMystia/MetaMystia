using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem.Linq;
using UnityEngine;

using Common.CharacterUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;
using GameData.RunTime.Common;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using MetaMystia.Patch;
using SgrYuki.Utils;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Daiyousei : SpellBase
{
    // 红卡召唤池：露米娅(1)、莉格露(0)、琪露诺(28)
    private static readonly int[] FriendIds = { 1, 0, 28 };
    // 降级：上白泽慧音(4)
    private const int KeineId = 4;
    // 水果食材：桃子(21)、葡萄(36)、柠檬(2001)
    private static readonly int[] FruitIds = { 21, 36, 2001 };

    private static readonly System.Random _rng = new System.Random();

    // 雾气覆盖区域
    internal const float FogMinX = 0.15f;
    internal const float FogMinY = 0.31f;
    internal const float FogMaxX = 0.43f;
    internal const float FogMaxY = 0.79f;

    // 黑卡雾气 buff 持续时间（秒）
    private const float FogDuration = 30f;

    private static Sprite _buffIcon;

    public static void LoadBuffIcon()
    {
        if (_buffIcon != null) return;
        if (ResourceExManager.TryGetSprite("rex://ResourceExample/assets/Buff/9000_1.png", out var sprite) && sprite != null)
        {
            _buffIcon = sprite;
            Log.LogInfo("[Daiyousei] Buff icon loaded");
        }
        else
        {
            Log.LogWarning("[Daiyousei] Buff icon load failed");
        }
    }

    private static void RegisterFogBuff()
    {
        NativeBuffHelper.RegisterCustomBuffDescription(
            NativeBuffHelper.BT.DaiyouseiFog,
            title: "飞雾",
            description: "雾气弥漫了你的用餐区",
            visual: _buffIcon);
        NativeBuffHelper.Register(NativeBuffHelper.BT.DaiyouseiFog, FogDuration);
    }

    public override string OnGettingSpellOwnerIdentifier()
        => "_ResourceExample_Daiyousei";

    // 关键：告诉游戏这个符卡有红卡效果
    public override bool HasPositiveSpell => true;
    // 关键：告诉游戏这个符卡有黑卡效果
    public override bool HasNegativeSpell => true;

    // 关键：告诉游戏自动调用符卡声明
    public override bool ShouldCallSpellDeclarationAuto(bool isPositiveSpell) => true;

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext ctx)
    {
        Log.LogInfo("[Daiyousei] *** OnPositiveBuffExecute CALLED ***");
        try { return PositiveBuffRoutine(ctx).WrapToIl2Cpp(); }
        catch (Exception ex)
        {
            Log.LogError($"[Daiyousei] OnPositiveBuffExecute threw: {ex}");
            throw;
        }
    }

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext ctx)
    {
        Log.LogInfo("[Daiyousei] *** OnNegativeBuffExecute CALLED ***");
        try { return NegativeBuffRoutine(ctx).WrapToIl2Cpp(); }
        catch (Exception ex)
        {
            Log.LogError($"[Daiyousei] OnNegativeBuffExecute threw: {ex}");
            throw;
        }
    }

    // ================================================================================
    // 红卡：「妖精的呼朋引伴」
    // ================================================================================

    [HideFromIl2Cpp]
    private IEnumerator PositiveBuffRoutine(SpellExecutionContext ctx)
    {
        var onField = GetOnFieldSpecialGuestIds();

        // 从召唤池中筛选不在场上的朋友
        var available = new List<int>();
        foreach (var id in FriendIds)
            if (!onField.Contains(id))
                available.Add(id);

        if (available.Count > 0)
        {
            var chosen = available[_rng.Next(available.Count)];
            Log.LogInfo($"[Daiyousei] 红卡：召唤稀客 id={chosen}");
            yield return SummonGuestCoroutine(chosen);
        }
        else if (!onField.Contains(KeineId))
        {
            Log.LogInfo("[Daiyousei] 红卡：三人都在场，召唤慧音");
            yield return SummonGuestCoroutine(KeineId);
        }
        else
        {
            Log.LogInfo("[Daiyousei] 红卡：四人都在场，给水果食材");
            yield return GiveRandomFruitsRoutine(3);
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator SummonGuestCoroutine(int guestId)
    {
        if (GuestsManager.Instance == null)
        {
            Log.LogWarning("[Daiyousei] GuestsManager.Instance is null, cannot summon");
            yield break;
        }

        if (!PlayerManager.SpecialGuestAvailable(guestId))
        {
            Log.LogWarning($"[Daiyousei] 稀客 id={guestId} 不可用 (SpecialGuestAvailable=false)");
            yield break;
        }

        var specialGuest = DataBaseCharacter.RefSGuest(guestId);
        if (specialGuest == null)
        {
            Log.LogWarning($"[Daiyousei] RefSGuest({guestId}) returned null");
            yield break;
        }

        // 在用餐区入口附近生成
        var spawnPos = new Vector3(2f, 0f, 0f);

        var ctrl = new SpecialGuestsController(
            specialGuest,
            new Il2CppSystem.Nullable<Vector3>(spawnPos),
            null,
            GuestGroupController.LeaveType.Move,
            SpecialGuestsController.GuestSpawnType.Normal);

        GuestsManager.Instance.PostInitializeGuestGroup(ctrl, -1, false, true);
        Log.LogInfo($"[Daiyousei] 召唤稀客 id={guestId} 成功");

        yield break;
    }

    [HideFromIl2Cpp]
    private IEnumerator GiveRandomFruitsRoutine(int count)
    {
        var fruitIds = new List<int>(count);
        for (int i = 0; i < count; i++)
            fruitIds.Add(FruitIds[_rng.Next(FruitIds.Length)]);

        // 创建覆盖层 Canvas
        var canvasGO = new GameObject("Daiyousei_FruitAnim");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var screenW = Screen.width;
        var screenH = Screen.height;

        // 目标位置：屏幕左下角（仓库位置）
        var targetPos = new Vector2(screenW * 0.08f, screenH * 0.32f);

        for (int i = 0; i < fruitIds.Count; i++)
        {
            // 获取食材精灵
            Sprite sprite = null;
            try
            {
                var lang = GameData.CoreLanguage.Collections.DataBaseLanguage.Ingredients[fruitIds[i]];
                sprite = lang?.Visual;
            }
            catch { }

            if (sprite == null)
            {
                Log.LogWarning($"[Daiyousei] 无法获取食材 id={fruitIds[i]} 的精灵，跳过动画");
                continue;
            }

            // 创建 Image
            var imgGO = new GameObject($"Fruit_{i}");
            imgGO.transform.SetParent(canvasGO.transform, false);
            var img = imgGO.AddComponent<UnityEngine.UI.Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.SetNativeSize();

            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(64f, 64f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // 初始位置：屏幕中央偏上，随机偏移
            var startX = screenW * 0.0f + (_rng.Next(0, 0));
            var startY = screenH * 0.0f + (_rng.Next(0, 0));
            rt.anchoredPosition = new Vector2(startX, startY);

            // 飞行动画
            yield return FlyImage(rt, new Vector2(startX, startY), targetPos, 0.5f);

            // 缩小 + 淡出
            yield return ShrinkAndFade(img, rt, 0.2f);

            UnityEngine.Object.Destroy(imgGO);
        }

        // 实际添加食材
        var fruits = new Il2CppSystem.Collections.Generic.List<int>(fruitIds.Count);
        foreach (var id in fruitIds) fruits.Add(id);
        RunTimeStorage.IngredientInRange(fruits.ToIEnumerable(), false);
        Log.LogInfo($"[Daiyousei] 添加 {count} 个水果食材");

        UnityEngine.Object.Destroy(canvasGO);
    }

    [HideFromIl2Cpp]
    private static IEnumerator FlyImage(RectTransform rt, Vector2 from, Vector2 to, float duration)
    {
        for (var t = 0f; t < duration; t += Time.deltaTime)
        {
            var progress = t / duration;
            // 使用缓出曲线让飞行更有质感
            progress = 1f - (1f - progress) * (1f - progress);
            rt.anchoredPosition = Vector2.Lerp(from, to, progress);
            yield return null;
        }
        rt.anchoredPosition = to;
    }

    [HideFromIl2Cpp]
    private static IEnumerator ShrinkAndFade(UnityEngine.UI.Image img, RectTransform rt, float duration)
    {
        var startSize = rt.sizeDelta;
        var startColor = img.color;

        for (var t = 0f; t < duration; t += Time.deltaTime)
        {
            var progress = t / duration;
            rt.sizeDelta = Vector2.Lerp(startSize, Vector2.zero, progress);
            img.color = new Color(startColor.r, startColor.g, startColor.b,
                Mathf.Lerp(startColor.a, 0f, progress));
            yield return null;
        }
    }

    // ================================================================================
    // 黑卡：雾符【妖精的薄雾】
    // ================================================================================

    [HideFromIl2Cpp]
    private IEnumerator NegativeBuffRoutine(SpellExecutionContext ctx)
    {
        Log.LogInfo("[Daiyousei] 黑卡：创建屏幕空间白色雾气");

        // 注册 FogDriftUI（幂等）
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<FogDriftUI>())
            ClassInjector.RegisterTypeInIl2Cpp<FogDriftUI>();

        // 创建屏幕空间雾气（参考神绮传送门的方法）
        FogDriftUI.FogActive = true;
        var fogGO = CreateScreenFog();

        if (fogGO != null)
        {
            Log.LogInfo("[Daiyousei] 黑卡：雾气已创建，30秒后自动销毁");

            // 注册 buff 图标
            RegisterFogBuff();

            // 异步销毁（不阻塞符卡执行队列）
            PluginManager.Instance.StartCoroutine(
                ScreenFogDestroyRoutine(fogGO, FogDuration).WrapToIl2Cpp());
        }
        else
        {
            Log.LogWarning("[Daiyousei] 黑卡：雾气创建失败");
        }

        yield break;
    }

    // ================================================================================
    // 工具方法
    // ================================================================================

    private static HashSet<int> GetOnFieldSpecialGuestIds()
    {
        var result = new HashSet<int>();
        var allGuests = GuestsMap.GetAllGuestsSnapshot();
        foreach (var (_, fsm) in allGuests)
        {
            if (fsm?.Ids == null || fsm.Controller == null) continue;

            // 只统计特殊客人，排除普通客人 ID 干扰
            if (fsm.GuestType != GuestsManager.GuestType.Special) continue;

            var state = fsm.CurrentState;
            if (state == GuestFSM.State.Left || state == GuestFSM.State.Dead || state == GuestFSM.State.None)
                continue;

            if (!fsm.Controller.HaveNotLeft())
                continue;

            foreach (var id in fsm.Ids)
                result.Add(id);
        }
        return result;
    }

    // ================================================================================
    // 桌子位置查询（供雾气等效果使用）
    // ================================================================================

    /// <summary>
    /// 获取用餐区所有桌子的世界坐标列表。
    /// 通过反射查找场景中所有 DeskUnit MonoBehaviour，提取 transform.position。
    /// </summary>
    /// <returns>桌子世界坐标数组，找不到时返回空数组</returns>
    public static Vector3[] GetTablePositions()
    {
        var positions = new List<Vector3>();
        try
        {
            var allBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in allBehaviours)
            {
                if (mb == null) continue;
                if (mb.GetIl2CppType().Name != "DeskUnit") continue;
                var go = mb.gameObject;
                if (go == null) continue;
                positions.Add(go.transform.position);
            }
        }
        catch (Exception ex)
        {
            Log.LogError($"[Daiyousei] GetTablePositions failed: {ex.Message}");
        }
        return positions.ToArray();
    }

    // ================================================================================
    // 屏幕空间雾气效果（ScreenSpaceOverlay，参考神绮传送门）
    // ================================================================================

    /// <summary>
    /// 在屏幕空间中创建白色半透明雾气效果。
    /// 使用 ScreenSpaceOverlay Canvas + 多个半透明 Image 模拟雾气，确保始终可见。
    /// </summary>
    private static GameObject CreateScreenFog()
    {
        try
        {
            var canvasGO = new GameObject("Daiyousei_ScreenFog");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -5; // 在游戏 UI 之下，但在世界上层

            var screenW = Screen.width;
            var screenH = Screen.height;

            // 创建多个雾气层，覆盖整个屏幕
            var fogCount = 20;
            for (int i = 0; i < fogCount; i++)
                SpawnFogChild(canvasGO.transform);

            Log.LogInfo($"[Daiyousei] 屏幕空间雾气创建成功，{fogCount} 层");
            return canvasGO;
        }
        catch (Exception ex)
        {
            Log.LogError($"[Daiyousei] 创建屏幕空间雾气失败: {ex.Message}");
            return null;
        }
    }

    internal static void SpawnFogChild(Transform parent)
    {
        var fogChild = new GameObject("FogLayer");
        fogChild.transform.SetParent(parent, false);

        var img = fogChild.AddComponent<UnityEngine.UI.Image>();
        img.sprite = CreateFogSprite();
        img.color = new Color(1f, 1f, 1f, 0.8f);
        img.preserveAspect = false;

        var rt = img.rectTransform;
        var rx = UnityEngine.Random.Range(FogMinX, FogMaxX);
        var ry = UnityEngine.Random.Range(FogMinY, FogMaxY);
        var size = UnityEngine.Random.Range(150f, 300f);
        rt.anchorMin = new Vector2(rx - 0.1f, ry - 0.1f);
        rt.anchorMax = new Vector2(rx + 0.1f, ry + 0.1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;

        var drift = fogChild.AddComponent<FogDriftUI>();
        drift.DriftSpeed = UnityEngine.Random.Range(0.1f, 0.3f);
        drift.DriftRange = UnityEngine.Random.Range(20f, 50f);
        drift.Lifespan = UnityEngine.Random.Range(3f, 6f);
    }

    /// <summary>
    /// 异步屏幕空间雾气销毁协程：等待延迟后淡出销毁
    /// </summary>
    [HideFromIl2Cpp]
    private static IEnumerator ScreenFogDestroyRoutine(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        FogDriftUI.FogActive = false;
        yield return FadeOutAndDestroyScreenFog(go, 1.5f);
    }

    /// <summary>
    /// 屏幕空间 Canvas 淡出并销毁
    /// </summary>
    [HideFromIl2Cpp]
    private static IEnumerator FadeOutAndDestroyScreenFog(GameObject go, float duration)
    {
        if (go == null) yield break;

        var images = go.GetComponentsInChildren<UnityEngine.UI.Image>();
        if (images == null || images.Length == 0)
        {
            UnityEngine.Object.Destroy(go);
            yield break;
        }

        var startColors = new Color[images.Length];
        for (int i = 0; i < images.Length; i++)
            startColors[i] = images[i].color;

        for (var t = 0f; t < duration; t += Time.deltaTime)
        {
            var p = Mathf.Clamp01(t / duration);

            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null)
                {
                    var sc = startColors[i];
                    images[i].color = new Color(sc.r, sc.g, sc.b, sc.a * (1f - p));
                }
            }

            yield return null;
        }

        UnityEngine.Object.Destroy(go);
    }

    private static Sprite _fogSprite;

    /// <summary>
    /// 创建一个渐变圆形纹理，用于雾气 Sprite
    /// </summary>
    internal static Sprite CreateFogSprite()
    {
        if (_fogSprite != null) return _fogSprite;

        try
        {
            var size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];

            var center = new Vector2(size / 2f, size / 2f);
            var maxDist = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var dist = Vector2.Distance(new Vector2(x, y), center);
                    var alpha = Mathf.Clamp01(1f - (dist / maxDist));
                    alpha = alpha * alpha;  // 平方衰减，更柔和
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            _fogSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            _fogSprite.name = "FogSprite";

            Log.LogInfo("[Daiyousei] 雾气 Sprite 创建成功");
            return _fogSprite;
        }
        catch (Exception ex)
        {
            Log.LogError($"[Daiyousei] 创建雾气 Sprite 失败: {ex.Message}");
            return null;
        }
    }
}

// ================================================================================
// FogDriftUI：屏幕空间雾气飘动 MonoBehaviour（用于 ScreenSpaceOverlay Canvas）
// ================================================================================

public class FogDriftUI : MonoBehaviour
{
    public float DriftSpeed = 0.2f;
    public float DriftRange = 30f;
    public float Lifespan = 4f;
    public float FadeOutDuration = 0.8f;
    public static bool FogActive;

    private RectTransform _rt;
    private Vector2 _startPos;
    private float _timeOffset;
    private UnityEngine.UI.Image _img;
    private float _startAlpha;
    private float _elapsed;
    private bool _fadingOut;
    private bool _replaced;

    public FogDriftUI(IntPtr ptr) : base(ptr) { }

    void Start()
    {
        _rt = GetComponent<RectTransform>();
        if (_rt != null)
            _startPos = _rt.anchoredPosition;
        _timeOffset = UnityEngine.Random.Range(0f, 1000f);
        _img = GetComponent<UnityEngine.UI.Image>();
        _startAlpha = _img != null ? _img.color.a : 1f;
        _elapsed = 0f;
    }

    void Update()
    {
        if (_rt == null) return;

        _elapsed += Time.deltaTime;

        // 到达生命周期末尾 → 开始淡出
        if (_elapsed >= Lifespan && !_fadingOut)
            _fadingOut = true;

        // 淡出阶段
        if (_fadingOut)
        {
            var fadeProgress = Mathf.Clamp01((_elapsed - Lifespan) / FadeOutDuration);
            if (_img != null)
            {
                var c = _img.color;
                _img.color = new Color(c.r, c.g, c.b, _startAlpha * (1f - fadeProgress));
            }

            if (fadeProgress >= 1f && !_replaced)
            {
                _replaced = true;
                if (FogActive)
                    SpawnReplacement();
                Destroy(gameObject);
            }
            return;
        }

        // 正常飘动
        var t = Time.time + _timeOffset;
        var offsetX = Mathf.Sin(t * DriftSpeed) * DriftRange;
        var offsetY = Mathf.Cos(t * DriftSpeed * 0.7f) * DriftRange * 0.5f;
        _rt.anchoredPosition = _startPos + new Vector2(offsetX, offsetY);
    }

    void SpawnReplacement()
    {
        var parent = transform.parent;
        if (parent == null) return;
        Spell_Daiyousei.SpawnFogChild(parent);
    }
}
