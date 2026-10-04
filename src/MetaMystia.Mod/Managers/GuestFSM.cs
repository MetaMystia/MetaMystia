using System;
using System.Collections.Generic;
using System.Linq;

using Il2CppInterop.Runtime;
using Il2CppSystem.Linq;
using UnityEngine;

using GameData.Core.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;
using NightScene.Tiles;

using Mystia.Listeners;
using Mystia.Scenes;

using MetaMystia.Multiplayer;
using MetaMystia.Listeners;
using MetaMystia.Multiplayer.Messages;
using SgrYuki.Utils;
using static NightScene.GuestManagementUtility.GuestsManager;

namespace MetaMystia;

[AutoLog]
public partial class GuestFSM
{
    public enum State
    {
        None,               // 尚未接收到任何顾客生命周期事件
        Constructed,        // 控制器已创建，但还未确定入队还是入座
        Queued,             // 正在等待位排队，尚未占桌
        SeatMoving,         // 已分配桌位，角色正在移动到座位
        SeatedDelay,        // 已落座，处于首单前的短暂延时
        WaitingServe,       // 订单已打开，正在等待料理和酒水送达
        Evaluating,         // 已开始评价，本单不再接受服务
        EatingDelay,        // 评价表现与吃饭动画等待中
        ContinueDecision,   // 评价结束，正在决定续单还是离开
        Leaving,            // 已开始离桌，正在收尾
        Left,               // 实体已彻底离开场景，本轮生命周期结束
        Manual,             // 手动顾客轨道，由外部脚本驱动
        Dead                // 联机崩溃的顾客，已被清理
    }

    public State CurrentState { get; private set; } = State.None;

    /// <summary>框架给的顾客身份，一次营业会话内有效。取代原来的控制器引用。</summary>
    public GuestHandle Handle { get; set; }

    /// <summary>句柄在本会话内的投影；句柄已过期（换过场）时为 null。</summary>
    public GuestProxy? Proxy => Handle.TryGet(out var guest) ? guest : null;

    /// <summary>普通顾客 / 特殊顾客。框架侧是 <see cref="GuestKind"/>，两者取值一致，模组侧仍用自己的域模型。</summary>
    public GuestType GuestType { get; private set; }

    public int[] Ids { get; private set; }
    public int Fund { get; private set; }
    public int MaxFundCarry { get; private set; }

    /// <summary>联机消息里的顾客身份，由 <see cref="GuestsMap.StoreGuest(int, GuestFSM)"/> 写入。</summary>
    public int RuntimeId { get; internal set; }

    public int DeskCode => Proxy?.DeskCode ?? -1;

    /// <summary>当前正在考虑的那一单（订单栈顶）；没有订单时为 null。</summary>
    public OrderProxy? CurrentOrder => Proxy is { } guest && guest.TryGetPendingOrder(out var order) ? order : null;

    public int OrderSeq => IsManualGuest ? manualOrderSeq : Proxy?.PendingOrderCount ?? -1;

    public bool IsFirstOrder { get; private set; } = true; // TODO: OrderSeq == 1 ?
    public bool IsRepelling { get; private set; }

    /// <summary>
    /// 本端记着的待上菜槽位，内容是消息里传递的菜品。写入订单槽位或面板时经
    /// <c>IWorkSceneServices.Dishes.DishOf</c> 换成框架的菜品投影。
    /// </summary>
    public Sellable WillServeBeverage { get; set; }
    public Sellable WillServeFood { get; set; }

    /// <summary>客机重放评价时用来覆写结果。取值与游戏的 <c>EvaluationResult</c> 一一对应。</summary>
    public GuestEvaluation OverrideEvalResult { get; set; } = GuestEvaluation.None;

    private const int PendingTtlMs = 30000;
    private readonly Queue<Pending> _pending = new();
    private bool _draining; // 标记位，避免 Drain 嵌套

    private static void FlowLog(string message)
    {
#if DEBUG
        Log.Warning(message);
#else
        Log.Info(message);
#endif
    }

    private sealed class Pending
    {
        public string Tag;
        public System.Func<bool> Apply;
        public long DeadlineMs;
    }

    public void Enqueue(string tag, System.Func<bool> apply, int ttlMs = PendingTtlMs)
    {
        if (CurrentState == State.Dead || CurrentState == State.Left)
        {
            FlowLog($"Guest #{RuntimeId} Enqueue '{tag}' rejected: FSM={CurrentState}");
            return;
        }
        _pending.Enqueue(new Pending
        {
            Tag = tag,
            Apply = apply,
            DeadlineMs = MetaMystia.Multiplayer.RoomClock.Now + ttlMs,
        });
        Drain();
    }

    /// <summary>
    /// 营业场景服务作用域内的服务。重放只能由 <see cref="GuestSync"/> 的
    /// <c>IWorkSceneGameLoop.Update</c> 驱动，因此 <see cref="Drain"/> 能执行时它一定非空。
    /// </summary>
    private static IWorkSceneServices Services => GuestSync.ScopedServices;

    /// <summary>
    /// 检测单个顾客的阻塞的待处理项并尝试执行。每次执行完一项都重新检查队首，直到遇到未过期但无法执行的项为止。
    /// 重放会调用被开关拦住的游戏方法，只能在营业场景循环的服务作用域内执行；作用域外只入队，等下一次 Update。
    /// </summary>
    private void Drain()
    {
        if (_draining) return;
        if (Services == null) return;
        if (IsManualGuest && GameFlow.InStory) return;
        _draining = true;
        try
        {
            while (_pending.Count > 0)
            {
                var head = _pending.Peek();
                if (MetaMystia.Multiplayer.RoomClock.Now > head.DeadlineMs)
                {
                    Log.Error($"Guest #{RuntimeId} pending '{head.Tag}' timeout: stalled at {CurrentState}");
                    if (IsManualGuest)
                    {
                        // 剧情可能长于普通顾客的 TTL；本体仍被挑战协程引用，不能销毁。
                        head.DeadlineMs = MetaMystia.Multiplayer.RoomClock.Now + PendingTtlMs;
                    }
                    else
                    {
                        _pending.Clear();
                        Kill();
                        return;
                    }
                }
                if (!head.Apply())
                {
                    return;
                }

                if (CurrentState == State.Dead || CurrentState == State.Left)
                {
                    return;
                }

                if (_pending.Count > 0 && ReferenceEquals(_pending.Peek(), head))
                {
                    _pending.Dequeue();
                }
            }
        }
        finally { _draining = false; }
    }

    /// <summary>
    /// 每帧固定触发：在没有任何 To/Enqueue 触发时，仍能持续尝试检查柄执行待处理项。
    /// </summary>
    internal void TickPending()
    {
        if (_pending.Count == 0) return;
        Drain();
    }

    /// <summary>
    /// 主机 Hook 到顾客创建事件，获取顾客类型、ids、金钱等基本信息，注册顾客并广播 GuestSpawnMessage
    /// </summary>
    /// <param name="controller"></param>
    public static void OnSpawn(GuestHandle handle, PendingSpawnArgs? spawnArgs = null)
    {
        if (!handle.TryGet(out var guest))
        {
            Log.Error($"Guest spawned with a handle that does not resolve: {handle}");
            return;
        }

        var fsm = new GuestFSM
        {
            CurrentState = State.Constructed,
            Handle = handle,
            // 框架的 GuestKind 与游戏的 GuestType 取值一一对应（Normal = 0，Special = 1）。
            GuestType = (GuestType)(int)guest.Kind,
            Fund = guest.Fund,
            MaxFundCarry = guest.MaxFundCarry,
            Ids = guest.GuestIds.ToArray(),
        };

        GuestsMap.StoreGuest(fsm);
        var spawnInfo = new GuestSpawnInfo
        {
            GuestType = fsm.GuestType,
            Ids = fsm.Ids,
            Fund = fsm.Fund,
            MaxFundCarry = fsm.MaxFundCarry,
        };

        if (spawnArgs.HasValue)
        {
            var args = spawnArgs.Value;
            spawnInfo.HasNormalSpawnArgs = fsm.GuestType == GuestType.Normal;
            spawnInfo.HasSpecialSpawnArgs = fsm.GuestType == GuestType.Special;
            spawnInfo.GuestSpawnType = args.GuestSpawnType;
            spawnInfo.HasOverrideSpawnPosition = args.HasOverrideSpawnPosition;
            spawnInfo.OverrideSpawnX = args.OverrideSpawnPosition.X;
            spawnInfo.OverrideSpawnY = args.OverrideSpawnPosition.Y;
            spawnInfo.OverrideSpawnZ = args.OverrideSpawnPosition.Z;
            spawnInfo.LeaveType = (GuestGroupController.LeaveType)(int)args.LeaveType;
            spawnInfo.TargetDeskCode = args.TargetDeskCode;
            spawnInfo.ShouldFade = args.ShouldFade;
        }

        GuestSpawnMessage.Send(fsm.RuntimeId, spawnInfo);
    }

    /// <summary>
    /// 客机收到主机发来的顾客生成事件，注册顾客并重放顾客生成逻辑，但阻止生成的后续移动逻辑
    /// </summary>
    /// <param name="runtimeId"></param>
    /// <param name="guestSpawnInfo"></param>
    public static void DoSpawn(int runtimeId, GuestSpawnInfo guestSpawnInfo)
    {
        // 客机此刻还没有句柄：组由重放生成、框架铸造出组之后才知道，届时有 GuestService 调 GuestsMap.Bind。
        var fsm = new GuestFSM
        {
            CurrentState = State.Constructed,
            GuestType = guestSpawnInfo.GuestType,
            Ids = guestSpawnInfo.Ids,
            Fund = guestSpawnInfo.Fund,
            MaxFundCarry = guestSpawnInfo.MaxFundCarry,
        };

        GuestsMap.StoreGuest(runtimeId, fsm);

        if (fsm.GuestType == GuestType.Normal)
        {
            GuestService.ReplaySpawnNormalGuestGroupExtern(ref fsm, guestSpawnInfo, Services);
            return;
        }
        if (fsm.GuestType == GuestType.Special)
        {
            GuestService.ReplaySpawnSpecialGuestGroup(ref fsm, guestSpawnInfo, Services);
            return;
        }

        Log.Error($"Guest #{fsm.RuntimeId} spawned with {fsm.GuestType}");
    }

    /// <summary>
    /// 主机用于广播顾客组和桌号的入座信息
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="deskCode"></param>
    public static void OnMoveToDesk(GuestHandle handle, int deskCode)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);

        // 情况一：顾客组刚生成即可入座
        // 主机调用栈: PostInitializeGuestGroup -> TrySendToSeat(firstSpawn: true) -> MoveToDesk
        // FSM: Constructed -> SeatMoving

        // 情况二：顾客组因座满先入队，后被送出队伍入座
        // 主机调用栈: CheckAndSendFromQueue -> TrySendToSeat(firstSpawn: false) -> MoveToDesk
        // FSM: Queued -> SeatMoving
        if (fsm.CurrentState == State.Constructed || fsm.CurrentState == State.Queued)
        {
            MoveToDeskMessage.Send(fsm.RuntimeId, deskCode);
            fsm.To(State.SeatMoving);
            FlowLog($"Guest #{fsm.RuntimeId} moved to desk {deskCode}");
            return;
        }

        fsm.Kill(State.SeatMoving);
    }

    /// <summary>
    /// 客机用于将顾客组送往指定桌号，来源可以是 Constructed 和 Queued，Constructed 为刚生成即入座，Queued 为座满，先入队再出队入座
    /// </summary>
    public static bool DoMoveToDesk(int runtimeId, int deskCode)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm?.Proxy is not { } guest) return false;
        if (fsm.CurrentState != State.Constructed && fsm.CurrentState != State.Queued) return false;
        if (deskCode < 0) return false;

        var deskAvailable = GuestsManager.Instance.TrueAvailableDesks.TryGetValue(deskCode, out var capacity) &&
                            capacity >= guest.GuestCount;
        if (!deskAvailable)
        {
            // TODO: 能否直接返回 false
            // 桌位不足时只有「占着这张桌的正是本组」才继续，与原来按控制器指针比对等价。
            if (!Services.Guests.TryGetSeated(deskCode, out var seated)) return false;
            if (seated.Handle != fsm.Handle) return false;
        }

        // 目标桌位可用 => 直接尝试入座
        var firstSpawn = fsm.CurrentState == State.Constructed;
        if (!Services.Guests.Seat(fsm.Handle, deskCode, firstSpawn))
        {
            fsm.Kill(State.SeatMoving);
            return true;
        }
        fsm.To(State.SeatMoving);
        return true;
    }

    /// <summary>
    /// 因座满，主机刚生成的顾客组需要先入队时，主机同步入队事件
    /// </summary>
    /// <param name="controller"></param>
    public static void OnMoveToQueue(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm.CurrentState == State.Constructed)
        {
            fsm.To(State.Queued);
            FlowLog($"Guest #{fsm.RuntimeId} moved to queue, FSM: Constructed -> Queued");
            MoveToQueueMessage.Send(fsm.RuntimeId);
            return;
        }

        fsm.Kill(State.Queued);
    }

    /// <summary>
    /// 客机对 MoveToQueue 的重放，来源仅 Constructed，座满时直接入队，无法入队时应回退至 MoveToSpawn
    /// </summary>
    public static bool DoMoveToQueue(int runtimeId)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState != State.Constructed) return false;
        if (!Services.Guests.CanQueue(fsm.Handle)) return false; // 因队满而临界阻塞

        // 入队与耐心倒计时由框架按游戏自己的入队分支完成（含 SpawnGuest 登记）。客机不自驱耐心耗尽
        // （等主机的 PatientDepletedQueueMessage 同步），框架的 TryQueue 正是这个口径：只通知、不裁决。
        if (!Services.Guests.TryQueue(fsm.Handle)) return false;

        fsm.To(State.Queued);
        return true;

        // 如果客机始终无法入队(!CanQueue)，对应 PostInitializeGuestGroup 末尾的 MoveToSpawn(); 会因超时而自动 Kill
    }

    /// <summary>
    /// 客机提交玩家赶客请求，不推进状态。
    /// </summary>
    /// <param name="deskCode"></param>
    public static void OnPlayerRepell(int deskCode)
    {
        // 本方法由监听器回调触发，不在场景服务作用域内，因此按桌号从句柄投影里找（句柄解析不受作用域限制）。
        var fsm = GuestsMap.GetGuestFsmAtDesk(deskCode);
        if (fsm == null) return;

        PlayerRepellMessage.Send(fsm.RuntimeId);
    }

    /// <summary>
    /// 主机立即裁定赶客请求；过期请求不得等待下一次可赶客状态。
    /// </summary>
    /// <param name="runtimeId"></param>
    public static void DoPlayerRepell(int runtimeId)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (!GameSession.IsRoomHost || fsm?.Proxy is not { } guest) return;
        if (fsm.CurrentState is State.Leaving or State.Left or State.Dead || guest.HasLeft) return;
        var manager = GuestsManager.Instance;
        if (Services.Guests.TryGetSeated(guest.DeskCode, out var seated) && seated.Handle != fsm.Handle) return;
        if (!manager.CheckCanPlayerRepelGuest(guest.DeskCode)) return;

        manager.PlayerRepell(guest.DeskCode);
    }

    /// <summary>原版已决定驱赶；离桌入口发送结果，不能在玩家请求入口宣布成功。</summary>
    public static void OnRepell(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm == null || fsm.CurrentState is State.Left or State.Dead) return;
        fsm.IsRepelling = true;
        fsm.To(State.Leaving);
        TryCloseServePanel(fsm.DeskCode);
    }

    /// <summary>主机驱赶结果越过旧服务等待项，直接重放原版完整清理。</summary>
    public static void DoRepell(GuestRepellMessage result)
    {
        var runtimeId = result.RuntimeId;
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm?.Proxy is not { } guest || fsm.CurrentState is State.Left or State.Dead) return;
        if (Services.Guests.TryGetSeated(guest.DeskCode, out var seated) && seated.Handle != fsm.Handle)
        {
            Log.Warning($"Ignoring repell #{runtimeId}: desk is occupied by another guest");
            return;
        }

        fsm.IsRepelling = true;
        fsm.To(State.Left);
        guest.MarkLeft();
        guest.SetMood(result.Mood);
        var eventManager = NightScene.EventUtility.EventManager.Instance;
        eventManager.CurrentCombo = result.Combo;
        eventManager.LoseComboTimes = result.LoseComboTimes;
        eventManager.LoseComboTimeForPassion = result.LoseComboTimeForPassion;
        eventManager.LoseComboGuestSetNum = result.LoseComboGuestSetNum;
        eventManager.CallExternOnComboUpdate(result.Combo);
        eventManager.CallExternOnMusicIndexUpdate(eventManager.CurrentMusicLevelHandle.Invoke());
        TryCloseServePanel(guest.DeskCode);
        // 原版驱逐清理改走服务（服务内部放行被关掉的离场开关）。结果包里的 leaveType/triggerLeaveBuff 不再需要：
        // 原版 LeaveFromDesk 收到 Move 时会改用控制器自身的 FinalLeaveType 结算（游戏 GuestsManager.LeaveFromDesk:2637），
        // 而 RepellAndLeaveNoPay 内的 TriggerLeaveBuff 固定为 true，与主机侧的发送口径一致。
        Services.Guests.Leave(fsm.Handle, GuestLeaveKind.RepelledUnpaid);
    }

    /// <summary>
    /// 主机用于确定 SeatMoving => SeatedDelay 的状态更新。
    /// </summary>
    /// <param name="controller"></param>
    public static void OnRefreshCurrentFundAndOrder(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        FlowLog($"Guest #{fsm.RuntimeId} refreshed fund and order, current FSM state: {fsm.CurrentState}");

        if (fsm.CurrentState == State.SeatMoving)
        {
            fsm.To(State.SeatedDelay);
            return;
        }

        // do NOT kill
    }

    /// <summary>
    /// 客机落座回调，便于及时推进客机 SeatMoving => SeatedDelay 状态更新。
    /// 客机的落座由服务重放原版 <c>TrySendToSeat</c> 触发，到达时同样会走
    /// <c>RefreshCurrentFundAndOrder</c>，因此只在 SeatMoving 时推进；其它来源的刷新（法术等）不介入。
    /// </summary>
    /// <param name="controller"></param>
    public static void ClientGuestGroupOnArrive(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm == null || fsm.CurrentState != State.SeatMoving) return;

        FlowLog($"Guest #{fsm.RuntimeId} arrived at desk {fsm.DeskCode}, FSM: SeatMoving -> SeatedDelay");
        fsm.To(State.SeatedDelay);
    }


    /// <summary>
    /// 主机在 ReplayCheckAndSendFromQueue -> TrySendToSeat(firstSpawn: false) 后捕获可以出队入座的顾客组并广播。
    /// 但注意，主机端是先执行了 TrySendToSeat 然后才获知需要出队的顾客组，因此初始状态为 SeatMoving。也可考虑 Hook TrySendToSeat。
    /// </summary>
    /// <param name="controller"></param>
    public static void OnSendFromQueue(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        FlowLog($"Guest #{fsm.RuntimeId} sent from queue, current FSM state: {fsm.CurrentState}");

        if (fsm.CurrentState == State.SeatMoving)
        {
            fsm.To(State.SeatMoving);
            SendFromQueueMessage.Send(fsm.RuntimeId);
            return;
        }

        fsm.Kill(State.SeatMoving);
    }

    /// <summary>
    /// 客机对 CheckAndSendFromQueue 的部分重放，指定顾客出队入座。
    /// 但注意：客机是先重放了服务的入座（<c>IWorkSceneGuests.Seat</c> -> MoveToDesk），
    /// 然后才在此执行 OnLeaveQueueCallback 等一系列<b>后续</b>操作，因此初始状态为 SeatMoving。
    /// </summary>
    public static bool DoSendFromQueue(int runtimeId)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState == State.SeatMoving)
        {
            // 原实现里额外调用的 OnLeaveQueueCallback 全游戏无人赋值（GuestGroupController.cs:227 只声明），
            // 是死回调，因此只保留「停在排队耐心倒计时」这一步。
            Services.Guests.StopPatientCountdown(fsm.Handle);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 主机 Hook GenerateOrderInternal 捕获订单信息，预测覆盖结果并广播
    /// </summary>
    /// <param name="orderGenerationResult"></param>
    /// <param name="controller"></param>
    /// <param name="orderData"></param>
    public static void OnGenerateOrderInternal(GuestHandle handle, OrderGenerationOutcome result, OrderProxy order)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm?.Proxy is not { } guest) return;
        FlowLog($"Guest #{fsm.RuntimeId} generated order, current FSM state: {fsm.CurrentState}");

        if (fsm.CurrentState == State.SeatedDelay || fsm.CurrentState == State.ContinueDecision)
        {
            // 框架的结果枚举与游戏的 OrderGenerationResult 取值一一对应（桥接 Mirrors 负责映射），
            // 消息与模组内部仍以游戏枚举表达。
            var orderGenerationResult = (OrderGenerationResult)(int)result;
            OrderGenerationResult? overrideResult = null;
            if (guest.Kind == GuestKind.Special)
            {
                // 游戏中仅对 Special Guest 执行无副作用的 CheckRemainingFund 以做等价预测
                overrideResult = CheckRemainingFund(orderGenerationResult, guest);
            }
            GenerateOrderMessage.Send(fsm.RuntimeId, orderGenerationResult, overrideResult, order);

            var finalResult = overrideResult ?? orderGenerationResult;
            if (finalResult == OrderGenerationResult.Succeed)
            {
                // 游戏将 AddToPatientCountdown 留桌等服务
                fsm.To(State.WaitingServe);
            }
            else
            {
                fsm.To(State.Leaving);
            }
            fsm.IsFirstOrder = false;
        }
        else
        {
            fsm.Kill(State.WaitingServe);
        }
    }
    /// <summary>
    /// <see cref="GuestsManager.GenerateOrderSession"/> 中的 GenerateOrderSession 无副作用版用于主机预测结果
    /// </summary>
    /// <param name="oldResult"></param>
    /// <param name="toGenerate"></param>
    /// <returns></returns>
    private static OrderGenerationResult CheckRemainingFund(OrderGenerationResult oldResult, GuestProxy guest)
    {
        var filtered = guest.Orders.Where(x => !x.IsFree).ToArray();
        int spent = filtered.Length > 0
            ? filtered.Select(x => x.Price).Aggregate((a, b) => a + b)
            : 0;
        int totalFund = guest.MaxFundCarry + guest.ExtraFundByBuff;
        if (spent <= totalFund)
        {
            return oldResult;
        }
        float enduranceMultiplier = 1f;
        if (guest.Mood > 50)
        {
            // 原式是 Mathf.Log(51f / (101 - Mathf.Min(mood, 100)), 25f)，换成 System.Math 以免用掉 Unity 类型。
            enduranceMultiplier = 1f + (float)Math.Log(51f / (101 - Math.Min(guest.Mood, 100)), 25d);
        }
        return spent > totalFund * (guest.EnduranceLimit * enduranceMultiplier)
            ? OrderGenerationResult.ExceedEndurance
            : OrderGenerationResult.NoMoney;
    }

    /// <summary>
    /// 客机重放主机广播的完整订单和续单信息
    /// </summary>
    /// <param name="runtimeId">顾客 rid</param>
    /// <param name="orderGenerationResult">订单生成结果</param>
    /// <param name="overrideResult">SpecialGuest 的订单覆盖结果(主机预测)</param>
    /// <param name="orderData">订单数据</param>
    /// <returns></returns>
    public static bool DoGenerateOrderSession(
        int runtimeId,
        OrderGenerationResult orderGenerationResult,
        OrderGenerationResult? overrideResult,
        OrderKind orderKind,
        int foodRequest,
        int beverageRequest,
        int deskCode,
        bool hidden,
        bool free)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState != State.SeatedDelay && fsm.CurrentState != State.ContinueDecision) return false;

        if (fsm.IsFirstOrder) // FirstOrder
        {
            // 原 FirstOrder 里不经过订单开关的显示部分（心情条与回调）由服务照原样执行；
            // 点单会话本身由下面的服务放行。
            Services.Guests.ShowMood(fsm.Handle);
            fsm.IsFirstOrder = false;
        }
        else // MainOrderCycle
        {
            // 原 MainOrderCycle 的 SetPlayerCanRepelGuest 步骤；doContinue 已由主机的 Result 表达，无需同步。
            Services.Guests.SetRepellable(fsm.Handle);
        }

        // 订单对象属于下这一单的这台机器，所以本机按主机滚出来的内容自己造一单。
        var order = Services.Guests.CreateOrder(fsm.Handle, orderKind, foodRequest, beverageRequest, deskCode, hidden, free);
        if (order.IsNone)
        {
            Log.Error($"Guest #{runtimeId} could not build the replayed order");
            return false;
        }

        // 主机口径的订单与结果交给中间件的订单装载机制：服务放行原版 GenerateOrderSession，
        // 桥接在 GenerateOrder 前缀回写订单、在闭包 GenerateOrderInternal/CheckRemainingFund 回写结果。
        var result = (OrderGenerationOutcome)(int)(overrideResult ?? orderGenerationResult);
        Services.Guests.BeginOrderSession(fsm.Handle, result, order, string.Empty);

        if ((overrideResult ?? orderGenerationResult) == OrderGenerationResult.Succeed)
        {
            fsm.To(State.WaitingServe);
        }
        else
        {
            // 失败 result：游戏的 GenerateOrderSession 已经在本地走完 GuestPay+LeaveFromDesk
            // 或起了 OnDelay 协程 → PayAndLeave。客机 FSM 同步推进至 Leaving。
            fsm.To(State.Leaving);
        }
        return true;
    }

    /// <summary>
    /// 校验订单序号：普通顾客取订单栈深，手动顾客取主机序号。
    /// 不一致时记录并由调用方丢弃该 Action，避免应用到错误的订单上。
    /// </summary>
    private static bool OrderSeqMismatch(GuestFSM fsm, int orderSeq, string tag)
    {
        var local = fsm.OrderSeq;
        if (orderSeq == local) return false;
        Log.Error($"Guest #{fsm.RuntimeId} OrderSeq mismatch in {tag}: action=#{orderSeq} local=#{local}, dropping");
        return true;
    }

    /// <summary>
    /// Sellable 对比
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool SellableEquals(Sellable a, Sellable b)
        => SellableFood.ContentEquals(SellableFood.FromSellable(a), SellableFood.FromSellable(b));

    private static void RestoreFood(DishProxy food)
    {
        if (food == null) return;

        if (food.ModifierIds.Count == 0 && food.AdditiveTags.Count == 0)
        {
            Il2CppSystem.Collections.Generic.List<int> toRestore = new Il2CppSystem.Collections.Generic.List<int>(1);
            toRestore.Add(food.Id);
            RunTimeStorage.FoodInRange(toRestore.ToIEnumerable(), false);
            return;
        }

        Services.Storage.Store(food);
    }

    /// <summary>订单槽位上的菜品（框架投影）与消息里的菜品是否为同一份内容。</summary>
    private static bool ContentEquals(DishProxy slot, Sellable dish) =>
        SellableFood.ContentEquals(SellableFood.FromProxy(slot), SellableFood.FromSellable(dish));

    /// <summary>
    /// 主机或客机在执行
    /// <see cref="NightScene.UI.GuestManagementUtility.WorkSceneServePannel.Send"/>/<see cref="NightScene.UI.GuestManagementUtility.WorkSceneServePannel.Cancel"/> 时，
    /// 将 Sellable 从 Tray 送入 WillServe，同步状态以使其他玩家更新 UI
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="sellable"></param>
    /// <param name="type"></param>
    public static void OnServe(GuestHandle handle, Sellable sellable, Sellable.SellableType type)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        // 忽略手动顾客非待上菜状态下的上菜同步，避免进入异常分支销毁仍被剧情引用的实体。
        if (fsm?.IsManualGuest == true && fsm.CurrentState != State.WaitingServe) return;
        FlowLog($"Guest #{fsm.RuntimeId} served {sellable?.Text?.BriefName ?? "null"}, current FSM state: {fsm.CurrentState}");

        if (fsm.CurrentState == State.WaitingServe)
        {
            Sellable basedOn;
            if (type == Sellable.SellableType.Food)
            {
                basedOn = fsm.WillServeFood;
                fsm.WillServeFood = sellable;
            }
            else
            {
                basedOn = fsm.WillServeBeverage;
                fsm.WillServeBeverage = sellable;
            }
            ServeSellableMessage.Send(fsm.RuntimeId, fsm.OrderSeq, sellable, basedOn, type);
            fsm.To(State.WaitingServe);
        }
        else
        {
            fsm.Kill(State.WaitingServe);
        }
    }


    /// <summary>
    /// 主机或客机收到客机或主机的 <see cref="ServeSellableMessage"/> 后，执行状态更新和 UI 刷新。
    /// 主机需要进行冲突检查，裁定后选择广播更新。
    /// 客机需要忠实重放同步，检查冲突后执行回滚。
    /// </summary>
    /// <param name="runtimeId"></param>
    /// <param name="orderSeq"></param>
    /// <param name="requested"></param>
    /// <param name="baseOn"></param>
    /// <param name="type"></param>
    /// <param name="senderUid"></param>
    /// <returns></returns>
    public static bool DoServe(int runtimeId, int orderSeq, Sellable requested, Sellable baseOn, Sellable.SellableType type, int senderUid)
    {
        if (GameSession.IsRoomHost)
        {
            return DoServeHost(runtimeId, orderSeq, requested, baseOn, type, senderUid);
        }
        else
        {
            return DoServeClient(runtimeId, orderSeq, requested, baseOn, type);
        }
    }

    /// <summary>
    /// 主机收到客机的 <see cref="ServeSellableMessage"/> 后，进行冲突检查，裁定后选择广播更新。
    /// </summary>
    /// <param name="runtimeId"></param>
    /// <param name="orderSeq"></param>
    /// <param name="requested"></param>
    /// <param name="baseOn"></param>
    /// <param name="type"></param>
    /// <param name="senderUid"></param>
    /// <returns></returns>
    private static bool DoServeHost(int runtimeId, int orderSeq, Sellable requested, Sellable baseOn, Sellable.SellableType type, int senderUid)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return true;
        if (fsm.CurrentState != State.WaitingServe) return true;
        if (OrderSeqMismatch(fsm, orderSeq, nameof(DoServe))) return true;
        if (fsm.CurrentOrder is not { } order) return true;


        var hostSlot = type == Sellable.SellableType.Food ? fsm.WillServeFood : fsm.WillServeBeverage;
        var hostDeskSlot = type == Sellable.SellableType.Food ? order.Food : order.Beverage;
        var conflict = (hostDeskSlot != null)   // Host 已有确认上菜 -> 一定冲突
            || (requested != null   // 所请求的料理非空 -> 可能存在冲突
            && hostSlot != null     // Host 端已有预期 -> 可能存在冲突，需检测所 requested 的料理与 Host 端预期是否一致；如果 Host 端没有预期，则不论所请求的料理是什么都不冲突
            && !SellableEquals(hostSlot, requested) // Host 端预期与所 requested 的料理不等 -> 分歧；如果两者相等，则无分歧
            && !SellableEquals(hostSlot, baseOn));  // Host 端预期与所 **requested 和 baseOn** 的料理均不等 -> 分歧且冲突；如果两者 baseOn 相等而 requested 不等，则仅仅是客机的抢占替换
        if (conflict)
        {
            // 所请求和预期已有均非空，而且两者均不等于 host 端当前预期，当拒绝该同步包，记录日志并丢弃。
            Log.Error($"Guest #{runtimeId} DoServe conflict: sellable={requested?.Text?.BriefName}, basedOn={baseOn?.Text?.BriefName ?? "null"}, host has {hostSlot?.Text?.BriefName}, dropping");
            return true;
        }

        // accept: 接受客机状态更新，更新本地状态
        if (type == Sellable.SellableType.Food)
        {
            fsm.WillServeFood = requested;
            order.SetFoodInAir(null);
            order.SetFood(null);
        }
        else
        {
            fsm.WillServeBeverage = requested;
            order.SetBeverageInAir(null);
            order.SetBeverage(null);
        }

        TryUpdateServePanel(fsm.DeskCode, DishOf(requested), KindOf(type), canCancel: true);
        UpdateServeDesk(fsm.DeskCode, DishOf(requested), KindOf(type));

        // 传原 senderUid，让原发起客机自己 echo-filter 掉，避免在客机上重复跑一次。
        ServeSellableMessage.Send(fsm.RuntimeId, orderSeq, requested, baseOn, type, senderUid);

        return true;
    }

    /// <summary>
    /// 客机收到主机的 <see cref="ServeSellableMessage"/> 后，客机需要忠实重放同步，检查冲突后执行回滚。
    /// </summary>
    /// <param name="runtimeId"></param>
    /// <param name="orderSeq"></param>
    /// <param name="sellable"></param>
    /// <param name="baseOn"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    private static bool DoServeClient(int runtimeId, int orderSeq, Sellable sellable, Sellable baseOn, Sellable.SellableType type)
    {

        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.IsManualGuest && orderSeq > fsm.OrderSeq) return false;
        if (fsm.IsManualGuest && (orderSeq < fsm.OrderSeq
            || (orderSeq == fsm.OrderSeq && fsm.CurrentState != State.WaitingServe))) return true;
        if (fsm.CurrentState != State.WaitingServe) return false;
        if (OrderSeqMismatch(fsm, orderSeq, nameof(DoServe))) return true;

        // TODO: 冲突检查和回滚执行
        if (type == Sellable.SellableType.Food)
        {
            if (fsm.WillServeFood != null && !SellableEquals(fsm.WillServeFood, baseOn))
            {
                RestoreFood(DishOf(fsm.WillServeFood));
            }
            fsm.WillServeFood = sellable;
        }
        else
        {
            if (fsm.WillServeBeverage != null && !SellableEquals(fsm.WillServeBeverage, baseOn))
            {
                Il2CppSystem.Collections.Generic.List<int> toRestore = new Il2CppSystem.Collections.Generic.List<int>(1);
                toRestore.Add(fsm.WillServeBeverage.Id);
                RunTimeStorage.BeverageInRange(toRestore.ToIEnumerable());
            }
            fsm.WillServeBeverage = sellable;
        }

        TryUpdateServePanel(fsm.DeskCode, DishOf(sellable), KindOf(type), canCancel: true);
        UpdateServeDesk(fsm.DeskCode, DishOf(sellable), KindOf(type));

        return true;
    }


    /// <summary>
    /// 更新桌面 displayer 的 food/bev 贴图。
    /// 额外包含 type 以区分 Send/Cancel。
    /// </summary>
    /// <param name="deskCode"></param>
    /// <param name="sellable"></param>
    /// <param name="type"></param>
    public static void UpdateServeDesk(int deskCode, DishProxy? dish, DishKind kind)
    {
        // 桌面贴图由框架写（Unity 的 Sprite 不出桥接），模组只给桌号、菜品投影与槽位。
        Services.Guests.ShowServedDish(deskCode, dish, kind);
    }

    /// <summary>消息里的菜品类型对应的槽位。</summary>
    private static DishKind KindOf(Sellable.SellableType type) =>
        type == Sellable.SellableType.Food ? DishKind.Food : DishKind.Beverage;

    /// <summary>消息里的菜品转成框架的菜品投影，供订单槽位、面板与桌面使用。</summary>
    internal static DishProxy? DishOf(Sellable sellable) => GuestSync.ScopedServices?.Dishes.DishOf(sellable);

    /// <summary>
    /// 尝试更新上菜面板上的 food/bev 贴图
    /// </summary>
    /// <param name="deskCode"></param>
    /// <param name="sellable"></param>
    /// <param name="type"></param>
    /// <param name="canCancel"></param>
    /// <returns></returns>
    public static bool TryUpdateServePanel(int deskCode, DishProxy? dish, DishKind kind, bool canCancel)
    {
        var panel = WorkSync.ServePanel;
        if (panel?.DeskCode != deskCode) return false;

        if (kind == DishKind.Food)
        {
            panel.PendingFood = dish;
            panel.RefreshPendingVisual();
            // 已确认上菜：原实现只在 UI 上以“不可取消”方式渲染、并不占用待上菜槽位；
            // 视图没有单独的视觉入口，故渲染后立刻清空槽位，保持面板关闭时不重复确认的语义。
            if (!canCancel) panel.PendingFood = null;
        }
        else // DishKind.Beverage
        {
            panel.PendingBeverage = dish;
            panel.RefreshPendingVisual();
            if (!canCancel) panel.PendingBeverage = null;
        }
        return true;
    }

    /// <summary>
    /// 尝试关闭上菜面板，提前重置状态以不重复触发本地上菜过程
    /// </summary>
    /// <param name="deskCode"></param>
    /// <returns></returns>
    public static bool TryCloseServePanel(int deskCode)
    {
        var panel = WorkSync.ServePanel;
        if (panel == null || deskCode < 0 || panel.DeskCode != deskCode) return false;

        panel.ResetPendingVisual();
        WorkSync.SkipNextServePanelClose();
        panel.Close();
        return true;
    }

    /// <summary>
    /// 主机评价，捕获评价结果
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="evalResult"></param>
    /// <returns></returns>
    public static bool OnEvaluateOrder(GuestHandle handle, GuestEvaluation evalResult)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm?.Proxy is not { } guest) return false;
        if (fsm.CurrentState == State.WaitingServe)
        {
            if (guest.HasEvaluated && guest.TryGetPendingOrder(out var order) && order.IsFulfilled)
            {
                fsm.WillServeFood = null;
                fsm.WillServeBeverage = null;
                EvaluateOrderMessage.Send(
                    fsm.RuntimeId,
                    guest.PendingOrderCount,
                    order.Food,
                    order.Beverage,
                    evalResult);
                fsm.To(State.Evaluating);
                return true;
            }
        }
        fsm.Kill(State.Evaluating);
        return false;
    }

    /// <summary>
    /// 客机重放评价，调用并覆写结果
    /// </summary>
    /// <param name="runtimeId"></param>
    /// <param name="orderSeq"></param>
    /// <param name="food"></param>
    /// <param name="beverage"></param>
    /// <param name="evalResult"></param>
    /// <returns></returns>
    public static bool DoEvaluateOrder(int runtimeId, int orderSeq, Sellable food, Sellable beverage, GuestEvaluation evalResult)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState != State.WaitingServe) return false;
        if (OrderSeqMismatch(fsm, orderSeq, nameof(DoEvaluateOrder))) return true;
        if (fsm.CurrentOrder is not { } order) return false;

        var foodDish = DishOf(food);
        var beverageDish = DishOf(beverage);

        fsm.WillServeFood = null;
        fsm.WillServeBeverage = null;
        order.SetFoodInAir(null);
        order.SetBeverageInAir(null);
        order.SetFood(foodDish);
        order.SetBeverage(beverageDish);
        fsm.OverrideEvalResult = evalResult;

        TryCloseServePanel(fsm.DeskCode);
        UpdateServeDesk(fsm.DeskCode, foodDish, DishKind.Food);
        UpdateServeDesk(fsm.DeskCode, beverageDish, DishKind.Beverage);

        fsm.To(State.Evaluating);
        // 评价改走服务（服务内部同样以 isTriggerByPartner:false 调用原版，并放行被关掉的评价门控）。
        Services.Guests.Evaluate(fsm.Handle);
        fsm.OverrideEvalResult = GuestEvaluation.None;
        return true;
    }

    /// <summary>
    /// 主机或客机确定上菜
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="food"></param>
    /// <param name="beverage"></param>
    public static void OnConfirmServe(GuestHandle handle, DishProxy? food, DishProxy? beverage)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm == null) return;
        if (fsm.IsManualGuest && fsm.CurrentState != State.WaitingServe) return;
        if (fsm.IsRepelling) return;
        if (fsm.CurrentState == State.WaitingServe)
        {
            ConfirmServeMessage.Send(fsm.RuntimeId, fsm.OrderSeq, food, beverage);
            fsm.WillServeFood = null;
            fsm.WillServeBeverage = null;
        }
        else
        {
            fsm.Kill(State.WaitingServe);
        }
    }

    /// <summary>
    /// 主机或客机重放确定上菜
    /// 主机需要进行冲突检查，裁定后选择广播更新。
    /// 客机需要忠实重放同步，检查冲突后执行回滚。
    /// </summary>
    /// <param name="runtimeId"></param>
    /// <param name="orderSeq"></param>
    /// <param name="food"></param>
    /// <param name="beverage"></param>
    /// <param name="senderUid"></param>
    /// <returns></returns>
    public static bool DoConfirmServe(int runtimeId, int orderSeq, Sellable food, Sellable beverage, int senderUid)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.IsManualGuest && orderSeq > fsm.OrderSeq) return false;
        if (fsm.IsManualGuest && (orderSeq < fsm.OrderSeq
            || (orderSeq == fsm.OrderSeq && fsm.CurrentState != State.WaitingServe))) return true;
        if (fsm.CurrentState != State.WaitingServe) return false;
        if (OrderSeqMismatch(fsm, orderSeq, nameof(DoConfirmServe))) return true;

        if (GameSession.IsRoomHost)
        {
            // 主机已上该 料理/酒水 => 丢弃
            if ((fsm.CurrentOrder?.Food != null && food != null)
                || (fsm.CurrentOrder?.Beverage != null && beverage != null))
            {
                return true;
            }

            // 主机已上该 料理/酒水 只不过还在投掷中 => 丢弃
            if ((fsm.CurrentOrder?.FoodInAir != null && food != null)
                || (fsm.CurrentOrder?.BeverageInAir != null && beverage != null))
            {
                return true;
            }

            // 无冲突 => 接受客机的上菜确认，更新状态并广播，然后更新本地状态
            ConfirmServeMessage.Send(fsm.RuntimeId, orderSeq, DishOf(food), DishOf(beverage), senderUid);
        }


        if (fsm.CurrentOrder is not { } order)
        {
            fsm.Kill();
            return true;
        }

        var foodDish = DishOf(food);
        var beverageDish = DishOf(beverage);

        if (food != null)
        {
            // 槽位上是框架的菜品投影，回滚判定要与消息里的菜品比内容（含厨师）。
            var local = order.Food ?? order.FoodInAir;
            if (local != null && !ContentEquals(local, food))
            {
                RestoreFood(local);
            }

            order.SetFoodInAir(null);
            order.SetFood(foodDish);
            fsm.WillServeFood = null;
            UpdateServeDesk(fsm.DeskCode, foodDish, DishKind.Food);
            TryUpdateServePanel(fsm.DeskCode, foodDish, DishKind.Food, canCancel: false);
        }
        if (beverage != null)
        {
            var local = order.Beverage ?? order.BeverageInAir;
            if (local != null && !ContentEquals(local, beverage))
            {
                Il2CppSystem.Collections.Generic.List<int> toRestore = new Il2CppSystem.Collections.Generic.List<int>(1);
                toRestore.Add(local.Id);
                RunTimeStorage.BeverageInRange(toRestore.ToIEnumerable());
            }

            order.SetBeverageInAir(null);
            order.SetBeverage(beverageDish);
            fsm.WillServeBeverage = null;
            UpdateServeDesk(fsm.DeskCode, beverageDish, DishKind.Beverage);
            TryUpdateServePanel(fsm.DeskCode, beverageDish, DishKind.Beverage, canCancel: false);
        }

        // 收到并处理 ConfirmServeMessage 后发现订单已满 => 关闭活动面板，主机端执行评价
        // 注意：guest 可能已因 OnPanelClose 等路径离开 WaitingServe，此时不应重复触发评价
        if (order.IsFulfilled)
        {
            TryCloseServePanel(fsm.DeskCode);
            if (GameSession.IsRoomHost && fsm.CurrentState == State.WaitingServe)
            {
                if (fsm.IsManualGuest) YuyukoGuestSync.EvaluateConfirmed();
                else Services.Guests.Evaluate(fsm.Handle);
            }
        }
        return true;
    }

    /// <summary>
    /// 主机或客机顾客 <see cref="GuestsManager.EvaluateOrder"/> 结束，推进 Evaluating -> EatingDelay
    /// </summary>
    /// <param name="controller"></param>
    public static void OnEatingDelay(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm == null) return;
        if (fsm.CurrentState == State.Evaluating)
        {
            fsm.To(State.EatingDelay);
            return;
        }
        fsm.Kill(State.EatingDelay);
    }

    /// <summary>
    /// 主机或客机在 <see cref="GuestsManager.EvaluateOrder"/> 内
    /// 1.5s 协程后的 <see cref="GuestGroupController.PostEvaluation"/> 执行后，
    /// 用于推进 EatingDelay -> ContinueDecision
    /// </summary>
    /// <param name="controller"></param>
    public static void OnPostEvaluation(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm == null) return;
        if (fsm.CurrentState == State.EatingDelay)
        {
            fsm.To(State.ContinueDecision);
            return;
        }
        fsm.Kill(State.ContinueDecision);
    }

    /// <summary>
    /// 主机判定排队中顾客耐心耗尽。(注意，打烊驱赶不在此链。)
    /// 触发点：GuestGroupController.UpdatePatient → OnPatientDepeletedCallback
    /// (PostInitializeGuestGroup 内 OnPatientDepleted)
    /// </summary>
    public static void OnPatientDepletedInQueue(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm.CurrentState == State.Queued)
        {
            FlowLog($"Guest #{fsm.RuntimeId} patient depleted in queue, FSM: Queued -> Leaving");
            PatientDepletedQueueMessage.Send(fsm.RuntimeId);
            fsm.To(State.Leaving);
            return;
        }

        fsm.Kill(State.Leaving);
    }

    /// <summary>
    /// 客机重放排队耐心耗尽
    /// </summary>
    public static bool DoPatientDepletedInQueue(int runtimeId)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState != State.Queued) return false;
        Services.Guests.StopPatientCountdown(fsm.Handle);
        fsm.Proxy?.MoveToSpawn();
        fsm.To(State.Leaving);
        return true;
    }

    /// <summary>
    /// 主机判定桌上顾客耐心耗尽。同步并推进 WaitingServe -> Leaving。
    /// </summary>
    public static void OnPatientDepletedAtDesk(GuestHandle handle)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm.CurrentState == State.WaitingServe)
        {
            FlowLog($"Guest #{fsm.RuntimeId} patient depleted at desk, FSM: WaitingServe -> Leaving");
            PatientDepletedDeskMessage.Send(fsm.RuntimeId);
            fsm.To(State.Leaving);
            TryCloseServePanel(fsm.DeskCode);
            return;
        }

        fsm.Kill(State.Leaving);
    }

    /// <summary>
    /// 客机重放桌上耐心耗尽。同步推进 WaitingServe -> Leaving
    /// </summary>
    public static bool DoPatientDepletedAtDesk(int runtimeId)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState != State.WaitingServe) return false;

        fsm.To(State.Leaving);
        TryCloseServePanel(fsm.DeskCode);
        // 耐心耗尽改走服务：服务内部放行被关掉的离场开关，并在末端重放原版的订单清理与回调。
        Services.Guests.Leave(fsm.Handle, GuestLeaveKind.Patience);
        return true;
    }

    /// <summary>
    /// 主机客人离桌，部分来源将被 LeaveFromDesk SkipPatch。
    /// </summary>
    public static void OnLeaveFromDesk(
        GuestHandle handle,
        GuestLeaveType leaveType,
        bool triggerLeaveBuff,
        bool broadcast = true)
    {
        var fsm = GuestsMap.GetGuestFsm(handle);
        if (fsm == null) return;
        FlowLog($"Guest #{fsm.RuntimeId} OnLeaveFromDesk from {fsm.CurrentState}, leaveType={leaveType}, triggerLeaveBuff={triggerLeaveBuff}, broadcast={broadcast}");
        if (broadcast)
        {
            if (fsm.IsRepelling)
                GuestRepellMessage.Send(fsm.RuntimeId, handle, leaveType, triggerLeaveBuff);
            else
                GuestLeaveMessage.Send(fsm.RuntimeId, (GuestGroupController.LeaveType)(int)leaveType, triggerLeaveBuff);
        }
        fsm.To(State.Left);
    }

    /// <summary>
    /// 客机重放离桌。离桌改走服务，无条件推进状态到终态。
    /// </summary>
    public static bool DoLeaveFromDesk(int runtimeId, GuestGroupController.LeaveType leaveType, bool triggerLeaveBuff)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState == State.Dead || fsm.CurrentState == State.Left) return true;
        FlowLog($"Guest #{runtimeId} DoLeaveFromDesk from {fsm.CurrentState}, leaveType={leaveType}, triggerLeaveBuff={triggerLeaveBuff}");
        // 服务只按 GuestLeaveKind 选择原版离场方法，原版 LeaveFromDesk 内部一律以控制器自身的
        // FinalLeaveType 结算（游戏 GuestsManager.LeaveFromDesk:2637），与主机发来的 leaveType 等价。
        Services.Guests.Leave(fsm.Handle, GuestLeaveKind.Other);
        fsm.To(State.Left);
        return true;
    }

    /// <summary>
    /// 执行状态转移、记录日志并更新同步队列
    /// </summary>
    /// <param name="state"></param>
    private void To(State state)
    {
        FlowLog($"Guest #{RuntimeId} FSM: {CurrentState} -> {state}");
#if DEBUG
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage($"#{RuntimeId}: {CurrentState} -> {state}");
        UI.InGameConsole.ShowPassive($"#{RuntimeId}: {CurrentState} -> {state}");
#endif
        CurrentState = state;
        if (state == State.Dead || state == State.Left) _pending.Clear();
        Drain();
    }

    /// <summary>
    /// 主机或客机出现异常，或客机收到主机发来的清除命令后，执行异常顾客的清理
    /// </summary>
    /// <param name="state"></param>
    public void Kill(State state = State.None)
    {
        if (CurrentState == State.Dead)
        {
            Log.Error($"Guest #{RuntimeId} Kill ignored: already Dead (target was {state})");
            return;
        }

        var stateBefore = CurrentState;
        var rid = RuntimeId;
        Log.Error($"Guest #{rid} crashed when {stateBefore} -> {state}");
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage($"顾客 #{rid} 状态异常 {stateBefore} -> {state}");
        UI.InGameConsole.ShowPassive($"#{RuntimeId}: 状态异常 {stateBefore} -> {state}");
        Log.LogStacktrace();

        if (GameSession.IsRoomHost)
        {
            GuestKillMessage.Send(rid, stateBefore, DeskCode);
        }

        To(State.Dead);
        // 强制清理会调用被关掉的离场/入座接口，且 Kill 也可能由游戏调用栈（作用域外）触发，
        // 因此排队到营业场景循环的服务作用域内执行。
        var handle = Handle;
        GuestSync.EnqueueReplay($"cleanup #{rid}", services => GuestService.ReplayForceCleanupGuest(services, handle));
        GuestsMap.Remove(rid);
    }
}
