using HarmonyLib;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using MainLoop = GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16;

namespace MetaMystia.Patch;

// 4.4.0e MainChallengeLoop.MoveNext，阶段结束后的恢复点，见同步审计文档。
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16))]
[AutoLog]
public static partial class YuyukoMainLoopPatch
{
    /// <summary>
    /// 在原版主循环恢复前检查阶段数据：一阶段主机先清场，其他阶段发布依据；客机等待并回填消息。
    /// 未就绪时不执行本次原版 MoveNext，不改变恢复位置，以 null 等待下一帧。
    /// 返回给协程的结果仍为 true，表示等待而非结束挑战或跳过阶段。
    /// </summary>
    [HarmonyPatch(nameof(MainLoop.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(MainLoop __instance, ref bool __result, out int __state)
    {
        __state = __instance.__1__state;
        if (YuyukoGuestSync.BeforeMainStep(__instance)) return RunOriginal;
        __instance.__2__current = null;
        __result = true;
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(MainLoop.MoveNext))]
    [HarmonyPostfix]
    public static void MoveNext_Postfix(MainLoop __instance, int __state, bool __runOriginal)
    {
        if (__runOriginal) YuyukoGuestSync.AfterMainStep(__instance, __state);
    }
}
