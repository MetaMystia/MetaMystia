using System.Collections.Generic;

using HarmonyLib;

using NightScene.UI.EventUtility;

using SgrYuki.Utils;

using UnityEngine;

namespace MetaMystia.Patch;

/// <summary>
/// BuffElement 的两个 Postfix：
/// InitializeVisual — 设置初始描述 + 记录总时长
/// </summary>
[HarmonyPatch(typeof(BuffElement))]
[AutoLog]
public partial class BuffElementDescriptionPatch
{
    /// <summary>
    /// BuffElement instance ID → 总持续秒数（仅自定义定时 buff 有条目）
    /// </summary>
    static readonly Dictionary<int, float> _instanceDurations = new();

    [HarmonyPostfix]
    [HarmonyPatch("InitializeVisual")]
    public static void InitializeVisual_Postfix(BuffElement __instance)
    {
        if (__instance.description != null) return;
        var pending = BuffInitialDescriptionPatch.PendingDescription;
        __instance.description = pending ?? " ";
        BuffInitialDescriptionPatch.PendingDescription = null;

        var dur = BuffInitialDescriptionPatch.PendingDuration;
        if (dur.HasValue)
        {
            _instanceDurations[__instance.GetInstanceID()] = dur.Value;
            BuffInitialDescriptionPatch.PendingDuration = null;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch("UpdateBuff", typeof(string), typeof(float))]
    public static void UpdateBuff_Postfix(BuffElement __instance, float progress)
    {
        var id = __instance.GetInstanceID();
        if (!_instanceDurations.TryGetValue(id, out var totalDuration)) return;

        var remaining = Mathf.CeilToInt(totalDuration * (1f - progress));
        if (remaining <= 0)
        {
            __instance.count.text = string.Empty;
            __instance.altCount.text = string.Empty;
            _instanceDurations.Remove(id);
            return;
        }

        __instance.count.text = remaining.ToString();
        __instance.altCount.text = remaining.ToString();
    }
}
