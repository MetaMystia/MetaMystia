#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using GameData.Profile;
using NightScene.GuestManagementUtility;

using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0))]
[AutoLog]
public partial class YuyukoChallengeContextPatch
{
    /// <summary>
    /// 对应 4.4.0e 剧情版 YuyukoOverrideEvaluationCallback|33。
    /// 原版按菜酒等级改判并设置伤害倍率，强制连击保护，且可能返回有效的 Null 评价。
    /// 客机重放时直接回填主机结果、文本、保护与倍率；主机及非重放调用仍执行原版。
    /// </summary>
    [HarmonyPatch(nameof(YuyukoBossData.__c__DisplayClass16_0.Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_PDM_0))]
    [HarmonyPrefix]
    public static bool Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_PDM_0_Prefix(GuestGroupController __1, ref GuestGroupController.EvaluationResult __result,
        ref string message, ref bool comboProtect) =>
        YuyukoGuestSync.ReplayBossEvaluation(__1, ref __result, ref message, ref comboProtect) ? SkipOriginal : RunOriginal;

    /// <summary>
    /// 记录专用改判输出的文本和连击保护，供后续 PostEvaluation 入口组装主机评价消息。
    /// Prefix 替代结果后仍会调用本 Hook，客机记录的是已回填的主机输出，不重新改判。
    /// </summary>
    [HarmonyPatch(nameof(YuyukoBossData.__c__DisplayClass16_0.Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_PDM_0))]
    [HarmonyPostfix]
    public static void Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_PDM_0_Postfix(GuestGroupController __1, string message, bool comboProtect) =>
        YuyukoGuestSync.CaptureBossEvaluation(__1, message, comboProtect);

    /// <summary>
    /// 对应 4.4.0e MainChallengeLoop 的 Timing|2 入口（VA 0x18078CA80），三个阶段共用。
    /// 联机下按构建配置延长当前阶段时长：Debug 为原时长的 64 倍，Release 为 2 倍。
    /// 此处设置时长及三阶段提示；计时结束是否放行由 YuyukoTimingPatch 处理。
    /// </summary>
    [HarmonyPatch(nameof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.Method_Internal_IEnumerator_Func_1_Boolean_0))]
    [HarmonyPrefix]
    public static void Method_Internal_IEnumerator_Func_1_Boolean_0_Prefix(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0 __instance)
    {
        if (!MpManager.IsConnected) return;

        if (PrepSceneManager.YuyukoPrepRound == 3)
            InGameConsole.ShowPassive(TextId.YuyukoPhase3PatientExtended.Get());

        int originalDuration = __instance.__4__this.singleRoundDuration;
#if DEBUG
        __instance.thisSingleRoundDuration = originalDuration * 64;
#else
        __instance.thisSingleRoundDuration = originalDuration * 2;
#endif
        Log.Info($"幽幽子试炼本阶段时长：{originalDuration}s → {__instance.thisSingleRoundDuration}s");
    }
}
