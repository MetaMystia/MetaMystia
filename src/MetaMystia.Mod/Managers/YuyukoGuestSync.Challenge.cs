#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using System;
using System.Collections.Generic;

using Il2CppSystem.Linq;
using UnityEngine;

using NightScene.CookingUtility;
using NightScene.Tiles;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;

using LockLoop = GameData.Profile.YuyukoBossData.__c__DisplayClass16_6.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObSpCoObObUnique;
using MainLoop = GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16;
using Object = UnityEngine.Object;

namespace MetaMystia;

public static partial class YuyukoGuestSync
{
    private static readonly Dictionary<int, YuyukoGuestMessage> phases = new();
    private static readonly HashSet<int> sentPhases = new();
    private static readonly Queue<int> pendingSwallows = new();
    private static readonly Dictionary<IntPtr, int> replaySwallows = new();
    private static readonly Dictionary<IntPtr, LockLoop> activeSwallows = new();
    private static bool phase3Ended;
    /// <summary>吞食协程正在执行中断烹饪步骤；此期间内部 Extract 不作为玩家取菜再次广播。</summary>
    internal static bool IsInterruptingCooker { get; private set; }

    /// <summary>
    /// 判断指定厨具是否仍被本次吞食锁定，供本地烹饪入口和网络重放拒绝迟到操作。
    /// 同时匹配协程记录的厨具位置和原版锁列表，不能仅凭特效存在或历史吞食记录判断。
    /// </summary>
    internal static bool IsSwallowedCooker(int gridIndex)
    {
        if (!GameSession.HasPeers || !PrepSceneManager.IsYuyukoChallenge) return false;
        foreach (var loop in activeSwallows.Values)
            if (loop._lockedCookController_5__3?.GridIndex == gridIndex && loop.__8__1?.targets != null
                && loop.__4__this.field_Public___c__DisplayClass16_0_0.eventManager.LockedCookersRaw.Contains(loop.__8__1.targets))
                return true;
        return false;
    }

    /// <summary>
    /// 客机等待主机阶段依据，再由原版选择成功或失败分支。一阶段主机需先清场结账，由 Postfix 广播。
    /// 4.4.0e 的恢复位置：4 为一阶段计时结束，9 为二阶段计时结束，10 为二阶段符卡执行完毕；
    /// 15、16 分别为剧情版、重打版三阶段计时结束，不表示成功和失败。
    /// </summary>
    /// <returns>
    /// true 放行原版；false 暂停本次 MoveNext，由 Hook 保持当前位置并等待下一帧。
    /// 主机需先绑定本体以构造消息；客机需收到对应恢复位置的数据，才能回填收入、符卡数和生命值。
    /// </returns>
    internal static bool BeforeMainStep(MainLoop loop)
    {
        if (!GameSession.HasPeers || !PrepSceneManager.IsYuyukoChallenge) return true;
        int state = loop.__1__state;
        if (state is not (4 or 9 or 10 or 15 or 16)) return true;
        var context = loop.__8__1;
        if (GameSession.IsRoomHost)
        {
            if (fsm == null) return false;
            if (state != 4) SendPhase(loop, state);
        }
        else
        {
            if (!phases.TryGetValue(state, out var message)) return false;
            context.eventManager.EarnedFund = message.Fund;
            context.positiveSpellCount = message.PositiveSpellCount;
            context.yuyukoTotalLife = message.Life;
            if (state == 4) Log.Info($"Yuyuko phase 1 applying settled fund: {message.Fund}");
        }
        if (state is 15 or 16) EndPhase3();
        return true;
    }

    /// <summary>
    /// 4.4.0e state 4 在同一次 MoveNext 内清场结账并判定，随后停在失败等待 5 或成功剧情等待 6。
    /// 此时发送最终营业额，避免客机用清场前金额判定；原版未执行或未完成该段时不发送。
    /// </summary>
    internal static void AfterMainStep(MainLoop loop, int previousState)
    {
        if (!GameSession.HasPeers || !GameSession.IsRoomHost || !PrepSceneManager.IsYuyukoChallenge
            || previousState != 4 || loop.__1__state is not (5 or 6)) return;
        SendPhase(loop, 4);
    }

    private static void SendPhase(MainLoop loop, int state)
    {
        if (!sentPhases.Add(state)) return;
        var context = loop.__8__1;
        var message = Message(YuyukoGuestEvent.Phase);
        message.PhaseState = state;
        message.Fund = context.eventManager.EarnedFund;
        message.PositiveSpellCount = context.positiveSpellCount;
        message.Life = context.yuyukoTotalLife;
        YuyukoGuestMessage.Send(message);
        if (state == 4)
            Log.Info($"Yuyuko phase 1 settled: fund={message.Fund}, nextState={loop.__1__state}");
    }

    /// <summary>
    /// 客机按主循环恢复位置保存阶段消息，允许消息先于本地剧情到达。
    /// 仅接受已核对的五个位置；相同位置保留第一条消息，不在接收时直接推进原版协程。
    /// </summary>
    private static void ReceivePhase(YuyukoGuestMessage message)
    {
        if (message.PhaseState is 4 or 9 or 10 or 15 or 16)
            phases.TryAdd(message.PhaseState, message);
    }

    /// <summary>查询客机是否已收到指定恢复位置的主机数据；仅表示消息已到，不表示本地阶段已执行完毕。</summary>
    internal static bool HasPhaseEnd(int state) => phases.ContainsKey(state);

    /// <summary>
    /// 客机在重打上下文与特效列表就绪后，按主机目标启动原版吞厨具协程。
    /// 先登记目标和协程，再启动执行；后续 Hook 据此替换随机选择。无效索引记录后丢弃。
    /// 协程句柄同时加入原版列表，使挑战收尾仍能停止这些协程；第三阶段结束后不再启动。
    /// </summary>
    private static void PlayPendingSwallows()
    {
        var retake = YuyukoBossDataPatch.CurrentRetake;
        if (!GameSession.IsRoomClient || phase3Ended || retake?.eatingGameObejct == null) return;
        while (pendingSwallows.TryDequeue(out int index))
        {
            if (index < 0 || index >= TileManager.Instance.CookerDesks.Length)
            {
                Log.Error($"幽幽子吞厨具索引无效：{index}");
                continue;
            }
            var loop = new LockLoop(0) { __4__this = retake };
            replaySwallows.Add(loop.Pointer, index);
            activeSwallows.Add(loop.Pointer, loop);
            var coroutine = retake.field_Public___c__DisplayClass16_0_0.eventManager
                .StartCoroutine(loop.Cast<Il2CppSystem.Collections.IEnumerator>());
            retake.lockCookerCorotine.Add(coroutine);
        }
    }

    /// <summary>
    /// 吞食协程每次恢复前，主机放行原版，客机仅放行已登记的主机目标重放。
    /// 客机在 state 1 替代随机选择，并重建同段的特效初始化；state 0、2、3 继续使用原版。
    /// 阶段结束或客机自行触发的未登记协程直接结束，不再吞食。
    /// </summary>
    /// <param name="loop">4.4.0e 的 LockCookersYuyuko|41 状态机，字段与状态编号均依赖此版本。</param>
    /// <param name="result">跳过原版时返回给 MoveNext 的结果：true 表示仍在等待，false 表示协程结束。</param>
    /// <returns>是否执行原版 MoveNext；与 result 表示的协程存活状态不同。</returns>
    internal static bool BeforeSwallowStep(LockLoop loop, ref bool result)
    {
        IsInterruptingCooker = false;
        if (!GameSession.HasPeers || !PrepSceneManager.IsYuyukoChallenge) return true;
        if (phase3Ended) { result = false; return false; }
        IsInterruptingCooker = loop.__1__state == 2;
        if (!GameSession.IsRoomClient) return true;
        // 不接受客机自己的差评再次触发随机吞食。
        if (!replaySwallows.TryGetValue(loop.Pointer, out int index)) { result = false; return false; }
        if (loop.__1__state != 1) return true;

        // 原版 state 1 负责随机选择和创建特效；这里按主机索引创建同样的上下文，
        // state 2/3 仍由原版执行 InterruptCook、锁定、隐藏和登记 BossBuffend。
        var retake = loop.__4__this;
        var context = retake.field_Public___c__DisplayClass16_0_0;
        var helper = loop.__8__1;
        var targets = new Il2CppSystem.Collections.Generic.List<int>();
        targets.Add(index);
        helper.targets = targets.Cast<Il2CppSystem.Collections.Generic.IEnumerable<int>>();
        helper.cookerPosition = TileManager.Instance.CookerDesks[index];
        var effect = Object.Instantiate(context.__4__this.yuyukoEatEffect);
        retake.eatingGameObejct.Add(effect);
        loop._spriteRenderer_5__2 = effect.GetComponent<SpriteRenderer>();
        // 初始坐标和下方 0.5 秒移动时长均来自 4.4.0e 原版 state 1。
        effect.transform.position = new Vector3(10f, -9.5f, 0f);
        loop._spriteRenderer_5__2.enabled = false;
        loop._lockedCookController_5__3 = CookSystemManager.Instance.GetCooker(helper.cookerPosition);
        loop.__2__current = context.eventManager.LerpPosition(effect.transform,
            (System.Func<Vector3>)(() => helper._MainChallengeLoop_b__64()), 0.5f).Cast<Il2CppSystem.Object>();
        loop.__1__state = 2;
        result = true;
        return false;
    }

    /// <summary>
    /// 清除本次中断标记；主机在 state 1 到 2 时，记录并广播原版刚确定的吞食目标。
    /// 客机不广播。Prefix 跳过原版后 Postfix 仍会进入，因此不能仅凭 Postfix 被调用判断原流程已执行。
    /// </summary>
    internal static void AfterSwallowStep(LockLoop loop, int previousState)
    {
        IsInterruptingCooker = false;
        if (!GameSession.HasPeers || !PrepSceneManager.IsYuyukoChallenge || phase3Ended) return;
        if (GameSession.IsRoomHost && previousState == 1 && loop.__1__state == 2)
        {
            activeSwallows[loop.Pointer] = loop;
            var message = Message(YuyukoGuestEvent.Swallow);
            message.CookerIndex = loop.__8__1.targets.First();
            YuyukoGuestMessage.Send(message);
        }
    }

    /// <summary>
    /// 停止接受新吞食并清除待处理目标，移除本次已跟踪吞食的锁与特效。
    /// 覆盖原版已加锁但尚未登记 BossBuffend 回调就被中断的情况；保留其他符卡的锁。
    /// 协程句柄由原版收尾停止，仍被恢复的吞食协程也会因阶段结束标记退出；本方法可重复调用。
    /// </summary>
    internal static void EndPhase3()
    {
        phase3Ended = true;
        pendingSwallows.Clear();
        // 即便停在 state 2 与登记清理回调之间，也移除本次吞食的锁；不清空其他符卡的锁。
        foreach (var loop in activeSwallows.Values)
        {
            var retake = loop.__4__this;
            var targets = loop.__8__1?.targets;
            if (targets != null)
                retake.field_Public___c__DisplayClass16_0_0.eventManager.LockedCookersRaw.Remove(targets);
            if (loop._spriteRenderer_5__2 != null) Object.Destroy(loop._spriteRenderer_5__2.gameObject);
        }
        activeSwallows.Clear();
        replaySwallows.Clear();
        IsInterruptingCooker = false;
    }

    /// <summary>
    /// 先清理残留吞食，再清空已收和已发阶段记录、恢复阶段标记，供下一次本体同步使用。
    /// 与只结束第三阶段不同，此处同时废弃上一轮阶段消息。
    /// </summary>
    private static void ResetChallengeEvents()
    {
        EndPhase3();
        phases.Clear();
        sentPhases.Clear();
        phase3Ended = false;
    }
}
