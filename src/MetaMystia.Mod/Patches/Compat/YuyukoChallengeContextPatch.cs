#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using GameData.Profile;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

/// <summary>
/// 剧情版挑战的改判回调（<c>YuyukoOverrideEvaluationCallback|33</c>）在客机上的重放。
/// <para>
/// 缺口说明：框架的评价 seam（<c>OnPreBossEvaluated</c>/<c>OnBossEvaluated</c>）能给出并改写最终评价与
/// 连击保护，但回调自身的两个出参——覆盖台词与挑战闭包里的伤害倍率——不在 seam 的形状里，客机重放时
/// 需要它们与主机完全一致；此外回调还会按菜酒等级设置倍率，这些都属于闭包内部状态。
/// 因此本文件保留为兼容缺口，只做「回填主机结果 / 记录本机结果」，阶段时长改由挑战服务承担，见
/// <see cref="YuyukoChallengeSync"/>。
/// </para>
/// </summary>
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0))]
[AutoLog]
public partial class YuyukoChallengeContextPatch
{
    private const string EvaluationCallback =
        nameof(YuyukoBossData.__c__DisplayClass16_0.Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_0);

    /// <summary>
    /// 原版按菜酒等级改判并设置伤害倍率，强制连击保护，且可能返回有效的 Null 评价。
    /// 客机重放时直接回填主机结果、文本、保护与倍率；主机及非重放调用仍执行原版。
    /// </summary>
    [HarmonyPatch(EvaluationCallback)]
    [HarmonyPrefix]
    public static bool EvaluationCallback_Prefix(GuestGroupController __1, ref GuestGroupController.EvaluationResult __result,
        ref string message, ref bool comboProtect) =>
        YuyukoGuestSync.ReplayBossEvaluation(__1, ref __result, ref message, ref comboProtect) ? SkipOriginal : RunOriginal;

    /// <summary>
    /// 记录专用改判输出的文本和连击保护，供后续 PostEvaluation 入口组装主机评价消息。
    /// Prefix 替代结果后仍会调用本 Hook，客机记录的是已回填的主机输出，不重新改判。
    /// </summary>
    [HarmonyPatch(EvaluationCallback)]
    [HarmonyPostfix]
    public static void EvaluationCallback_Postfix(GuestGroupController __1, string message, bool comboProtect) =>
        YuyukoGuestSync.CaptureBossEvaluation(__1, message, comboProtect);
}
