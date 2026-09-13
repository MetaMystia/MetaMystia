#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using LockLoop = GameData.Profile.YuyukoBossData.__c__DisplayClass16_6.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObSpCoObObUnique;

namespace MetaMystia.Patch;

// 4.4.0e：<MainChallengeLoop>g__LockCookersYuyuko|41，state 1 选目标，state 2 实际锁定。
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_6.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObSpCoObObUnique))]
[AutoLog]
public static partial class YuyukoLockCookersPatch
{
    /// <summary>
    /// 保存本次恢复前的位置，供 Postfix 判断主机是否刚选定目标。
    /// 主机执行原版随机选择；客机仅执行收到主机指令的吞食，并在 state 1 使用主机目标。
    /// </summary>
    /// <param name="__instance">原版吞食协程。</param>
    /// <param name="__result">替代 MoveNext 的存活结果，等待为 true，结束为 false。</param>
    /// <param name="__state">Harmony 在本次 Prefix 与 Postfix 间传递的旧状态，不是额外的游戏状态。</param>
    /// <returns>是否执行原版方法，与协程是否结束是两个不同的判断。</returns>
    [HarmonyPatch(nameof(LockLoop.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(LockLoop __instance, ref bool __result, out int __state)
    {
        __state = __instance.__1__state;
        return YuyukoGuestSync.BeforeSwallowStep(__instance, ref __result) ? RunOriginal : SkipOriginal;
    }

    /// <summary>
    /// 结束本次中断标记；主机从 state 1 进入 2 时广播目标，此时已选定厨具但尚未执行实际锁定。
    /// Prefix 跳过原版也会进入本 Hook，客机重放不会因此再次广播。
    /// </summary>
    [HarmonyPatch(nameof(LockLoop.MoveNext))]
    [HarmonyPostfix]
    public static void MoveNext_Postfix(LockLoop __instance, int __state) =>
        YuyukoGuestSync.AfterSwallowStep(__instance, __state);
}
