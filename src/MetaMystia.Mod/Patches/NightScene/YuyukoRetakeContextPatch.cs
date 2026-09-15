#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using NightScene.GuestManagementUtility;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using Retake = GameData.Profile.YuyukoBossData.__c__DisplayClass16_6;

using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_6))]
[AutoLog]
public static partial class YuyukoRetakeContextPatch
{
    /// <summary>
    /// 对应 4.4.0e 重打版 YuyukoOverrideEvaluationCallback|50：好评扣除生命，差评触发吞厨具。
    /// 主机执行原版；客机重放时回填主机评价与附带状态，跳过本地扣血和额外吞食判定。
    /// </summary>
    [HarmonyPatch(nameof(Retake.Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_PDM_0))]
    [HarmonyPrefix]
    public static bool Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_PDM_0_Prefix(GuestGroupController thisGuestGroup, ref GuestGroupController.EvaluationResult __result,
        ref string message, ref bool comboProtect) =>
        YuyukoGuestSync.ReplayBossEvaluation(thisGuestGroup, ref __result, ref message, ref comboProtect) ? SkipOriginal : RunOriginal;

    /// <summary>
    /// 保存本体改判后的文本和连击保护，供评价消息使用。
    /// Prefix 跳过原版时仍执行本 Hook，此时保存的是主机重放值。
    /// </summary>
    [HarmonyPatch(nameof(Retake.Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_PDM_0))]
    [HarmonyPostfix]
    public static void Method_Internal_EvaluationResult_EvaluationResult_GuestGroupController_Boolean_byref_String_byref_Boolean_PDM_0_Postfix(GuestGroupController thisGuestGroup, string message, bool comboProtect) =>
        YuyukoGuestSync.CaptureBossEvaluation(thisGuestGroup, message, comboProtect);

    /// <summary>
    /// 对应 4.4.0e OnBuffEnd|42。原版先撤销倍率、执行已登记的锁清理并销毁特效。
    /// 随后结束同步侧的吞食处理，补清已加锁但尚未登记原版清理回调的目标；可与主循环收尾重复调用。
    /// </summary>
    [HarmonyPatch(nameof(Retake.Method_Internal_Void_PDM_0))]
    [HarmonyPostfix]
    public static void Method_Internal_Void_PDM_0_Postfix()
    {
        if (GameSession.HasPeers && PrepSceneManager.IsYuyukoChallenge) YuyukoGuestSync.EndPhase3();
    }
}
