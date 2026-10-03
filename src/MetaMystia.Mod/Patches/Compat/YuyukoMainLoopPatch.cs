#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using MainLoop = GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16;

using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

/// <summary>
/// 挑战主循环恢复位置上的阶段数据交换。
/// <para>
/// 缺口说明：框架的挑战时间线按语义暴露阶段、时钟与刷客迭代，但不暴露主循环的恢复位置编号
/// （<c>ChallengeStep</c> 的编号不可读），也不暴露挑战闭包里的营业额与正面符卡计数，因此
/// 「在某个恢复位置上交换阶段数据」这一项仍以兼容形式保留。本文件只做「读闭包数值 / 写闭包数值」，
/// 判定与消息收发全部交给 <see cref="YuyukoGuestSync"/>，后者不引用任何生成成员。
/// </para>
/// </summary>
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16))]
[AutoLog]
public static partial class YuyukoMainLoopPatch
{
    /// <summary>
    /// 在原版主循环恢复前交换阶段数据：主机先绑定本体再广播，客机先回填主机依据再判定；
    /// 任一条件未就绪时不执行本次原版 MoveNext，以 null 等待下一帧（返回 true 保持协程存活）。
    /// </summary>
    [HarmonyPatch(nameof(MainLoop.MoveNext))]
    [HarmonyPrefix]
    public static bool MoveNext_Prefix(MainLoop __instance, ref bool __result, out int __state)
    {
        __state = __instance.__1__state;

        if (YuyukoGuestSync.ShouldHoldMainStep(__state))
        {
            __instance.__2__current = null;
            __result = true;
            return SkipOriginal;
        }

        var context = __instance.__8__1;
        if (context == null) return RunOriginal;

        if (YuyukoGuestSync.TryApplyPhase(__state, out int fund, out int spell, out int life))
        {
            // 客机用主机的营业额、符卡数与生命值判定，不能先用本机数据得出结论。
            context.eventManager.EarnedFund = fund;
            context.positiveSpellCount = spell;
            context.yuyukoTotalLife = life;
        }
        else if (GameSession.IsRoomHost && YuyukoGuestSync.PhaseSyncActive && __state is 9 or 10 or 15 or 16)
        {
            // 一阶段之外的阶段在本次恢复之前广播；一阶段的营业额要在它自己的结账之后才可用（见 Postfix）。
            YuyukoGuestSync.SendPhase(__state, context.eventManager.EarnedFund, context.positiveSpellCount,
                context.yuyukoTotalLife);
        }

        // 三阶段的两种收尾都在恢复位置 15/16 上：停止接受新的吞食。
        YuyukoGuestSync.NoticePhaseState(__state);
        return RunOriginal;
    }

    /// <summary>
    /// 恢复位置 4 在同一次 MoveNext 内清场结账并判定，随后停在失败等待 5 或成功剧情等待 6。
    /// 只有确实离开 4 进入这两个位置时才广播最终营业额，避免客机用清场前金额判定。
    /// </summary>
    [HarmonyPatch(nameof(MainLoop.MoveNext))]
    [HarmonyPostfix]
    public static void MoveNext_Postfix(MainLoop __instance, int __state, bool __runOriginal)
    {
        if (!__runOriginal || !YuyukoGuestSync.PhaseSyncActive) return;
        if (!YuyukoGuestSync.ShouldBroadcastAfterStep(__state, __instance.__1__state)) return;

        var context = __instance.__8__1;
        if (context == null) return;
        YuyukoGuestSync.SendPhase(4, context.eventManager.EarnedFund, context.positiveSpellCount,
            context.yuyukoTotalLife);
    }
}
