using HarmonyLib;

using GameData.RunTime.Common;
using NightScene.EventUtility;

namespace MetaMystia.Patch;

/// <summary>
/// 小鸽子挂坠移速的响应式触发：装饰在任意时刻被装备/卸下（展示柜勾选或 /decor use）都会即时生效/撤销。
/// 白天走白天加成，夜间走夜间加成（含伙伴效率/移速），避免「仅在场景初始化判断一次」导致延迟装备不生效。
/// </summary>
[HarmonyPatch(typeof(RunTimeAlbum))]
[AutoLog]
public static class DovePendantDecorationDayScenePatch
{
    [HarmonyPatch(nameof(RunTimeAlbum.TryRecordUsedDecoration))]
    [HarmonyPostfix]
    public static void TryRecordUsedDecoration_Postfix(int decorationId)
    {
        if (decorationId != ResourceExManager.DovePendantDecorationId) return;

        if (PlayerManager.LocalIsDayOver)
        {
            ResourceExManager.ApplyDovePendantNightBuffs(EventManager.Instance);
        }
        else
        {
            ResourceExManager.ApplyDovePendantDaytimeSpeed();
        }
    }

    [HarmonyPatch(nameof(RunTimeAlbum.TryRemoveUsedDecoration))]
    [HarmonyPostfix]
    public static void TryRemoveUsedDecoration_Postfix(int decorationId)
    {
        if (decorationId != ResourceExManager.DovePendantDecorationId) return;

        ResourceExManager.RemoveDovePendantDaytimeSpeed();
        ResourceExManager.RemoveDovePendantNightBuffs();
    }
}
