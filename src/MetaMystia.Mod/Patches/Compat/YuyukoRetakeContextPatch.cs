#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using NightScene.GuestManagementUtility;

// 4.4.0e：挑战重打上下文（4.3.x 布局记作 __c__DisplayClass16_5），其改判回调是
// <MainChallengeLoop>g__YuyukoOverrideEvaluationCallback|50。
using Retake = GameData.Profile.YuyukoBossData.__c__DisplayClass16_6;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

/// <summary>
/// 重打版挑战的改判回调在客机上的重放。
/// <para>
/// 缺口说明同 <see cref="YuyukoChallengeContextPatch"/>：框架的评价 seam 不带回调的覆盖台词与闭包倍率，
/// 而重打版的回调还会因好评扣血、因差评触发吞厨具，客机必须整体跳过原回调。收尾部分（<c>OnBuffEnd|42</c>）
/// 已由框架的 <c>OnChallengeBuffEnded</c> 承担，见 <see cref="YuyukoChallengeSync"/>，不再保留 Hook。
/// </para>
/// </summary>
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_6))]
[AutoLog]
public static partial class YuyukoRetakeContextPatch
{
    private const string EvaluationCallback =
        nameof(Retake.Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_0);

    /// <summary>
    /// 原版好评扣除生命，差评触发吞厨具。主机执行原版；客机重放时回填主机评价与附带状态，
    /// 跳过本地扣血和额外吞食判定。
    /// </summary>
    [HarmonyPatch(EvaluationCallback)]
    [HarmonyPrefix]
    public static bool EvaluationCallback_Prefix(GuestGroupController thisGuestGroup, ref GuestGroupController.EvaluationResult __result,
        ref string message, ref bool comboProtect) =>
        YuyukoGuestSync.ReplayBossEvaluation(thisGuestGroup, ref __result, ref message, ref comboProtect) ? SkipOriginal : RunOriginal;

    /// <summary>
    /// 保存本体改判后的文本和连击保护，供评价消息使用。
    /// Prefix 跳过原版时仍执行本 Hook，此时保存的是主机重放值。
    /// </summary>
    [HarmonyPatch(EvaluationCallback)]
    [HarmonyPostfix]
    public static void EvaluationCallback_Postfix(GuestGroupController thisGuestGroup, string message, bool comboProtect) =>
        YuyukoGuestSync.CaptureBossEvaluation(thisGuestGroup, message, comboProtect);
}
