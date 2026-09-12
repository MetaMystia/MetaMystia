using HarmonyLib;
using UnityEngine;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

// <<MainChallengeLoop>g__Phase2GuestSpawnLoop|15>d，MoveNext VA 0x18078B480（4.3.0C）。
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb1))]
[AutoLog]
public partial class YuyukoPhase2GuestSpawnPatch
{
    [HarmonyPatch(nameof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb1.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb1 __instance, ref bool __result)
    {
        if (!MpManager.IsConnected || !MpManager.IsRoomClient) return RunOriginal;

        // 客机由出生消息创建顾客；保留等待协程，供原版二阶段收尾正常停止。
        __instance.__2__current ??= new WaitForSeconds(1f);
        __result = true;
        return SkipOriginal;
    }
}
