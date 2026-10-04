using Mystia.Listeners;
using Mystia.Scenes;

using MetaMystia.Multiplayer;
using MetaMystia.Patch;
using MetaMystia.UI;

namespace MetaMystia;

/// <summary>
/// 幽幽子挑战时间线监听，取代原先挂在挑战编译器生成状态机上的 9 个补丁：阶段计时（挂起与提前结束）、
/// 阶段刷客闸门、重打的本体可接单开关、吞厨具重放、失败开始通知、buff 收尾与本体生命值变更。
/// <para>
/// 挑战服务只在营业场景循环的 Setup/Update/Shutdown 内有效，因此主动动作（延长阶段时长、结束本地时钟、
/// 重放吞厨具、写回生命值、写回可接单开关）统一在 <see cref="Update"/> 的同一处执行。
/// </para>
/// </summary>
[AutoLog]
public sealed partial class YuyukoChallengeSync : IChallengeListener, IWorkSceneGameLoop
{
    private bool _orderFlagArmed;

    #region 营业场景循环

    public void Setup(IWorkSceneServices services)
    {
        _orderFlagArmed = false;
        ArmPhaseSeconds(services.Challenge);
    }

    public void Update(IWorkSceneServices services, float delta)
    {
        var challenge = services.Challenge;

        // 限时负面符卡（第二阶段）：联机时该效果归主机，本机抑制它（原 NightSceneDirector… 补丁在此跳过
        // 原版协程体并补上提示）。这一条不依赖阶段同步，只要有同伴就成立。
        challenge.TimedNegativeSpellEnabled = !GameSession.HasRoomPeers;

        if (YuyukoGuestSync.PhaseSyncActive)
        {
            // 本体的框架句柄由挑战服务提供（服务只在场景循环内有效），拿到之后监听器与消息都按句柄判断。
            YuyukoGuestSync.Capture(challenge.BossGuest);

            // 阶段时钟一旦跑过就不能再改该阶段时长，重复下发是幂等的。
            ArmPhaseSeconds(challenge);
            ArmsOrderFlag(challenge);
            YuyukoGuestSync.DriveChallenge(challenge);
        }
    }

    public void Shutdown(IWorkSceneServices services) => _orderFlagArmed = false;

    /// <summary>
    /// 联机下延长挑战阶段时长。原实现挂在阶段计时协程入口上直接改写闭包字段，框架按阶段接收绝对秒数，
    /// 因此这里用挑战数据里的基准时长乘上本模组的倍率。基准时长只存在于挑战闭包中，由保留的
    /// <see cref="YuyukoBossDataPatch"/> 以纯数值转出。
    /// </summary>
    private static void ArmPhaseSeconds(IWorkSceneChallengeServices challenge)
    {
        var baseSeconds = YuyukoBossDataPatch.SingleRoundSeconds;
        if (baseSeconds <= 0) return;

#if DEBUG
        // 调试期把每个阶段拉到便于观察的长度。
        const float factor = 64f;
#else
        var factor = PrepSceneManager.YuyukoPrepRound == 3 ? 4f : 2.25f;
#endif
        challenge.SetPhaseSeconds(ChallengePhase.One, baseSeconds * factor);
        challenge.SetPhaseSeconds(ChallengePhase.Two, baseSeconds * factor);
        challenge.SetPhaseSeconds(ChallengePhase.Three, baseSeconds * factor);
    }

    /// <summary>
    /// 客机在重打第三阶段允许本体接单。原实现直接写闭包的 <c>ifYuyukoCouldOrder</c>；框架的
    /// <see cref="IWorkSceneChallengeServices.BossOrderEnabled"/> 会在该阶段刷客循环赋值时写入同一字段，
    /// 因此这里只需要给出一次裁决，之后可由框架自行重申。
    /// </summary>
    private void ArmsOrderFlag(IWorkSceneChallengeServices challenge)
    {
        if (_orderFlagArmed || !YuyukoGuestSync.PhaseSyncActive || !GameSession.IsRoomClient) return;
        if (challenge.Phase != ChallengePhase.Three || challenge.RunKind != ChallengeRunKind.Retake) return;
        challenge.BossOrderEnabled = true;
        _orderFlagArmed = true;
    }

    #endregion

    #region 主循环步骤

    /// <summary>
    /// 挑战主循环的一步：主机在本体未绑定、或该位置的阶段数据还没广播之前挂起本步；客机在主机依据写入本机
    /// 挑战闭包之前挂起本步。挂起期间该步的判定数据由营业场景循环读写（见
    /// <see cref="YuyukoGuestSync.DriveChallenge"/>），放行后本步用的就是那次读写的数值。框架对挂起的步不
    /// 派发「步已执行」通知，因此挂起与广播不会互相误判。原实现在同一处停止接受新的吞食。
    /// </summary>
    void IChallengeListener.OnPreChallengeStep(ChallengeStep step, ref bool cancelInvocation)
    {
        cancelInvocation = YuyukoGuestSync.ShouldHoldMainStep(step);
        if (!cancelInvocation) YuyukoGuestSync.NoticePhaseState(step);
    }

    /// <summary>一步执行完：一阶段结账步之后（清场结账已在同一段里完成）广播最终营业额。</summary>
    void IChallengeListener.OnChallengeStepRan(ChallengeStep step, ChallengeStep next) =>
        YuyukoGuestSync.OnMainStepRan(step, next);

    #endregion

    #region 阶段时钟

    /// <summary>
    /// 第三阶段开始时给玩家一次提示。原实现挂在阶段计时协程入口上，与阶段时长一起下发；
    /// 现在由挑战时间线的阶段开始通知承担。
    /// </summary>
    void IChallengeListener.OnChallengePhaseStarted(ChallengePhaseInfo phase)
    {
        if (!GameSession.HasRoomPeers) return;
        if (phase.Phase != ChallengePhase.Three) return;
        InGameConsole.ShowPassive(TextId.YuyukoPhase3PatientExtended.Get());
    }

    /// <summary>
    /// 客机：本地阶段计时先结束而主机依据未到时挂住阶段结束；依据到达后由
    /// <see cref="YuyukoGuestSync.DriveChallenge"/> 结束本地时钟，两端因而在同一阶段上继续。
    /// 主机与单机不受影响。
    /// </summary>
    void IChallengeListener.OnChallengeClockElapsed(ChallengeClock clock, ref bool holdClock)
    {
        if (!GameSession.HasRoomPeers || !GameSession.IsRoomClient || !PrepSceneManager.IsYuyukoChallenge) return;
        if (!YuyukoGuestSync.IsHostPhaseReady(clock.Phase, clock.Kind)) holdClock = true;
    }

    #endregion

    #region 阶段刷客

    /// <summary>
    /// 客机：二三阶段的刷客由主机生成并通过普通顾客消息接入，这里挂住本次迭代。挂住只保留这一次刷客，
    /// 不会停止循环，因此原版收尾仍能正常停止它。一阶段的刷客跟随普通顾客同步，主机与单机一律放行原版。
    /// </summary>
    void IChallengeListener.OnPreChallengeGuestSpawn(ChallengeSpawnAttempt attempt, ref bool cancelInvocation)
    {
        if (attempt.Phase is not (ChallengePhase.Two or ChallengePhase.Three)) return;
        if (!YuyukoGuestSync.PhaseSyncActive || !GameSession.IsRoomClient) return;
        cancelInvocation = true;
    }

    #endregion

    #region 通知

    /// <summary>挑战失败剧情开始：主机广播失败结果，客机进入原版的失败重放。</summary>
    void IChallengeListener.OnChallengeFailureStarted() => YuyukoBossDataPatch.OnFailureStarted();

    /// <summary>重打第三阶段的 buff 收尾：厨具锁已由框架释放，这里结束同步侧的吞食处理。</summary>
    void IChallengeListener.OnChallengeBuffEnded() => YuyukoGuestSync.EndPhase3();

    /// <summary>挑战面板报告了本体生命值：主机把它作为权威值广播。</summary>
    void IChallengeListener.OnChallengeBossLifeChanged(int life) => YuyukoGuestSync.OnBossLifeChanged(life);

    /// <summary>
    /// 本体吞食了一个厨具：游戏自身的吞食会让主机广播目标，模组经 <c>SwallowCooker</c> 触发的重放
    /// 走同一条通知，因此客机会把重放也登记为已锁定。
    /// </summary>
    void IChallengeListener.OnChallengeCookerSwallowed(int cookerIndex) => YuyukoGuestSync.OnCookerSwallowed(cookerIndex);

    /// <summary>
    /// 本体的评价改判回调。客机回填主机结果、台词、连击保护与伤害倍率，并整体取消原回调
    /// （重打版原回调会扣血、差评还会吞厨具，不能在客机再次执行）；主机放行原回调。
    /// </summary>
    void IChallengeListener.OnPreBossEvaluated(ref ChallengeBossEvaluation evaluation, ref bool cancelInvocation) =>
        cancelInvocation = YuyukoGuestSync.ReplayBossEvaluation(ref evaluation);

    /// <summary>原回调结束：记录它给出的台词与连击保护，供后续评价消息组装。</summary>
    void IChallengeListener.OnBossEvaluated(in ChallengeBossEvaluation evaluation) =>
        YuyukoGuestSync.CaptureBossEvaluation(evaluation);

    /// <summary>
    /// 限时负面符卡被框架抑制（联机时不落在本机，由主机拥有该效果）：补上原版那句提示。
    /// </summary>
    void IChallengeListener.OnTimedNegativeSpellSuppressed() =>
        InGameConsole.ShowPassive(TextId.YuyukoTimedNegativeSpellDisabled.Get());

    /// <summary>
    /// 挑战自己的退场开始（框架放行游戏的 TryLeaveSession）：标记本次场景加载是最终试炼的返回。
    /// </summary>
    void IChallengeListener.OnChallengeLeaveStarted() => GameFlow.ReturningFromTrial = true;

    /// <summary>退场结束：清掉标记。</summary>
    void IChallengeListener.OnChallengeLeaveFinished() => GameFlow.ReturningFromTrial = false;

    #endregion
}
