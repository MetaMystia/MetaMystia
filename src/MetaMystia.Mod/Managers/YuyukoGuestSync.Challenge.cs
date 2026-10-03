using System.Collections.Generic;

using Mystia.Scenes;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;

namespace MetaMystia;

/// <summary>
/// 幽幽子挑战的阶段同步。原实现直接挂在挑战的编译器生成状态机上；本文件改为消费框架的挑战时间线
/// （<see cref="IWorkSceneChallengeServices"/> 与 <c>IChallengeListener</c>），只保留阶段数据的收发、
/// 吞厨具记录与生命值转发。需要读取挑战闭包或主循环恢复位置的少量工作留在
/// <c>Patches/Compat/YuyukoMainLoopPatch</c>（本仓库唯一允许接触生成成员的地方），它把纯数值交给本文件。
/// </summary>
public static partial class YuyukoGuestSync
{
    /// <summary>主机按主循环恢复位置发布的阶段数据；客机按位置保存，允许消息先于本地剧情到达。</summary>
    private static readonly Dictionary<int, YuyukoGuestMessage> phases = new();
    private static readonly HashSet<int> sentPhases = new();

    /// <summary>主机广播的吞厨具索引，等待在营业场景循环内用挑战服务重放。</summary>
    private static readonly Queue<int> pendingSwallows = new();

    /// <summary>本次挑战已被吞食、厨具锁仍生效的位置；由挑战监听按游戏收尾释放。</summary>
    private static readonly HashSet<int> swallowedCookers = new();

    private static bool phase3Ended;
    private static int? hostLife;
    private static int? appliedLife;

    /// <summary>
    /// 本模组正在用挑战服务重放吞厨具。该重放在厨具层的 <c>InterruptCook</c> 使游戏再次走到取菜入口，
    /// 此期间不把它当作玩家取菜广播。
    /// </summary>
    internal static bool IsInterruptingCooker { get; private set; }

    /// <summary>阶段同步是否生效；与是否已绑定本体无关。</summary>
    internal static bool PhaseSyncActive => GameSession.HasRoomPeers && PrepSceneManager.IsYuyukoChallenge;

    /// <summary>原版主循环里需要交换阶段数据的恢复位置；数字属于 4.4.0e 布局，由 <see cref="YuyukoMainLoopPatch"/> 核验。</summary>
    internal static bool IsPhasePosition(int state) => state is 4 or 9 or 10 or 15 or 16;

    /// <summary>该厨具是否仍被本次挑战的吞食锁定；吞食收尾后由挑战监听清除。</summary>
    internal static bool IsSwallowedCooker(int gridIndex) => PhaseSyncActive && swallowedCookers.Contains(gridIndex);

    #region 主循环阶段数据

    /// <summary>
    /// 本次主循环恢复位置是否必须挂起：主机在绑定本体之前无法构造阶段消息；客机在收到主机依据之前不能
    /// 用本机数据判定结果。非阶段同步期间一律放行。
    /// </summary>
    internal static bool ShouldHoldMainStep(int state)
    {
        if (!PhaseSyncActive || !IsPhasePosition(state)) return false;
        return GameSession.IsRoomHost ? fsm == null : !phases.ContainsKey(state);
    }

    /// <summary>客机在相应恢复位置回填主机依据；返回 false 表示本机照常判定。</summary>
    internal static bool TryApplyPhase(int state, out int fund, out int spell, out int life)
    {
        fund = 0;
        spell = 0;
        life = 0;
        if (!GameSession.IsRoomClient || !PhaseSyncActive || !IsPhasePosition(state)) return false;
        if (!phases.TryGetValue(state, out var message)) return false;
        fund = message.Fund;
        spell = message.PositiveSpellCount;
        life = message.Life;
        return true;
    }

    /// <summary>主机发布一个恢复位置的阶段数据；同一位置只发一次。</summary>
    internal static void SendPhase(int state, int fund, int spell, int life)
    {
        if (!GameSession.IsRoomHost || !PhaseSyncActive || !sentPhases.Add(state)) return;
        var message = Message(YuyukoGuestEvent.Phase);
        message.PhaseState = state;
        message.Fund = fund;
        message.PositiveSpellCount = spell;
        message.Life = life;
        YuyukoGuestMessage.Send(message);
        Log.Info($"幽幽子阶段 {state} 已广播: fund={fund}, spell={spell}, life={life}");
    }

    /// <summary>
    /// 一阶段在恢复位置 4 内清场结账并判定，阶段数据必须在同一段执行完之后才广播，
    /// 否则客机拿到的是清场前金额。这里只回答「刚离开 4 且进入等待分支」这一种情况。
    /// </summary>
    internal static bool ShouldBroadcastAfterStep(int previousState, int currentState) =>
        GameSession.IsRoomHost && PhaseSyncActive && previousState == 4 && currentState is 5 or 6;

    /// <summary>该恢复位置是否属于第三阶段收尾，需要停止接受新的吞食。</summary>
    internal static void NoticePhaseState(int state)
    {
        if (state is 15 or 16) EndPhase3();
    }

    private static void ReceivePhase(YuyukoGuestMessage message)
    {
        if (IsPhasePosition(message.PhaseState)) phases.TryAdd(message.PhaseState, message);
    }

    #endregion

    #region 吞厨具

    /// <summary>客机收到主机选定的厨具，等待在营业场景循环内用挑战服务重放。</summary>
    internal static void ReceiveSwallow(int cookerIndex)
    {
        if (!phase3Ended) pendingSwallows.Enqueue(cookerIndex);
    }

    /// <summary>
    /// 挑战的吞食报告：主机广播本次选定的厨具，两端都记下仍被锁定的位置。
    /// 游戏自身的吞食与模组经服务触发的重放走同一条通知，因此主机不会漏报、客机的重放也不会回声。
    /// </summary>
    internal static void OnCookerSwallowed(int cookerIndex)
    {
        if (!PhaseSyncActive || phase3Ended) return;
        swallowedCookers.Add(cookerIndex);
        if (!GameSession.IsRoomHost) return;
        var message = Message(YuyukoGuestEvent.Swallow);
        message.CookerIndex = cookerIndex;
        YuyukoGuestMessage.Send(message);
    }

    /// <summary>
    /// 停止接受新吞食并清空本次记录。厨具锁本身由框架在挑战收尾时释放（游戏自身的 buff 收尾与框架的
    /// 重放共用同一条释放路径），因此这里不再手工解锁。本方法可重复调用。
    /// </summary>
    internal static void EndPhase3()
    {
        phase3Ended = true;
        pendingSwallows.Clear();
        swallowedCookers.Clear();
        IsInterruptingCooker = false;
    }

    private static void ResetChallengeEvents()
    {
        EndPhase3();
        phases.Clear();
        sentPhases.Clear();
        phase3Ended = false;
        ResetLife();
    }

    #endregion

    #region 本体生命值

    /// <summary>挑战面板报告了本体生命值；主机把它作为权威值广播，客机不回声。</summary>
    internal static void OnBossLifeChanged(int life)
    {
        if (!PhaseSyncActive || !GameSession.IsRoomHost) return;
        YuyukoLifeMessage.Send(life);
    }

    /// <summary>客机记下主机生命值，等待在营业场景循环内写回面板。</summary>
    internal static void ReceiveLife(int life)
    {
        if (!PrepSceneManager.IsYuyukoChallenge) return;
        hostLife = life;
    }

    /// <summary>丢弃上一轮暂存的主机生命值；新一次挑战开始时调用。</summary>
    internal static void ResetLife()
    {
        hostLife = null;
        appliedLife = null;
    }

    #endregion

    #region 每帧驱动

    /// <summary>
    /// 客机是否已收到某个阶段对应恢复位置的主机依据。阶段与恢复位置的对应属于 4.4.0e 布局，
    /// 与 <see cref="YuyukoMainLoopPatch"/> 使用的编号一致。
    /// </summary>
    internal static bool IsHostPhaseReady(ChallengePhase phase, ChallengeRunKind kind)
    {
        int state = phase switch
        {
            ChallengePhase.One => 4,
            ChallengePhase.Two => 9,
            ChallengePhase.Three when kind == ChallengeRunKind.Retake => 16,
            ChallengePhase.Three => 15,
            _ => -1,
        };
        return state >= 0 && phases.ContainsKey(state);
    }

    /// <summary>
    /// 在营业场景循环内落实挑战服务动作：客机在拿到主机阶段依据后结束本地阶段时钟（与
    /// <see cref="YuyukoChallengeSync.OnChallengeClockElapsed"/> 的挂起互补）、重放待处理的吞厨具、
    /// 并把主机生命值写回面板。场景服务只在场景循环内有效，因此这些动作只能在这里执行。
    /// </summary>
    internal static void DriveChallenge(IWorkSceneChallengeServices challenge)
    {
        if (!PhaseSyncActive || !GameSession.IsRoomClient) return;

        PlayPendingSwallows(challenge);

        if (hostLife is { } life && appliedLife != life)
        {
            appliedLife = life;
            challenge.BossLife = life;
        }

        if (challenge.Clock != default && IsHostPhaseReady(challenge.Phase, challenge.RunKind))
            challenge.EndPhaseClock();
    }

    private static void PlayPendingSwallows(IWorkSceneChallengeServices challenge)
    {
        if (phase3Ended)
        {
            pendingSwallows.Clear();
            return;
        }

        while (pendingSwallows.TryDequeue(out int index))
        {
            IsInterruptingCooker = true;
            try
            {
                challenge.SwallowCooker(index);
            }
            finally
            {
                IsInterruptingCooker = false;
            }
        }
    }

    #endregion
}
