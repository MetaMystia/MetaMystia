using System.Collections.Generic;

using Mystia.Scenes;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;

namespace MetaMystia;

/// <summary>
/// 幽幽子挑战的阶段同步。原实现直接挂在挑战的编译器生成状态机上；本文件改为消费框架的挑战时间线
/// （<see cref="IWorkSceneChallengeServices"/> 与 <c>IChallengeListener</c>），只保留阶段数据的收发、
/// 吞厨具记录与生命值转发。
/// <para>
/// 阶段数据只在营业场景循环内读写：挑战服务要求场景作用域，而主循环的步骤回调发生在游戏自己的协程里
/// （不在作用域内）。「某个位置的数据还没就绪」因此用挂起该步表达（见 <see cref="ShouldHoldMainStep"/>），
/// 读写由该步被挂起的那一帧在 <see cref="DriveChallenge"/> 里完成，放行后该步判定用的就是这次读写的数值。
/// </para>
/// </summary>
public static partial class YuyukoGuestSync
{
    /// <summary>
    /// 阶段数据在协议里的位置。取值沿用协议历史上的游戏状态编号，线格式不变；与游戏步骤的对应关系由框架
    /// 的语义步骤给出，逻辑一律比对 <see cref="ChallengeStep"/>，不比对数字。
    /// </summary>
    private enum PhasePoint
    {
        Settled1 = 4,
        CountingStopped2 = 9,
        Settled2 = 10,
        Ending3PreparingRetake = 15,
        Ended3 = 16,
    }

    /// <summary>主机按位置发布的阶段数据；客机按位置保存，允许消息先于本地剧情到达。</summary>
    private static readonly Dictionary<int, YuyukoGuestMessage> phases = new();

    /// <summary>主机已广播的位置。</summary>
    private static readonly HashSet<int> sentPhases = new();

    /// <summary>客机已写入本机挑战闭包的位置。</summary>
    private static readonly HashSet<int> appliedPhases = new();

    /// <summary>客机正挂起等待写入的位置。</summary>
    private static PhasePoint? heldPoint;

    /// <summary>主机待广播的位置；由场景循环读出挑战闭包的数值后发送。</summary>
    private static PhasePoint? pendingSend;

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

    /// <summary>框架的语义步骤对应的协议位置；不交换阶段数据的步骤为 null。</summary>
    private static PhasePoint? PointOf(ChallengeStep step)
    {
        if (step == ChallengeStep.Phase1Settled) return PhasePoint.Settled1;
        if (step == ChallengeStep.Phase2CountingStopped) return PhasePoint.CountingStopped2;
        if (step == ChallengeStep.Phase2Settled) return PhasePoint.Settled2;
        if (step == ChallengeStep.Phase3EndingPreparingRetake) return PhasePoint.Ending3PreparingRetake;
        if (step == ChallengeStep.Phase3Ended) return PhasePoint.Ended3;
        return null;
    }

    /// <summary>协议里的位置编号是否有效；对端可能发来本版本不认识的位置。</summary>
    private static bool IsPhasePoint(int point) => (PhasePoint)point
        is PhasePoint.Settled1 or PhasePoint.CountingStopped2 or PhasePoint.Settled2
        or PhasePoint.Ending3PreparingRetake or PhasePoint.Ended3;

    /// <summary>该厨具是否仍被本次挑战的吞食锁定；吞食收尾后由挑战监听清除。</summary>
    internal static bool IsSwallowedCooker(int gridIndex) => PhaseSyncActive && swallowedCookers.Contains(gridIndex);

    #region 主循环步骤上的阶段数据

    /// <summary>
    /// 这一步是否必须挂起。
    /// <para>
    /// 主机：绑定本体之前无法广播；该位置的阶段数据还没广播之前也一样——广播要读挑战闭包的数值，而读只能
    /// 在营业场景循环里做，因此挂起本步一帧，等场景循环读出并发出之后再放行。
    /// </para>
    /// <para>
    /// 客机：主机依据还没写入本机挑战闭包之前不能用自己的数据判定，因此挂起本步，等场景循环写入后再放行。
    /// 非阶段同步期间一律放行。
    /// </para>
    /// </summary>
    internal static bool ShouldHoldMainStep(ChallengeStep step)
    {
        if (!PhaseSyncActive || PointOf(step) is not { } point) return false;

        if (!GameSession.IsRoomHost)
        {
            if (appliedPhases.Contains((int)point))
            {
                heldPoint = null;
                return false;
            }
            heldPoint = point;
            return true;
        }

        if (fsm == null) return true;
        // 一阶段的营业额要在它自己的清场结账之后才可用，因此它在步后广播（见 OnMainStepRan）。
        if (point == PhasePoint.Settled1 || sentPhases.Contains((int)point)) return false;
        pendingSend = point;
        return true;
    }

    /// <summary>这一步是否属于第三阶段收尾，需要停止接受新的吞食。</summary>
    internal static void NoticePhaseState(ChallengeStep step)
    {
        if (step == ChallengeStep.Phase3EndingPreparingRetake || step == ChallengeStep.Phase3Ended) EndPhase3();
    }

    /// <summary>
    /// 一步执行完。一阶段在结账步里清场结账并判定，随后停在失败等待或成功剧情等待，因此只有确实离开
    /// 结账步、进入这两个位置时才广播最终营业额；数值由场景循环在该步之后读出，客机据此判定。
    /// </summary>
    internal static void OnMainStepRan(ChallengeStep step, ChallengeStep next)
    {
        if (!GameSession.IsRoomHost || !PhaseSyncActive) return;
        if (step != ChallengeStep.Phase1Settled) return;
        if (next != ChallengeStep.Phase1Failed && next != ChallengeStep.Phase1Story) return;
        pendingSend = PhasePoint.Settled1;
    }

    /// <summary>主机发布一个位置的阶段数据；同一位置只发一次。</summary>
    private static void SendPhase(int point, int fund, int spell, int life)
    {
        if (!GameSession.IsRoomHost || !PhaseSyncActive || !sentPhases.Add(point)) return;
        var message = Message(YuyukoGuestEvent.Phase);
        message.PhaseState = point;
        message.Fund = fund;
        message.PositiveSpellCount = spell;
        message.Life = life;
        YuyukoGuestMessage.Send(message);
        Log.Info($"幽幽子阶段 {point} 已广播: fund={fund}, spell={spell}, life={life}");
    }

    private static void ReceivePhase(YuyukoGuestMessage message)
    {
        if (IsPhasePoint(message.PhaseState)) phases.TryAdd(message.PhaseState, message);
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
        appliedPhases.Clear();
        heldPoint = null;
        pendingSend = null;
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
    /// 客机是否已收到某个阶段对应位置的主机依据。阶段与位置的对应沿用原实现：一阶段结账、二阶段停止
    /// 计数、三阶段两种收尾。
    /// </summary>
    internal static bool IsHostPhaseReady(ChallengePhase phase, ChallengeRunKind kind)
    {
        var step = phase switch
        {
            ChallengePhase.One => ChallengeStep.Phase1Settled,
            ChallengePhase.Two => ChallengeStep.Phase2CountingStopped,
            ChallengePhase.Three when kind == ChallengeRunKind.Retake => ChallengeStep.Phase3Ended,
            ChallengePhase.Three => ChallengeStep.Phase3EndingPreparingRetake,
            _ => default,
        };
        return PointOf(step) is { } point && phases.ContainsKey((int)point);
    }

    /// <summary>
    /// 在营业场景循环内落实阶段数据交换与挑战服务动作。主机把待广播位置的阶段数据读出来发出；客机把
    /// 挂起位置的主机依据写入本机挑战闭包、重放待处理的吞厨具、把主机生命值写回面板，并在拿到主机依据后
    /// 结束本地阶段时钟（与 <see cref="YuyukoChallengeSync.OnChallengeClockElapsed"/> 的挂起互补）。
    /// 场景服务只在场景循环内有效，因此这些读写只能在这里执行。
    /// </summary>
    internal static void DriveChallenge(IWorkSceneChallengeServices challenge)
    {
        if (!PhaseSyncActive) return;

        if (GameSession.IsRoomHost)
        {
            if (pendingSend is not { } send) return;
            pendingSend = null;
            SendPhase((int)send, challenge.EarnedFund, challenge.PositiveSpellCount, challenge.BossLife);
            return;
        }

        ApplyHeldPhase(challenge);
        PlayPendingSwallows(challenge);

        if (hostLife is { } life && appliedLife != life)
        {
            appliedLife = life;
            challenge.BossLife = life;
        }

        if (challenge.Clock != default && IsHostPhaseReady(challenge.Phase, challenge.RunKind))
            challenge.EndPhaseClock();
    }

    /// <summary>
    /// 客机把挂起位置的主机依据写入本机挑战闭包，使该步用主机的营业额、符卡数与生命值判定。本体尚未绑定时
    /// 不写（此时框架的本体镜像还没接上，写入会落到空处），挂起继续等待即可。
    /// </summary>
    private static void ApplyHeldPhase(IWorkSceneChallengeServices challenge)
    {
        if (fsm == null || heldPoint is not { } point) return;
        if (appliedPhases.Contains((int)point)) return;
        if (!phases.TryGetValue((int)point, out var message)) return;

        challenge.EarnedFund = message.Fund;
        challenge.PositiveSpellCount = message.PositiveSpellCount;
        challenge.BossLife = message.Life;
        appliedPhases.Add((int)point);
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
