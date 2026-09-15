using HarmonyLib;

using GameData.RunTime.Common;

namespace MetaMystia.Patch;

/// <summary>
/// 小鸽子挂坠白天移速的响应式触发：装饰在白天任意时刻被装备/卸下（展示柜勾选或 /decor use）都会即时生效/撤销。
/// 解决「仅在 DayScene 场景初始化时判断一次」导致开发期先入场景再 /decor use 时加成不生效的问题。
/// </summary>
[HarmonyPatch(typeof(RunTimeAlbum))]
[AutoLog]
public static class DovePendantDecorationDayScenePatch
{
    [HarmonyPatch(nameof(RunTimeAlbum.TryRecordUsedDecoration))]
    [HarmonyPostfix]
    public static void TryRecordUsedDecoration_Postfix(int decorationId)
    {
        if (decorationId == ResourceExManager.DovePendantDecorationId)
        {
            ResourceExManager.ApplyDovePendantDaytimeSpeed();
        }
    }

    [HarmonyPatch(nameof(RunTimeAlbum.TryRemoveUsedDecoration))]
    [HarmonyPostfix]
    public static void TryRemoveUsedDecoration_Postfix(int decorationId)
    {
        if (decorationId == ResourceExManager.DovePendantDecorationId)
        {
            ResourceExManager.RemoveDovePendantDaytimeSpeed();
        }
    }
}
