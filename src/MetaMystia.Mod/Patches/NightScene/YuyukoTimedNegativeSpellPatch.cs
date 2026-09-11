using HarmonyLib;

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
    public static bool MoveNext_Prefix(ref bool __result)
    {
        if (!MpManager.IsConnected) return RunOriginal;

        Log.Info("幽幽子最终试炼联机中，阶段二的定时惩罚符卡循环被禁用");
        __result = false;
        return SkipOriginal;
    }
}
