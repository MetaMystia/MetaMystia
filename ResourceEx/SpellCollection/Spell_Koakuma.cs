using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage.Collections;
using NightScene.CookingUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using NightScene.UI.CookingUtility;
using SgrYuki;
using SgrYuki.Utils;

using BeverageStackList = Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.KeyValuePair<GameData.Core.Collections.Sellable, int>>;
using IngredientStackList = Il2CppSystem.Collections.Generic.List<Il2CppSystem.Collections.Generic.KeyValuePair<GameData.Core.Collections.Ingredient, int>>;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>
/// 小恶魔 Spell_Koakuma — 红卡：接下来 3 次稀客点单时提示订单内容。
///
/// 原理：
///   RedCard → RegisterCountedBuff(Koakuma, 3)
///   稀客点单 → Patch 触发 OnSpecialGuestOrder() → 显示通知 → Deduct
/// </summary>
[AutoLog]
public partial class Spell_Koakuma : SpellBase
{
    private const int PositiveBuffId = 9001;
    private const int NegativeBuffId = 9002;
    private const int NegativeDuration = 30;
    private const int MAX_CHARGES = 3;
    private static int _blackCardGeneration;
    private static bool _blackCardRemovingBuff;

    public override string OnGettingSpellOwnerIdentifier() => "Koakuma";

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext ctx)
        => PositiveBuffRoutine(ctx).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext ctx)
        => NegativeBuffRoutine(ctx).WrapToIl2Cpp();

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator PositiveBuffRoutine(SpellExecutionContext ctx)
    {
        // 注册 Buff 描述（$a 被游戏替换为剩余次数）
        var buffType = (EventManager.BuffType)9001;
        var desc = new GameData.CoreLanguage.ObjectLanguageBase(
            "灵符「遗失典籍的回响」",
            "小恶魔从图书馆搬来一本百科全书，接下来 $a 次稀客点单会提示具体内容",
            SpellBuffVisuals.GetBuffIconOrFallback(
                PositiveBuffId,
                "rex://ResourceExample/assets/Buff/9001_1.png"));
        DataBaseLanguage.BuffDescription[buffType] = desc;

        EventManager.Instance.RegisterCountedBuff(buffType, MAX_CHARGES, EventManager.MathOperation.Add, null, null);
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage($"小恶魔从图书馆搬来一本百科全书，接下来 {MAX_CHARGES} 次稀客点单会提示具体内容！");
        Log.Info($"[Spell_Koakuma] 注册 CountedBuff ×{MAX_CHARGES}");
        yield return null;
    }

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator NegativeBuffRoutine(SpellExecutionContext ctx)
    {
        var buffType = (EventManager.BuffType)NegativeBuffId;

        // 注册计时 Buff（会显示倒计时图标）
        var desc = new GameData.CoreLanguage.ObjectLanguageBase(
            "幻符「献给巴瓦鲁的镇魂曲」",
            "$a 秒内料理面板里的食材顺序被打乱，酒水柜里的酒水顺序被打乱，过滤功能不可用，交互的厨具变成随机厨具",
            SpellBuffVisuals.GetBuffIconOrFallback(
                NegativeBuffId,
                "rex://ResourceExample/assets/Buff/9001_2.png"));
        GameData.CoreLanguage.Collections.DataBaseLanguage.BuffDescription[buffType] = desc;

        RemoveBlackCardBuff();
        EventManager.Instance.RegisterTimedBuff(NegativeDuration, buffType, out var _, ((System.Action)CleanupBlackCard).ToIl2cppAction(), null, null);

        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("你所看到的一切全都是「真实」,不过…你却永远无法到达烹饪料理的「真实」！");
        Log.Info("[Spell_Koakuma] 黑卡发动");
        BeginBlackCard();

        var generation = _blackCardGeneration;
        if (PluginManager.Instance != null)
        {
            PluginManager.Instance.StartCoroutine(BlackCardDurationRoutine(generation).WrapToIl2Cpp());
        }
        else
        {
            Log.Warning("[Spell_Koakuma] PluginManager.Instance is null; ending black card immediately.");
            CleanupBlackCard();
            RemoveBlackCardBuff();
        }
        yield return null;
    }

    private static readonly System.Random _rng = new();
    private static readonly Dictionary<Vector3Int, Vector3Int> _cookerPositionRedirects = new();
    private static readonly Dictionary<int, GameObject> _hiddenFilterButtons = new();
    private static readonly Dictionary<int, bool> _hiddenFilterButtonOriginalStates = new();

    // 黑卡是否激活（用于 Harmony Patch）
    internal static bool IsBlackCardActive = false;

    [HideFromIl2Cpp]
    private static void BeginBlackCard()
    {
        _blackCardGeneration++;
        IsBlackCardActive = true;
        BuildCookerRedirects();
    }

    [HideFromIl2Cpp]
    private static void EndBlackCard()
    {
        _cookerPositionRedirects.Clear();
        RestoreFilterButtons();
        IsBlackCardActive = false;
    }

    [HideFromIl2Cpp]
    private static System.Collections.IEnumerator BlackCardDurationRoutine(int generation)
    {
        var endTime = Time.time + NegativeDuration;
        while (IsBlackCardActive && generation == _blackCardGeneration && Time.time < endTime)
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (IsBlackCardActive && generation == _blackCardGeneration)
        {
            CleanupBlackCard();
            RemoveBlackCardBuff();
        }
    }

    private static void CleanupBlackCard()
    {
        if (!IsBlackCardActive) return;

        _blackCardGeneration++;
        EndBlackCard();
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("符卡结束喵！不写符卡了喵！累死了喵！");
        Log.Info("[Spell_Koakuma] 黑卡效果结束");
    }

    private static void RemoveBlackCardBuff()
    {
        if (_blackCardRemovingBuff || EventManager.Instance == null) return;

        _blackCardRemovingBuff = true;
        EventManager.Instance.RemoveAllRegisteredTimedBuff((EventManager.BuffType)NegativeBuffId);
        _blackCardRemovingBuff = false;
    }

    [HideFromIl2Cpp]
    private static void BuildCookerRedirects()
    {
        _cookerPositionRedirects.Clear();

        var mgr = NightScene.CookingUtility.CookSystemManager.Instance;
        if (mgr == null)
        {
            Log.Info("[Spell_Koakuma] CookSystemManager 不存在，跳过随机厨具映射");
            return;
        }

        var allCookers = mgr.AllCookerControllers;
        if (allCookers == null)
        {
            Log.Info("[Spell_Koakuma] 厨具列表不存在，跳过随机厨具映射");
            return;
        }

        var cookerCount = Il2CppSystem.Linq.Enumerable.Count(allCookers);
        if (cookerCount < 2)
        {
            Log.Info("[Spell_Koakuma] 厨具数量不足，跳过随机厨具映射");
            return;
        }

        var cookers = new List<NightScene.CookingUtility.CookController>(cookerCount);
        for (int i = 0; i < cookerCount; i++)
            cookers.Add(Il2CppSystem.Linq.Enumerable.ElementAt(allCookers, i));

        var positions = cookers
            .Where(c => c != null && !c.IsEmptyDesk && c.Cooker != null && c.Cooker.Type != Cooker.CookerType.Empty)
            .Select(c => c.GridPosition)
            .Distinct()
            .ToList();

        if (positions.Count < 2)
        {
            Log.Info("[Spell_Koakuma] 可随机化的厨具不足，跳过随机厨具映射");
            return;
        }

        var targets = positions.ToList();
        ShuffleList(targets);
        AvoidFixedCookerMappings(positions, targets);

        for (int i = 0; i < positions.Count; i++)
            _cookerPositionRedirects[positions[i]] = targets[i];

        Log.Info($"[Spell_Koakuma] 厨具交互已随机映射 ({_cookerPositionRedirects.Count} 个)");
    }

    [HideFromIl2Cpp]
    private static void AvoidFixedCookerMappings(IReadOnlyList<Vector3Int> sources, List<Vector3Int> targets)
    {
        if (sources.Count < 2 || targets.Count < 2) return;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != sources[i]) continue;

            int swapIndex = (i + 1) % targets.Count;
            (targets[i], targets[swapIndex]) = (targets[swapIndex], targets[i]);
        }
    }

    [HideFromIl2Cpp]
    internal static bool TryRedirectCookerPosition(ref Vector3Int cookerPosition)
    {
        if (!IsBlackCardActive || _cookerPositionRedirects.Count == 0)
            return false;

        if (!_cookerPositionRedirects.TryGetValue(cookerPosition, out var redirected))
            return false;

        cookerPosition = redirected;
        return true;
    }

    [HideFromIl2Cpp]
    internal static bool ShuffleCookingIngredients(
        IngredientStackList seaFood,
        IngredientStackList meat,
        IngredientStackList veggies,
        IngredientStackList other)
    {
        if (!IsBlackCardActive)
            return false;
        if (seaFood == null || meat == null || veggies == null || other == null)
            return false;

        var lists = new[] { seaFood, meat, veggies, other };
        var counts = new int[lists.Length];
        var totalCount = 0;

        for (int i = 0; i < lists.Length; i++)
        {
            counts[i] = lists[i].Count;
            totalCount += counts[i];
        }

        if (totalCount < 2)
            return false;

        foreach (var list in lists)
            ShuffleIl2CppListInPlace(list);

        Log.Info($"[Spell_Koakuma] 食材数据顺序已打乱 ({totalCount} 个)");
        return true;
    }

    [HideFromIl2Cpp]
    internal static bool ShuffleBeverages(BeverageStackList beverages)
    {
        if (!IsBlackCardActive || beverages == null || beverages.Count < 2)
            return false;

        ShuffleIl2CppListInPlace(beverages);
        Log.Info($"[Spell_Koakuma] 酒水数据顺序已打乱 ({beverages.Count} 个)");
        return true;
    }

    [HideFromIl2Cpp]
    internal static void UpdateStorageFilterButton(WorkSceneStoragePannel panel)
    {
        if (panel == null || panel.filterButton == null)
            return;

        var buttonObject = panel.filterButton.gameObject;
        if (buttonObject == null)
            return;

        if (!IsBlackCardActive)
        {
            RestoreFilterButton(buttonObject);
            return;
        }

        int id = buttonObject.GetInstanceID();
        if (!_hiddenFilterButtons.ContainsKey(id))
        {
            _hiddenFilterButtons[id] = buttonObject;
            _hiddenFilterButtonOriginalStates[id] = buttonObject.activeSelf;
        }

        buttonObject.SetActive(false);
    }

    [HideFromIl2Cpp]
    private static void RestoreFilterButtons()
    {
        foreach (var pair in _hiddenFilterButtons.ToArray())
            RestoreFilterButton(pair.Value);

        _hiddenFilterButtons.Clear();
        _hiddenFilterButtonOriginalStates.Clear();
    }

    [HideFromIl2Cpp]
    private static void RestoreFilterButton(GameObject buttonObject)
    {
        if (buttonObject == null)
            return;

        int id = buttonObject.GetInstanceID();
        if (!_hiddenFilterButtonOriginalStates.TryGetValue(id, out var originalActive))
            return;

        buttonObject.SetActive(originalActive);
        _hiddenFilterButtons.Remove(id);
        _hiddenFilterButtonOriginalStates.Remove(id);
    }

    [HideFromIl2Cpp]
    private static void ShuffleIl2CppListInPlace<T>(Il2CppSystem.Collections.Generic.List<T> list)
    {
        if (list == null || list.Count < 2)
            return;

        for (int i = list.Count - 1; i > 0; i--)
        {
            int j;
            lock (_rng)
                j = _rng.Next(i + 1);

            SwapIl2CppListItems(list, j, i);
        }
    }

    [HideFromIl2Cpp]
    private static void SwapIl2CppListItems<T>(Il2CppSystem.Collections.Generic.List<T> list, int first, int second)
    {
        if (first == second)
            return;

        if (first > second)
            (first, second) = (second, first);

        int count = second - first + 1;
        list.Reverse(first, count);

        int middleCount = count - 2;
        if (middleCount > 1)
            list.Reverse(first + 1, middleCount);
    }

    [HideFromIl2Cpp]
    private static void ShuffleList<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j;
            lock (_rng)
                j = _rng.Next(i + 1);

            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ===================================================================
    //  静态方法 — 供 Harmony Patch 调用
    // ===================================================================

    public static bool HasBuff()
        => EventManager.Instance.CheckCountedBuffExists((EventManager.BuffType)PositiveBuffId);

    private static void Deduct()
        => EventManager.Instance.TryDeductCountedBuffValue((EventManager.BuffType)PositiveBuffId);

    /// <summary>
    /// 由 GuestsManager.GenerateOrderSession 的 Postfix 触发。
    /// 检查 Koakuma CountedBuff 是否存在，存在则显示订单详情并消耗一层。
    ///
    /// ⚠️ 稀客的 foodRequest/beverageRequest 是标签 ID（tag），不是具体物品 ID！
    ///    用 GetFoodTag() / GetBeverageTag() 转成标签名，别去查 DataBaseCore.Foods。
    /// </summary>
    public static void OnSpecialGuestOrder(GuestGroupController guestGroup)
    {
        if (!HasBuff()) return;

        var order = guestGroup.PeekOrders();
        if (order == null) return;

        var foodTag = order.foodRequest.GetFoodTag();
        var bevTag = order.beverageRequest.GetBeverageTag();
        var guestName = guestGroup.OnGetGuestName();

        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage($"{guestName} 想要「{foodTag}」标签的料理 + 「{bevTag}」标签的酒水");
        Log.Info($"[Spell_Koakuma] {guestName}: foodTag={foodTag} bevTag={bevTag}");
        Deduct();
    }
}

/// <summary>
/// 黑卡激活时拦截 WorkSceneStoragePannel.OpenFilterPanel，
/// 阻止键盘快捷键（Z 键）打开过滤面板。
/// </summary>
[HarmonyPatch(typeof(NightScene.UI.CookingUtility.WorkSceneStoragePannel), "OpenFilterPanel")]
public static class Spell_Koakuma_FilterBlockPatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (Spell_Koakuma.IsBlackCardActive)
        {
            //Log.Info("[Spell_Koakuma] Harmony 拦截: 黑卡生效中，阻止打开过滤面板");
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(NightScene.UI.CookingUtility.WorkSceneCookingSelectionPannel), "UpdateAllVisual")]
public static class Spell_Koakuma_IngredientShufflePatch
{
    [HarmonyPostfix]
    private static void Postfix(WorkSceneCookingSelectionPannel __instance)
    {
        if (__instance == null)
            return;

        if (!Spell_Koakuma.ShuffleCookingIngredients(
            __instance.m_Ingredient_SeaFoodInstances,
            __instance.m_Ingredient_MeatInstances,
            __instance.m_Ingredient_VeggiesInsatance,
            __instance.m_Ingredient_OtherInstances))
        {
            return;
        }

        __instance.m_StaticIngredientsGroup?.UpdateElements();
    }
}

[HarmonyPatch(typeof(NightScene.UI.CookingUtility.WorkSceneStoragePannel), "UpdateBevField")]
public static class Spell_Koakuma_BeverageShufflePatch
{
    [HarmonyPostfix]
    private static void Postfix(WorkSceneStoragePannel __instance)
    {
        if (__instance == null)
            return;

        Spell_Koakuma.ShuffleBeverages(__instance.m_Beverages);
    }
}

[HarmonyPatch(typeof(NightScene.UI.CookingUtility.WorkSceneStoragePannel), "OnPanelOpen")]
public static class Spell_Koakuma_StorageFilterButtonPatch
{
    [HarmonyPostfix]
    private static void Postfix(WorkSceneStoragePannel __instance)
    {
        Spell_Koakuma.UpdateStorageFilterButton(__instance);
    }
}

[HarmonyPatch(typeof(NightScene.UI.CookingUtility.WorkSceneStoragePannel), "OnPanelClose")]
public static class Spell_Koakuma_StorageFilterButtonRestorePatch
{
    [HarmonyPrefix]
    private static void Prefix(WorkSceneStoragePannel __instance)
    {
        Spell_Koakuma.UpdateStorageFilterButton(__instance);
    }
}

[HarmonyPatch(typeof(NightScene.CookingUtility.CookSystemManager))]
public static class Spell_Koakuma_CookerRedirectPatch
{
    [HarmonyPatch(nameof(NightScene.CookingUtility.CookSystemManager.CallCooker))]
    [HarmonyPrefix]
    private static void CallCooker_Prefix(ref Vector3Int cookerPosition)
    {
        Spell_Koakuma.TryRedirectCookerPosition(ref cookerPosition);
    }

    [HarmonyPatch(nameof(NightScene.CookingUtility.CookSystemManager.GetCooker), new[] { typeof(Vector3Int) })]
    [HarmonyPrefix]
    private static void GetCooker_Prefix(ref Vector3Int cookerPosition)
    {
        Spell_Koakuma.TryRedirectCookerPosition(ref cookerPosition);
    }
}
