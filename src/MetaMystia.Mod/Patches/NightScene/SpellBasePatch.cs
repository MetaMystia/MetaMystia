using HarmonyLib;

using GameData.Core.Collections.NightSceneUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Multiplayer.Spells;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Core.Collections.NightSceneUtility.SpellBase))]
[AutoLog]
public static partial class SpellBasePatch
{
    [HarmonyPatch(nameof(SpellBase.SchedulePositiveBuffExecution))]
    [HarmonyPrefix]
    public static bool SchedulePositiveBuffExecution_Prefix(SpellExecutionContext spellExecutionContext)
    {
        if (RunsLocally(spellExecutionContext)) return RunOriginal;
        if (EventManager.Instance.CanQueueSpell)
            GuestsManager.Instance.OnPositiveSpellTriggered?.Invoke(spellExecutionContext.GuestsController);
        spellExecutionContext.OnFinishCallback?.Invoke();
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(SpellBase.ScheduleNegativeBuffExecution))]
    [HarmonyPrefix]
    public static bool ScheduleNegativeBuffExecution_Prefix(SpellExecutionContext spellExecutionContext, out int __state)
    {
        __state = EventManager.Instance.TryGetCountedBuffValue(EventManager.BuffType.Skill_Reimu_Shield);
        if (RunsLocally(spellExecutionContext)) return RunOriginal;
        spellExecutionContext.OnFinishCallback?.Invoke();
        return SkipOriginal;
    }

    /// <summary>主机护盾抵消后通知客机。</summary>
    [HarmonyPatch(nameof(SpellBase.ScheduleNegativeBuffExecution))]
    [HarmonyPostfix]
    public static void ScheduleNegativeBuffExecution_Postfix(SpellExecutionContext spellExecutionContext, int __state)
    {
        if (GuestRegistry.Mode(spellExecutionContext.GuestsController) == GuestMode.Host
            && EventManager.Instance.TryGetCountedBuffValue(EventManager.BuffType.Skill_Reimu_Shield) < __state)
            SpellDefendedMessage.Send();
    }

    [HarmonyPatch(nameof(SpellBase.ScheduleOnLeaveBuffExecution))]
    [HarmonyPrefix]
    public static bool ScheduleOnLeaveBuffExecution_Prefix(SpellExecutionContext spellExecutionContext)
    {
        if (RunsLocally(spellExecutionContext)) return RunOriginal;
        spellExecutionContext.OnFinishCallback?.Invoke();
        return SkipOriginal;
    }

    /// <summary>客机重放的评价与离场不自行触发符卡，等待主机广播；回调照常立即结束。</summary>
    private static bool RunsLocally(SpellExecutionContext context) =>
        GuestRegistry.Mode(context.GuestsController) != GuestMode.Client;

    [HarmonyPatch(nameof(SpellBase.ExecutePositiveBuff))]
    [HarmonyPostfix]
    public static void ExecutePositiveBuff_Postfix(SpellBase __instance, SpellExecutionContext spellExecutionContext,
        ref Il2CppSystem.Collections.IEnumerator __result) =>
        SpellRegistry.Current.Wrap(__instance, SpellKind.Positive, spellExecutionContext, ref __result);

    [HarmonyPatch(nameof(SpellBase.ExecuteNegativeBuff))]
    [HarmonyPostfix]
    public static void ExecuteNegativeBuff_Postfix(SpellBase __instance, SpellExecutionContext spellExecutionContext,
        ref Il2CppSystem.Collections.IEnumerator __result) =>
        SpellRegistry.Current.Wrap(__instance, SpellKind.Negative, spellExecutionContext, ref __result);

    [HarmonyPatch(nameof(SpellBase.ExecuteOnLeaveBuff))]
    [HarmonyPostfix]
    public static void ExecuteOnLeaveBuff_Postfix(SpellBase __instance, SpellExecutionContext spellExecutionContext,
        ref Il2CppSystem.Collections.IEnumerator __result) =>
        SpellRegistry.Current.Wrap(__instance, SpellKind.Leave, spellExecutionContext, ref __result);
}
