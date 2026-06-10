using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

using NightScene.UI;
using NightScene.UI.CookingUtility;
using NightScene.CookingUtility;

using MetaMystia.ResourceEx.SpellCollection;
using SgrYuki.Utils;

namespace MetaMystia.Patch;


// ============================================================================
// Patch 类 1: OpenCookingSelectionPannel Prefix — 在来源处替换 cookController
// ============================================================================

/// <summary>
/// 小恶魔黑卡 — 在 OpenCookingSelectionPannel 的 Prefix 中用 ref 替换 cookController 参数。
/// cookController 是普通 class 引用（不在 Il2Cpp Nullable 内），可以安全操作。
/// 替换后后续流程会自然使用目标厨具：PannelOpenContext 构造函数、OnPanelOpen 渲染等。
/// </summary>
[HarmonyPatch(typeof(WorkSceneSustainedPannel), nameof(WorkSceneSustainedPannel.OpenCookingSelectionPannel))]
[AutoLog]
public partial class KoakumaCookingPatch_OpenCookingSelectionPannel
{
    /// <summary>
    /// Prefix：用 ref 替换 cookController 参数为目标随机厨具。
    /// </summary>
    [HarmonyPrefix]
    public static bool Prefix(ref CookController cookController, float setIngredientFieldAlpha, float setRecipeFieldAlpha)
    {
        if (!Spell_Koakuma.IsChaosActive) return true;

        var targetCC = KoakumaCookwareChaosPatchHelper.FindRandomIdleController(cookController);
        if (targetCC == null)
        {
            UnityEngine.Debug.LogWarning("[Koakuma] OpenCookingSelectionPannel: 未找到可用的随机目标厨具，使用原始厨具");
            return true;
        }

        UnityEngine.Debug.Log($"[Koakuma] OpenCookingSelectionPannel 重定向: GridIndex={cookController.GridIndex} → GridIndex={targetCC.GridIndex}");
        cookController = targetCC;
        return true;
    }
}

// ============================================================================
// Patch 类 2: UpdateAllVisual Postfix — 栏位重排
// ============================================================================

[HarmonyPatch(typeof(WorkSceneCookingSelectionPannel), "UpdateAllVisual")]
[AutoLog]
public partial class KoakumaCookingPatch_UpdateAllVisual
{
    [HarmonyPostfix]
    public static void Postfix(WorkSceneCookingSelectionPannel __instance)
    {
        KoakumaCookingPatchHelper.ReorderCategoryColumns(__instance);
    }
}

// ============================================================================
// 共享辅助类
// ============================================================================

/// <summary>
/// 小恶魔黑卡食材栏位重排 — 共享逻辑
/// </summary>
[AutoLog]
public static partial class KoakumaCookingPatchHelper
{
    // ========================================================================
    // 栏位重排：将4个食材分类随机排列
    // ========================================================================

    private static readonly System.Random _shuffleRng = new();

    /// <summary>
    /// 随机交换食材数据源 List 的引用来改变栏位显示顺序。
    /// 每次面板刷新时重新洗牌，4个分类完全随机排列。
    /// </summary>
    public static void ReorderCategoryColumns(WorkSceneCookingSelectionPannel panel)
    {
        if (!Spell_Koakuma.IsChaosActive) return;

        try
        {
            // 通过 Reflection 获取4个食材列表的引用
            var seafood = GetPrivateMemberValue(panel, "m_Ingredient_SeaFoodInstances");
            var meat = GetPrivateMemberValue(panel, "m_Ingredient_MeatInstances");
            var veggies = GetPrivateMemberValue(panel, "m_Ingredient_VeggiesInsatance");
            var other = GetPrivateMemberValue(panel, "m_Ingredient_OtherInstances");

            if (seafood == null || meat == null || veggies == null || other == null)
            {
                UnityEngine.Debug.LogWarning("[Koakuma] 黑卡：无法读取全部食材列表，跳过重排");
                Log.LogWarning("[Koakuma] 黑卡：无法读取全部食材列表，跳过重排");
                return;
            }

            // Fisher-Yates 洗牌：将4个列表引用随机排列
            var lists = new object[] { seafood, meat, veggies, other };
            var names = new[] { "海鲜", "肉类", "蔬菜", "其他" };
            var slots = new[] { "m_Ingredient_SeaFoodInstances", "m_Ingredient_MeatInstances",
                                "m_Ingredient_VeggiesInsatance", "m_Ingredient_OtherInstances" };

            // Fisher-Yates shuffle
            for (int i = lists.Length - 1; i > 0; i--)
            {
                int j = _shuffleRng.Next(i + 1);
                (lists[i], lists[j]) = (lists[j], lists[i]);
            }

            // 构建日志
            var orderNames = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                for (int n = 0; n < 4; n++)
                {
                    if (lists[i] == seafood) { orderNames.Add("海鲜"); break; }
                    if (lists[i] == meat) { orderNames.Add("肉类"); break; }
                    if (lists[i] == veggies) { orderNames.Add("蔬菜"); break; }
                    if (lists[i] == other) { orderNames.Add("其他"); break; }
                }
            }

            // 将洗牌后的列表引用写回4个栏位
            SetPrivateMemberValue(panel, slots[0], lists[0]);
            SetPrivateMemberValue(panel, slots[1], lists[1]);
            SetPrivateMemberValue(panel, slots[2], lists[2]);
            SetPrivateMemberValue(panel, slots[3], lists[3]);

            Log.LogInfo($"[Koakuma] 黑卡重排：完成 — 随机顺序 [{string.Join("/", orderNames)}]");

            // 强制刷新 UI
            RefreshIngredientsGroup(panel);
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[Koakuma] 黑卡食材重排异常: {ex}");
            Log.LogError($"[Koakuma] 黑卡食材重排异常: {ex}");
        }
    }

    /// <summary>
    /// 通过 Reflection 设置实例的私有成员值（兼容 Il2CppInterop 属性映射）。
    /// </summary>
    public static void SetPrivateMemberValue(object obj, string memberName, object value)
    {
        var type = obj.GetType();

        // 尝试作为属性设置（Il2CppInterop 常见映射）
        var prop = type.GetProperty(memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(obj, value);
            return;
        }

        // 尝试作为字段设置
        var field = type.GetField(memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            field.SetValue(obj, value);
            return;
        }

        UnityEngine.Debug.LogWarning($"[Koakuma] SetPrivateMemberValue: 未找到可写入成员 {type.Name}.{memberName}");
    }

    /// <summary>
    /// 调用 StaticVerticalGridScrollListUILogicalGroupT.UpdateElements() 刷新 UI。
    /// </summary>
    private static void RefreshIngredientsGroup(WorkSceneCookingSelectionPannel panel)
    {
        try
        {
            var group = GetPrivateMemberValue(panel, "m_StaticIngredientsGroup");
            if (group == null) return;

            var updateMethod = group.GetType().GetMethod("UpdateElements",
                BindingFlags.Public | BindingFlags.Instance);
            updateMethod?.Invoke(group, null);
        }
        catch (Exception ex)
        {
            Log.LogWarning($"[Koakuma] 黑卡：UpdateElements 刷新异常（非致命）: {ex.Message}");
        }
    }

    // ========================================================================
    // 反射辅助
    // ========================================================================

    /// <summary>
    /// 获取实例的私有成员值，依次尝试字段和属性（兼容 Il2CppInterop
    /// 将游戏 IL2Cpp 私有字段映射为 C# 属性的情况）。
    /// 失败使用缓存避免重复刷屏。
    /// </summary>
    private static readonly HashSet<string> _reflectionFailCache = new HashSet<string>();

    private static object GetPrivateMemberValue(object obj, string memberName)
    {
        var type = obj.GetType();

        // 策略1: 尝试作为字段获取
        var field = type.GetField(memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            return field.GetValue(obj);

        // 策略2: 尝试作为属性获取（Il2CppInterop 常见映射方式）
        var prop = type.GetProperty(memberName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
            return prop.GetValue(obj);

        // 策略3: 尝试带 _ 前缀的名称
        string underscored = "_" + memberName;
        field = type.GetField(underscored,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            return field.GetValue(obj);

        prop = type.GetProperty(underscored,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null)
            return prop.GetValue(obj);

        // 全部失败 — 打印一次警告
        if (_reflectionFailCache.Add(memberName))
            Log.LogWarning($"[Koakuma] 反射: 未找到成员 {type.Name}.{memberName}");

        return null;
    }


}
