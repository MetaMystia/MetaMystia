using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

using GameData.CoreLanguage.Collections;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using SgrYuki.Utils;

using static NightScene.GuestManagementUtility.GuestsManager;

namespace MetaMystia.Patch;

/// <summary>
/// 小恶魔红卡「灵符【遗失典籍的回响】」的 tag 揭示补丁。
/// 拦截稀客订单生成，读取 foodRequest/beverageRequest tag，
/// 并在传菜界面（GetOrderBevText）覆写文本附加 tag 信息。
///
/// 注：tag 类稀客订单不会调用 GetOrderFoodText（仅显示 tag 名本身），
///     因此将所有 tag 揭示信息统一附加到 GetOrderBevText 返回值上。
/// </summary>
[HarmonyPatch]
[AutoLog]
public partial class KoakumaOrderRevealPatch
{
    private const int KoakumaEchoBuffType = 101;
    private const string TagColor = "#42A5F5"; // 蓝色

    /// <summary>
    /// 缓存：DeskCode → (foodTagName, bevTagName)
    /// </summary>
    private static readonly Dictionary<int, (string foodTag, string bevTag)> _tagCache = new();

    // ========================================================================
    // Part 1: 订单生成拦截 — 缓存 tag + 扣减计数
    // ========================================================================

    [HarmonyPatch(typeof(GuestsManager.__c__DisplayClass174_0),
        nameof(GuestsManager.__c__DisplayClass174_0.Method_Internal_OrderGenerationResult_GuestGroupController_byref_OrderBase_0))]
    [HarmonyPostfix]
    public static void GenerateOrderInternal_Postfix(
        OrderGenerationResult __result,
        GuestGroupController toGenerate,
        ref GuestsManager.OrderBase orderData)
    {
        var em = EventManager.Instance;
        if (em == null) return;
        if (!em.CheckCountedBuffExists((EventManager.BuffType)KoakumaEchoBuffType)) return;
        if (__result != OrderGenerationResult.Succeed) return;
        if (toGenerate.ControllType != GuestType.Special) return;

        try
        {
            var foodTagId = orderData.foodRequest;
            var bevTagId = orderData.beverageRequest;

            string foodTagName = null;
            string bevTagName = null;

            if (foodTagId != 0)
            {
                try { foodTagName = DataBaseLanguage.FoodTags[foodTagId]; }
                catch { foodTagName = $"#{foodTagId}"; }
            }

            if (bevTagId != 0)
            {
                try { bevTagName = DataBaseLanguage.BeverageTags[bevTagId]; }
                catch { bevTagName = $"#{bevTagId}"; }
            }

            if (foodTagName == null && bevTagName == null)
            {
                Log.LogWarning("[Koakuma] 红卡：订单无有效 tag，跳过揭示");
                return;
            }

            int deskCode = orderData.DeskCode;
            _tagCache[deskCode] = (foodTagName, bevTagName);
            Log.LogInfo($"[Koakuma] 红卡：缓存 tag DeskCode={deskCode} food=\"{foodTagName}\" bev=\"{bevTagName}\"");

            // 扣减计数
            em.TryDeductCountedBuffValue((EventManager.BuffType)KoakumaEchoBuffType);
        }
        catch (Exception ex)
        {
            Log.LogError($"[Koakuma] 红卡揭示异常: {ex.Message}");
        }
    }

    // ========================================================================
    // Part 2: 传菜界面文本覆写
    // ========================================================================

    /// <summary>
    /// GetOrderBevText — tag 类稀客订单的主要文本显示入口。
    /// 同时附加食物 tag 和饮品 tag 的揭示信息（蓝色）。
    /// </summary>
    [HarmonyPatch(typeof(SpecialGuestsController), "GetOrderBevText")]
    [HarmonyPostfix]
    public static void GetOrderBevText_Postfix(ref string __result, SpecialOrder specialOrder)
    {
        if (specialOrder == null) return;

        int deskCode = specialOrder.DeskCode;
        if (!_tagCache.TryGetValue(deskCode, out var tags)) return;

        // 构建 tag 揭示文本
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(tags.foodTag))
            parts.Add($"料理tag：{tags.foodTag}");
        if (!string.IsNullOrEmpty(tags.bevTag))
            parts.Add($"饮品tag：{tags.bevTag}");

        if (parts.Count == 0) return;

        var tagInfo = string.Join("，", parts);
        __result = $"{__result}\n<color={TagColor}>[小恶魔查到：{tagInfo}]</color>";
        Log.LogInfo($"[Koakuma] 红卡：传菜界面附加 tag \"{tagInfo}\" (DeskCode={deskCode})");
    }
}
