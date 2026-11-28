#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using MainLoop = GameData.Profile.DLC3_MausoleumCuisineCompetitionBossData._MainChallengeLoop_d__59;

namespace MetaMystia.Patch;

// 4.4.0e 神灵庙料理大赛 MainChallengeLoop.MoveNext，状态见料理对决设计文档。
[HarmonyPatch(typeof(GameData.Profile.DLC3_MausoleumCuisineCompetitionBossData._MainChallengeLoop_d__59))]
public static class MausoleumMainLoopPatch
{
    /// <summary>未就绪时不执行本次原版 MoveNext，不改变恢复位置，以 null 等待下一帧。</summary>
    [HarmonyPatch(nameof(MainLoop.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(MainLoop __instance, ref bool __result, out int __state)
    {
        __state = __instance.__1__state;
        if (VersusManager.BeforeLoopStep(__instance)) return RunOriginal;
        __instance.__2__current = null;
        __result = true;
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(MainLoop.MoveNext))]
    [HarmonyPostfix]
    public static void MoveNext_Postfix(MainLoop __instance, int __state, bool __runOriginal)
    {
        if (__runOriginal) VersusManager.AfterLoopStep(__instance, __state);
    }
}
