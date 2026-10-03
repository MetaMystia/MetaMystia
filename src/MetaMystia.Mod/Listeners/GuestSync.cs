using System;
using System.Collections.Generic;
using System.Linq;

using Il2CppSystem.Linq;
using Mystia.Listeners;
using Mystia.Scenes;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.RunTime.NightSceneUtility;
using NightScene.CookingUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;

namespace MetaMystia.Listeners;

/// <summary>
/// 营业场景顾客同步。承接原 <c>Patches/NightScene/GuestsManagerPatch</c>、<c>GuestGroupControllerPatch</c>、
/// <c>GuestsManager__c__DisplayClass174_0Patch</c> 的同步侧逻辑，并把「跳过原版」改为框架开关与服务。
/// </summary>
/// <remarks>
/// 开关取向与原补丁条件一致（原补丁对每条路径都是「主机放行原版 / 客机短路」）：
/// <list type="bullet">
/// <item>刷客：两侧都关掉原版刷客循环，主机在 <see cref="Update"/> 里按原版公式主动生成（客机只重放主机广播）。</item>
/// <item>入座、订单、离场、打烊：主机放行原版（通知与服务由此产生），客机关掉（客机由主机消息重放）。</item>
/// <item>评价：主机放行原版，客机硬门控（门控期间客机只重放主机广播的评价结果）。</item>
/// </list>
/// 客机的「重放」不再直接调用被开关拦住的游戏方法：顾客 FSM 的待处理项与
/// <see cref="s_pendingReplays"/> 都在 <see cref="Update"/> 的服务作用域内执行，并统一走
/// <c>IWorkSceneGuests</c>/<c>IWorkSceneIzakaya</c> 服务（服务内部放行对应开关）。
/// </remarks>
[AutoLog]
public sealed partial class GuestSync : IGuestGroupListener, IGuestSpawnModifier, IWorkSceneGameLoop
{
    private const int MaxNormalGuestRerollAttempts = 32;
    private const int ReimuProtectionGuestId = 7;

    /// <summary>本帧出队入座的顾客组指针；供 <see cref="OnGroupSeated"/> 判定是否广播出队。</summary>
    private nint _seatingFromQueue;

    /// <summary>
    /// 本模组重放的离场（崩溃顾客清理）期间挂起 <see cref="OnGroupLeft"/> 的广播，
    /// 取代原 <c>GuestReentryPermits.LeaveFromDesk</c> 令牌。
    /// </summary>
    internal static bool LeaveBroadcastSuspended { get; set; }

    /// <summary>原版刷客循环启动后置位，用于以同一时刻对齐三条刷客节奏。</summary>
    private bool _driverArmed;
    private float _nextNormalInterval;
    private float _nextPasserbyInterval;
    private float _lastNormalSpawnTime;
    private float _lastPasserbySpawnTime;
    private float _lastSpecialSpawnTime;

    /// <summary>刷客开关是否由本模组持有；<see cref="_spawnGateOriginal"/> 为其进入同步前的取值。</summary>
    private bool _spawnGateHeld;
    private bool _spawnGateOriginal;

    /// <summary>
    /// 当前营业场景循环的服务作用域。场景服务只在 Setup/Update/Shutdown 内有效，
    /// 因此重放（<c>GuestFSM</c> 的待处理项与 <see cref="s_pendingReplays"/>）也只在这里面执行。
    /// </summary>
    private static IWorkSceneServices s_scopeServices = null!;

    /// <summary>作用域内的营业场景服务；作用域外为 <c>null</c>，此时重放只入队、不执行。</summary>
    internal static IWorkSceneServices ScopedServices => s_scopeServices;

    /// <summary>
    /// 营业场景的服务重放队列（与 <c>WorkSync.EnqueueCook</c> 同形）：入队发生在消息处理器或游戏调用栈里
    /// （作用域外），出队统一发生在 <see cref="Update"/> 内。顾客重放、远端数值编辑、QTE 结算、
    /// 打烊与启动计时都走这一条队列。
    /// </summary>
    private static readonly List<(string Tag, Action<IWorkSceneServices> Apply)> s_pendingReplays = [];

    /// <summary>主机在生成时暂存的顾客生成参数（原 <c>GuestsManagerPatch.PendingSpawnArgs</c>），
    /// 由 <see cref="OnPreSpawnNormalGuests"/>／<see cref="OnPreSpawnSpecialGuest"/> 记录，随生成广播发给客机。</summary>
    private static PendingSpawnArgs? s_pendingNormalSpawnArgs;
    private static PendingSpawnArgs? s_pendingSpecialSpawnArgs;

    /// <summary>排队一个只在营业场景循环的服务作用域内执行的重放动作。</summary>
    internal static void EnqueueReplay(string tag, Action<IWorkSceneServices> apply)
        => s_pendingReplays.Add((tag, apply));

    /// <summary>原补丁的统一前置条件：非联机同步期间一律不介入。</summary>
    private static bool SyncActive => !GameFlow.ShouldSkipAction && GameSession.HasRoomPeers;

    public void Setup(IWorkSceneServices services)
    {
        _seatingFromQueue = 0;
        _driverArmed = false;
        _spawnGateHeld = false;
        s_pendingNormalSpawnArgs = null;
        s_pendingSpecialSpawnArgs = null;
        s_pendingReplays.Clear();
        ApplyGates(services);
    }

    public void Shutdown(IWorkSceneServices services)
    {
        _seatingFromQueue = 0;
        _driverArmed = false;
        _spawnGateHeld = false;
        s_pendingNormalSpawnArgs = null;
        s_pendingSpecialSpawnArgs = null;
        s_pendingReplays.Clear();
    }

    public void Update(IWorkSceneServices services, float delta)
    {
        s_scopeServices = services;
        try
        {
            // 重放：顾客 FSM 的阻塞待处理项在本帧的服务作用域内执行，随后执行本帧排队的重放意图。
            GuestsMap.TickAllPending();
            DrainReplays(services);

            var eventManager = EventManager.Instance;
            if (eventManager == null) return;

            ApplyGates(services);
            if (!SyncActive) return;

            // 客机：原补丁靠 Spawn*GuestGroup 前缀短路「客机跳过」，新接口只能改写、不能取消，
            // 因此直接停掉原版刷客协程（受邀客人循环不受 SetSpawnEnabled 约束，是这里的重点）。
            if (GameSession.IsRoomClient) StopClientInstantiateLoops(eventManager);

            if (!InstantiateLoopsStarted(eventManager) || eventManager.HasTimeDepeleted) return;

            // 原版三条循环在同一时刻启动，间隔基准也随之对齐（原版 lastSpawnTimeStamp = Time.time）。
            if (!_driverArmed)
            {
                _driverArmed = true;
                _nextNormalInterval = 0f;
                _nextPasserbyInterval = 0f;
                _lastNormalSpawnTime = Time.time;
                _lastPasserbySpawnTime = Time.time;
                _lastSpecialSpawnTime = Time.time;
            }

            // 原版周期逻辑迁到模组侧：间隔与门槛沿用 EventManager 的公式。
            DrivePasserbySpawn(eventManager);
            if (!GameSession.IsRoomHost) return;

            DriveNormalSpawn(services, eventManager);
            DriveSpecialSpawn(services, eventManager);
        }
        finally
        {
            s_scopeServices = null!;
        }
    }

    /// <summary>执行本帧排队的重放意图。意图由消息处理器或顾客 FSM 投递，只在这里调用游戏服务。</summary>
    private static void DrainReplays(IWorkSceneServices services)
    {
        if (s_pendingReplays.Count == 0) return;

        var pending = s_pendingReplays.ToArray();
        s_pendingReplays.Clear();
        foreach (var (tag, apply) in pending)
        {
            Log.LogInfo($"Replaying {tag}");
            apply(services);
        }
    }

    /// <summary>
    /// 开关维护。原补丁逐调用判定「主机放行 / 客机短路」，这里每帧同步同一取向；
    /// 非同步期间（剧情/无对端）恢复为放行，与原补丁「直接放行原版」一致。
    /// </summary>
    private void ApplyGates(IWorkSceneServices services)
    {
        var client = SyncActive && GameSession.IsRoomClient;

        // 入座/订单/离场/打烊：主机放行原版（通知与服务由此产生），客机短路。
        services.Guests.SetSeatingEnabled(!client);
        services.Guests.SetOrderingEnabled(!client);
        services.Guests.SetLeaveEnabled(!client);
        services.Izakaya.SetCloseEnabled(!client);
        // 客机本地倒计时不得自行打烊，等主机广播完整关闭路径（原 ModifyTotalTime_Prefix）。
        services.Izakaya.SetTimeCloseEnabled(!client);

        // 评价：客机硬门控（原 EvaluateOrder 前缀只在重放时才放行原版），主机照常评价。
        services.Guests.SetEvaluationEnabled(!client);

        // 联机时人气标签不再折算本机的好恶（原 SellablePatch 的 GetPopTag 前缀）。
        // 非联机（含剧情）恢复原版计算。
        services.Economy.SetPopularityTagsEnabled(!GameSession.HasRoomPeers);

        ApplySpawnGate(services);
    }

    /// <summary>
    /// 刷客开关：同步期间两侧都关（主机由模组主动生成，客机完全不生成）；作弊流速为 0 时也关掉
    /// （原 <c>StartGuestInstantiateLoop/StartChallengeGuestInstantiateLoop</c> 前缀跳过循环启动，
    /// 客机不受该作弊判定约束）。
    /// 该开关是游戏自身的状态（时间轴 <c>NS_Set_DoSpawnNGuest_Behaviour</c>、镜华教学都会改写），
    /// 因此只在需要抑制时逐帧重申关闭，退出抑制时还原进入前的取值，不覆盖游戏自身的判定。
    /// </summary>
    private void ApplySpawnGate(IWorkSceneServices services)
    {
        var suppress = SyncActive || (!GameSession.IsRoomClient && ConfigManager.CheatFlowRate.Value == 0f);
        if (suppress)
        {
            if (!_spawnGateHeld)
            {
                _spawnGateHeld = true;
                _spawnGateOriginal = EventManager.Instance?.ShouldGuestSpawn ?? true;
            }

            services.Guests.SetSpawnEnabled(false);
            return;
        }

        if (!_spawnGateHeld) return;

        _spawnGateHeld = false;
        services.Guests.SetSpawnEnabled(_spawnGateOriginal);
    }

    /// <summary>原版刷客循环是否已启动；<c>EventManager.OnTiming</c> 在开场等待后才会启动它们。</summary>
    private static bool InstantiateLoopsStarted(EventManager eventManager)
        => eventManager.onNormalGuestInstantiateLoop != null;

    /// <summary>
    /// <see cref="EventManager.ShouldGuestSpawn"/> 只能约束普客/路人/稀客抽取循环，受邀客人循环不受它约束，
    /// 原补丁靠 <c>SpawnSpecialGuestGroup</c> 前缀短路；客机在此停掉全部刷客协程。
    /// </summary>
    private static void StopClientInstantiateLoops(EventManager eventManager)
    {
        if (eventManager.onNormalGuestInstantiateLoop == null) return;
        eventManager.StopGuestInstantiateLoop();
    }

    /// <summary>
    /// 原 <c>EventManager.OnNormalGuestInstantiate</c>（主客机各自本地刷普客，模组只重抽主机侧）。
    /// </summary>
    private void DriveNormalSpawn(IWorkSceneServices services, EventManager eventManager)
    {
        if (!eventManager.ShouldNormalGuestSpawn || !eventManager.ShouldGuestSpawnByBuff) return;
        if (_nextNormalInterval * eventManager.TotalGuestSpawnSpeed(GuestsManager.GuestType.Normal) + _lastNormalSpawnTime > Time.time) return;

        SpawnNormalGroup(services);
        _lastNormalSpawnTime = Time.time;
        _nextNormalInterval = UnityEngine.Random.Range(
            IzakayaConfigure.Instance.NormalGuestInterval.x,
            IzakayaConfigure.Instance.NormalGuestInterval.y);
    }

    /// <summary>
    /// 原 <c>GuestsManagerPatch.SpawnNormalGuestGroup_Prefix</c> 的主机分支：重抽到全局可用的普客组再生成。
    /// </summary>
    private void SpawnNormalGroup(IWorkSceneServices services)
    {
        var cook = CookSystemManager.Instance;
        for (var attempt = 0; attempt < MaxNormalGuestRerollAttempts; attempt++)
        {
            var guestGroups = cook?.GetRandomNormalGuestGroups();
            if (guestGroups == null)
            {
                Log.Error("CookSystemManager failed to GetRandomNormalGuestGroups.");
                return;
            }

            var guests = guestGroups.ToArray();
            if (!IsNormalGuestGroupAvailable(guests)) continue;

            services.Guests.SpawnNormal(guests, -1);
            return;
        }

        Log.Warning("No globally available normal guest group found after reroll, skipping spawn.");
    }

    /// <summary>
    /// 原 <c>EventManager.OnPasserbyGuestInstantiate</c>。路人客人不参与同步，主客机各自本地生成，保持原行为。
    /// </summary>
    private void DrivePasserbySpawn(EventManager eventManager)
    {
        if (!IzakayaConfigure.Instance.SpawnPasserbyGuest) return;
        if (!eventManager.ShouldNormalGuestSpawn || !eventManager.ShouldGuestSpawnByBuff) return;
        if (_nextPasserbyInterval * eventManager.TotalGuestSpawnSpeed(GuestsManager.GuestType.Normal) + _lastPasserbySpawnTime > Time.time) return;

        eventManager.CallExternOnPasserbyGuestInstantiate();
        _lastPasserbySpawnTime = Time.time;
        _nextPasserbyInterval = UnityEngine.Random.Range(
            IzakayaConfigure.Instance.PasserbyGuestSpanInterval.x,
            IzakayaConfigure.Instance.PasserbyGuestSpanInterval.y);
    }

    /// <summary>
    /// 原 <c>EventManager.OnSpecialGuestInstantiate</c> 的主机分支：抽取稀客后按全局可用性重抽。
    /// </summary>
    private void DriveSpecialSpawn(IWorkSceneServices services, EventManager eventManager)
    {
        if (!eventManager.ShouldGuestSpawnByBuff) return;
        var configure = IzakayaConfigure.Instance;
        if (!configure.CanGacha) return;
        if (configure.SpecialGuestGachaInterval * eventManager.TotalGuestSpawnSpeed(GuestsManager.GuestType.Special) + _lastSpecialSpawnTime > Time.time) return;

        _lastSpecialSpawnTime = Time.time;

        var id = configure.Gacha();
        if (id == -1) return; // 原版仅在抽取成功时才触发生成回调

        if (!TryResolveAvailableSpecialGuest(ref id))
        {
            Log.Warning("No globally available special guest found, skipping spawn.");
            return;
        }

        services.Guests.SpawnSpecial(id, -1);
    }

    /// <summary>原 <c>TryResolveAvailableSpecialGuest</c>。</summary>
    private static bool TryResolveAvailableSpecialGuest(ref int id)
    {
        var configure = IzakayaConfigure.Instance;
        var maxAttempts = (configure.SpecialGuestPoolIdentityData?.Length ?? 0) + 1;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            if (id != -1)
            {
                if (PlayerManager.SpecialGuestAvailable(id)) return true;

                configure.SetThisGuestHasSpawned(id);
                Log.Warning($"Special guest {id} is not available for all players, rerolling.");
            }

            if (!configure.CanGacha) return false;
            id = configure.Gacha();
        }

        return false;
    }

    /// <summary>原 <c>TryReplaceUnavailableNormalGuests</c>：把全局不可用的普客逐一替换为可用普客。</summary>
    public void OnNormalGuestsGenerating(ref List<NormalGuest> guests)
    {
        if (!SyncActive || !GameSession.IsRoomHost) return;

        for (var index = 0; index < guests.Count; index++)
        {
            var guest = guests[index];
            if (PlayerManager.NormalGuestAvailable(guest.id)) continue;

            if (!TryGetFallbackNormalGuest(out var replacement))
            {
                // 找不到替换时不改写；调用方（模组自驱或原版生成）按可用性判定是否放弃这次生成。
                Log.Warning($"Normal guest {guest.id} is not available for all players and no fallback was found.");
                continue;
            }

            Log.Warning($"Normal guest {guest.id} is not available for all players, replacing with {replacement.id}.");
            guests[index] = replacement;
        }
    }

    /// <summary>
    /// 原 <c>SpawnNormalGuestGroup(*)_Prefix</c> 的方法级短路（<see cref="OnNormalGuestsGenerating"/> 只改写生成列表）：
    /// 客机不生成顾客，主机保留本次生成参数供 <see cref="OnGroupSpawned"/> 广播。
    /// </summary>
    public void OnPreSpawnNormalGuests(ref GuestSpawnRequest request, ref bool cancelInvocation)
    {
        if (!SyncActive) return;

        if (GameSession.IsRoomClient)
        {
            Log.Info("Skipping SpawnNormalGuestGroup on client");
            cancelInvocation = true;
            return;
        }

        s_pendingNormalSpawnArgs = PendingSpawnArgs.FromRequest(request);
    }

    /// <summary>原 <c>SpawnSpecialGuestGroup_Prefix</c> 的方法级短路、重抽与参数暂存。</summary>
    public void OnPreSpawnSpecialGuest(ref GuestSpawnRequest request, ref int guestId, ref bool cancelInvocation)
    {
        if (!SyncActive) return;
        if (IsReimuProtectionGuestId(guestId)) return; // 灵梦赛钱箱不参与同步

        if (GameSession.IsRoomClient)
        {
            Log.Info("Skipping SpawnSpecialGuestGroup on client");
            cancelInvocation = true;
            return;
        }

        // 重抽到全局可用的稀客；重抽失败时与原补丁一致放弃本次生成。
        if (!TryResolveAvailableSpecialGuest(ref guestId))
        {
            Log.Warning("No globally available special guest found, skipping spawn.");
            cancelInvocation = true;
            return;
        }

        s_pendingSpecialSpawnArgs = PendingSpawnArgs.FromRequest(request);
    }

    /// <summary>原 <c>SpawnSpecialGuestGroup_Prefix</c> 的稀客重抽：灵梦赛钱箱（防保护）不参与重抽。</summary>
    public void OnSpecialGuestGenerating(ref int guestId)
    {
        if (!SyncActive || !GameSession.IsRoomHost) return;
        if (IsReimuProtectionGuestId(guestId)) return;

        TryResolveAvailableSpecialGuest(ref guestId);
    }

    /// <summary>原 <c>PostInitializeGuestGroup_Prefix</c>：主机把刚生成的顾客组连同生成参数广播给客机。</summary>
    public void OnGroupSpawned(GuestGroupController group, GuestSpawnRequest request)
    {
        if (IsReimuProtectionGuest(group)) return;
        if (!SyncActive) return;
        if (!GameSession.IsRoomHost) return;

        // 生成参数取 OnPreSpawn* 暂存的那份（与原补丁一致，按顾客类型分别暂存）；
        // 未被暂停存覆盖的生成途径回落到框架随通知给出的请求。
        GuestFSM.OnSpawn(group, ConsumeSpawnArgs(group.ControllType) ?? PendingSpawnArgs.FromRequest(request));
    }

    /// <summary>取走对应顾客类型在生成时暂存的参数。</summary>
    private static PendingSpawnArgs? ConsumeSpawnArgs(GuestsManager.GuestType guestType)
    {
        switch (guestType)
        {
            case GuestsManager.GuestType.Normal:
                var normal = s_pendingNormalSpawnArgs;
                s_pendingNormalSpawnArgs = null;
                return normal;
            case GuestsManager.GuestType.Special:
                var special = s_pendingSpecialSpawnArgs;
                s_pendingSpecialSpawnArgs = null;
                return special;
            default:
                return null;
        }
    }

    /// <summary>
    /// 原 <c>PlayerRepell_Prefix</c>：主机放行原版赶客；客机把请求发给主机并取消本地赶客
    /// （离场开关关掉后 <c>OnGroupLeft(PlayerRepelled)</c> 不再产生，请求入口由本回调承担）。
    /// </summary>
    public void OnPrePlayerRepel(int deskCode, ref bool cancelInvocation)
    {
        if (!SyncActive) return;
        if (GameSession.IsRoomHost) return;

        GuestFSM.OnPlayerRepell(deskCode);
        cancelInvocation = true;
    }

    /// <summary>原 <c>GuestService.HijackCheckAndSendFromQueue</c> 的广播侧：出队入座同步。</summary>
    public void OnGroupSeated(GuestGroupController group, int desk)
    {
        if (!SyncActive) return;
        if (!GameSession.IsRoomHost) return;
        if (group.Pointer != _seatingFromQueue) return;

        _seatingFromQueue = 0;
        GuestFSM.OnSendFromQueue(group);
    }

    /// <summary>原 <c>GuestsManager__c__DisplayClass174_0Patch.GenerateOrderInternal_Postfix</c>：主机捕获订单并广播。</summary>
    public void OnGroupOrderGenerated(GuestGroupController group, GuestsManager.OrderGenerationResult result, ref GuestsManager.OrderBase order)
    {
        if (!SyncActive) return;
        if (!GameSession.IsRoomHost) return;

        GuestFSM.OnGenerateOrderInternal(result, group, order);
    }

    /// <summary>
    /// 原 <c>TryOverrideEvaluateByBuff_Postfix</c>（评价结果同步）与 <c>EvaluateOrder_Postfix</c>
    /// （Evaluating → EatingDelay 推进）。手动（幽幽子）评价走 <c>EvaulateManualOrder</c>，原补丁未覆盖，这里同样不介入。
    /// </summary>
    public void OnGroupEvaluated(GuestGroupController group, ref GuestGroupController.EvaluationResult result)
    {
        if (!SyncActive) return;

        var fsm = GuestsMap.GetGuestFsm(group);
        if (fsm == null || fsm.IsManualGuest || fsm.IsRepelling) return;

        if (GameSession.IsRoomHost)
        {
            GuestFSM.OnEvaluateOrder(group, result);
            GuestFSM.OnEatingDelay(group);
            return;
        }

        if (GameSession.IsRoomClient)
        {
            if (fsm.OverrideEvalResult == GuestGroupController.EvaluationResult.Null) return;

            result = fsm.OverrideEvalResult;
            if (fsm.CurrentState == GuestFSM.State.Evaluating) GuestFSM.OnEatingDelay(group);
        }
    }

    /// <summary>
    /// 评价回调结束后的推进（原 <c>NormalGuestsControllerPatch.NormalGuest_PostEvaluation_Postfix</c> 与
    /// <c>SpecialGuestsControllerPatch.PostEvaluation</c> 的前缀 + 后缀）：幽幽子本体先同步评价数据与状态，
    /// 其余在场顾客推进 EatingDelay -> ContinueDecision。
    /// </summary>
    /// <remarks>
    /// 框架把两条 PostEvaluation 都合成本通知，因此本体的 <c>BeforePostEvaluation</c> 落在原版
    /// <c>PostEvaluation</c> 体之后（原补丁在前缀里、体之前）。
    /// </remarks>
    public void OnGroupPostEvaluated(GuestGroupController group, GuestGroupController.EvaluationResult result)
    {
        if (YuyukoGuestSync.IsBody(group))
        {
            YuyukoGuestSync.BeforePostEvaluation(group, result);
            return;
        }

        if (!SyncActive) return;
        GuestFSM.OnPostEvaluation(group);
    }

    /// <summary>原 <c>RefreshCurrentFundAndOrder_Prefix</c>：到达桌位。</summary>
    public void OnGroupArrived(GuestGroupController group)
    {
        if (YuyukoGuestSync.IsBody(group)) return;
        if (!SyncActive) return;

        if (GameSession.IsRoomHost)
        {
            GuestFSM.OnRefreshCurrentFundAndOrder(group);
            return;
        }

        // 客机的落座由重放的 IWorkSceneGuests.Seat 驱动，到达回调在这里把 SeatMoving 推进到 SeatedDelay
        // （原 ReplayTrySendToSeat 的 OnSit 侧）。
        GuestFSM.ClientGuestGroupOnArrive(group);
    }

    /// <summary>原 <c>MoveToDesk_Prefix</c>：入座方向同步；出队入座的来源在此标记。</summary>
    public void OnGroupMovingToDesk(GuestGroupController group, int desk)
    {
        if (YuyukoGuestSync.IsBody(group)) return;
        if (IsReimuProtectionGuest(group)) return;
        if (!SyncActive) return;
        if (!GameSession.IsRoomHost) return;

        if (GuestsMap.GetGuestFsm(group)?.CurrentState == GuestFSM.State.Queued) _seatingFromQueue = group.Pointer;

        GuestFSM.OnMoveToDesk(group, desk);
    }

    /// <summary>原 <c>MoveToQueue_Postfix</c>：座满先入队。</summary>
    public void OnGroupQueued(GuestGroupController group)
    {
        if (!SyncActive) return;
        if (!GameSession.IsRoomHost) return;

        GuestFSM.OnMoveToQueue(group);
    }

    /// <summary>
    /// 原 <c>PlayerRepell_Prefix</c> / <c>RepellInternal_Prefix</c> / <c>PatientDepletedLeave_Prefix</c> /
    /// <c>LeaveFromDesk_Prefix</c> 的主机广播侧。
    /// 离场类型取控制器自身的 <see cref="GuestGroupController.FinalLeaveType"/>（原补丁转发的是
    /// <c>LeaveFromDesk</c> 形参，游戏内部同样以 FinalLeaveType 结算）；<c>triggerLeaveBuff</c> 沿用原版默认 true。
    /// 嵌套离场由框架的离场 seam 只派发最外层一次，原 <c>GuestReentryPermits</c> 的嵌套放行不再需要。
    /// </summary>
    public void OnGroupLeft(GuestGroupController group, GuestLeaveKind kind)
    {
        if (IsReimuProtectionGuest(group)) return;
        if (YuyukoGuestSync.IsBody(group)) return;
        if (LeaveBroadcastSuspended) return;
        if (!SyncActive) return;
        if (!GameSession.IsRoomHost) return;

        switch (kind)
        {
            case GuestLeaveKind.RepelledPaid:
            case GuestLeaveKind.RepelledUnpaid:
            case GuestLeaveKind.PlayerRepelled:
                GuestFSM.OnRepell(group);
                GuestFSM.OnLeaveFromDesk(group, group.FinalLeaveType, true);
                break;

            case GuestLeaveKind.Patience:
                GuestFSM.OnPatientDepletedAtDesk(group);
                break;

            default:
                GuestFSM.OnLeaveFromDesk(group, group.FinalLeaveType, true);
                break;
        }
    }

    /// <summary>原 <c>TryCloseIzakaya_Prefix</c> 的主机分支：广播打烊。</summary>
    public void OnIzakayaClosing()
    {
        if (!SyncActive) return;
        if (!GameSession.IsRoomHost) return;

        IzakayaCloseMessage.Send();
    }

    /// <summary>
    /// 客机重放主机打烊，等价原 <c>EventManager.StopInstantiationLoopAndCloseIzakaya</c>
    /// （停刷客循环 + 时间耗尽 + 关店）；其中被关店开关拦住的 <c>GuestsManager.TryCloseIzakaya</c>
    /// 改走 <c>IWorkSceneIzakaya.Close</c>。由 <c>IzakayaCloseMessage</c> 排队到营业场景循环内执行。
    /// </summary>
    internal static void ReplayIzakayaClose(IWorkSceneServices services)
    {
        var eventManager = EventManager.Instance;
        if (eventManager == null)
        {
            Log.Warning("EventManager is null when replaying host close.");
            return;
        }

        eventManager.StopGuestInstantiateLoop();
        eventManager.SetTimeDepeleted();
        services.Izakaya.Close();
        IzakayaCloseMessage.UnblockClientCloseWait(eventManager);
    }

    private static bool IsNormalGuestGroupAvailable(IReadOnlyList<NormalGuest> guests)
        => guests.Count > 0 && guests.All(guest => PlayerManager.NormalGuestAvailable(guest.id));

    private static bool TryGetFallbackNormalGuest(out NormalGuest guest)
    {
        var candidates = DataBaseCharacter.GetAllNormalGuests()
            .ToArray()
            .Where(candidate => PlayerManager.NormalGuestAvailable(candidate.id))
            .ToArray();
        if (candidates.Length <= 0)
        {
            guest = null;
            return false;
        }

        guest = candidates[UnityEngine.Random.Range(0, candidates.Length)];
        return true;
    }

    private static bool IsReimuProtectionGuestId(int id)
        => RunTimeSchedulerGapsPatch.IsDuringReimuProtection && id == ReimuProtectionGuestId;

    private static bool IsReimuProtectionGuest(GuestGroupController controller)
    {
        if (!RunTimeSchedulerGapsPatch.IsDuringReimuProtection
            || controller == null
            || controller.ControllType != GuestsManager.GuestType.Special)
        {
            return false;
        }

        var guests = controller.GetAllGuests().ToArray();
        return guests.Length == 1 && guests[0].Id == ReimuProtectionGuestId;
    }
}
