using HarmonyLib;
using UnityEngine;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using SpawnLoop = GameData.Profile.YuyukoBossData.__c__DisplayClass16_6.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb0;

namespace MetaMystia.Patch;

// 4.4.0e：<MainChallengeLoop>g__Phase3GuestSpawnLoop|43。
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_6.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObWaVoObMoInVoBoOb0))]
[AutoLog]
public static partial class YuyukoPhase3GuestSpawnPatch
{
    /// <summary>
    /// 客机将三阶段分身生成循环保持为等待，分身由主机生成并通过普通顾客同步接入。
    /// 拦截整个循环，避免只拦生成后原循环仍访问未生成的客群或安装主机专用回调。
    /// 同时允许本体原版订单循环准备本地回调；实际安装仍等待主机订单。
    /// </summary>
    /// <remarks>
    /// __result 为 true 保持协程存活，由原版挑战收尾停止；一秒等待仅用于避免客机逐帧空转。
    /// 非客机或非联机挑战直接放行原版。
    /// </remarks>
    [HarmonyPatch(nameof(SpawnLoop.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(SpawnLoop __instance, ref bool __result)
    {
        if (!MpManager.IsConnected || !MpManager.IsRoomClient || !PrepSceneManager.IsYuyukoChallenge) return RunOriginal;
        // 本体可提前准备本地回调，真正安装哪一单仍由主机消息决定。
        __instance.__4__this.ifYuyukoCouldOrder = true;
        __instance.__2__current ??= new WaitForSeconds(1f);
        __result = true;
        return SkipOriginal;
    }
}
