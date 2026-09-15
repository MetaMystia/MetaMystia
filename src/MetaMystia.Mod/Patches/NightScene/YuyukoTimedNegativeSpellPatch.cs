#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;
using UnityEngine;

using MetaMystia.Multiplayer;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

// <<MainChallengeLoop>g__Phase2TimedNegativeSpell|16>d
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb2))]
[AutoLog]
public partial class YuyukoTimedNegativeSpellPatch
{
    [HarmonyPatch(nameof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb2.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb2 __instance, ref bool __result)
    {
        if (!GameSession.HasPeers) return RunOriginal;

        // 原版收尾无条件 StopCoroutine，不能立即结束，否则 StartCoroutine 会返回 null。
        if (__instance.__2__current == null)
        {
            __instance.__2__current = new WaitForSeconds(1f);
            InGameConsole.ShowPassive(TextId.YuyukoTimedNegativeSpellDisabled.Get());
        }
        __result = true;
        return SkipOriginal;
    }
}
