using HarmonyLib;
using UnityEngine;

using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using TimedNegativeSpell = GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb2;

namespace MetaMystia.Patch;

// <<MainChallengeLoop>g__Phase2TimedNegativeSpell|16>d
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb2))]
[AutoLog]
public partial class YuyukoTimedNegativeSpellPatch
{
    [HarmonyPatch(nameof(TimedNegativeSpell.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(TimedNegativeSpell __instance, ref bool __result)
    {
        if (!MpManager.IsConnected) return RunOriginal;

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
