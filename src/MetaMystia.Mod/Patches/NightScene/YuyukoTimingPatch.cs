#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using TimingLoop = GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObFu1BoexSiInObObUnique;

using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

// 4.4.0e：<MainChallengeLoop>g__Timing|2。保留本地计时显示，由主机结束消息放行。
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObFu1BoexSiInObObUnique))]
[AutoLog]
public static partial class YuyukoTimingPatch
{
    /// <summary>
    /// 客机已收到当前主循环恢复位置的主机阶段消息时，结束本地计时协程，让主循环继续核对阶段数据。
    /// __result 为 false 表示计时协程结束，不表示挑战失败；没有消息时仍执行原版以保留本地计时显示。
    /// </summary>
    [HarmonyPatch(nameof(TimingLoop.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(TimingLoop __instance, ref bool __result)
    {
        if (!GameSession.HasPeers || !GameSession.IsRoomClient || !PrepSceneManager.IsYuyukoChallenge
            || !YuyukoGuestSync.HasPhaseEnd(YuyukoBossDataPatch.CurrentState)) return RunOriginal;
        __instance.__2__current = null;
        __result = false;
        return SkipOriginal;
    }

    /// <summary>
    /// 客机本地计时先结束但主机消息未到时，以 null 和 true 继续等待下一帧。
    /// 主机消息到达后由 Prefix 结束等待；Prefix 跳过原版也会进入此处，已有消息时不会重新挂起。
    /// 主机与单机不受此等待规则影响。
    /// </summary>
    [HarmonyPatch(nameof(TimingLoop.MoveNext))]
    [HarmonyPostfix]
    public static void MoveNext_Postfix(TimingLoop __instance, ref bool __result)
    {
        if (!GameSession.HasPeers || !GameSession.IsRoomClient || !PrepSceneManager.IsYuyukoChallenge || __result
            || YuyukoGuestSync.HasPhaseEnd(YuyukoBossDataPatch.CurrentState)) return;
        __instance.__2__current = null;
        __result = true;
    }
}
