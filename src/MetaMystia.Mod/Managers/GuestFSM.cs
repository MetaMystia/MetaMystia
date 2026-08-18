using Il2CppSystem.Linq;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using GameData.Core.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;
using NightScene.Tiles;

using MetaMystia.Network;
using MetaMystia.Patch;
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
    public GuestGroupController Controller { get; set; }
    public GuestType GuestType { get; private set; }
    public int[] Ids { get; private set; }
    public int Fund { get; private set; }
    public int MaxFundCarry { get; private set; }

    public int DeskCode => Controller.DeskCode;
    public OrderBase CurrentOrder => Controller?.PeekOrders();
    public int OrderSeq => Controller?.AllOrdersCount ?? -1;

    public bool IsFirstOrder { get; private set; } = true; // TODO: OrderSeq == 1 ?
    public Sellable WillServeBeverage { get; set; }
    public Sellable WillServeFood { get; set; }

    public GuestGroupController.EvaluationResult OverrideEvalResult { get; set; } =
        GuestGroupController.EvaluationResult.Null;

    /// <summary>
    /// 客机端在 DoGenerateOrderSession 中暂存主机口径的订单数据，
    /// 让本地 GenerateOrderInternal Prefix 短路时能回写一致的 orderData/result。
    /// 同一帧内本质只有 0 或 1 个待处理订单，故可为可空字段而无需 Stack。
    /// </summary>
    public GeneratedOrderInfo? PendingOrder { get; set; }
    public int RuntimeId => GuestsMap.GetRuntimeId(Controller);

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
            DeadlineMs = MpManager.TimestampNow + ttlMs,
        });
        Drain();
    }

    /// <summary>
    /// 检测单个顾客的阻塞的待处理项并尝试执行。每次执行完一项都重新检查队首，直到遇到未过期但无法执行的项为止。
    /// </summary>
    private void Drain()
    {
        if (_draining) return;
        _draining = true;
        try
        {
            while (_pending.Count > 0)
            {
                var head = _pending.Peek();
                if (MpManager.TimestampNow > head.DeadlineMs)
                {
                    Log.Error($"Guest #{RuntimeId} pending '{head.Tag}' timeout: stalled at {CurrentState}");
                    _pending.Clear();
                    Kill();
                    return;
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
    /// 主机 Hook 到顾客创建事件，获取顾客类型、ids、金钱等基本信息，注册顾客并广播 GuestSpawnAction
    /// </summary>
    /// <param name="controller"></param>
    public static void OnSpawn(GuestGroupController controller, GuestsManagerPatch.PendingNormalSpawnArgs? normalSpawnArgs = null)
    {
        var fsm = new GuestFSM();
        fsm.CurrentState = State.Constructed;
        fsm.Controller = controller;
        fsm.GuestType = controller.ControllType;
        fsm.Fund = controller.GetFund;
        fsm.MaxFundCarry = controller.MaxFundCarry;
        fsm.Ids = controller
            .GetAllGuests()
            .ToArray()
            .Select(g => g.Id)
            .ToArray();

        GuestsMap.StoreGuest(fsm);
        var spawnInfo = new GuestSpawnInfo
        {
            GuestType = fsm.GuestType,
            Ids = fsm.Ids,
            Fund = controller.GetFund,
            MaxFundCarry = fsm.MaxFundCarry,
        };

        if (normalSpawnArgs.HasValue)
        {
            var args = normalSpawnArgs.Value;
            spawnInfo.HasNormalSpawnArgs = true;
            spawnInfo.HasOverrideSpawnPosition = args.HasOverrideSpawnPosition;
            spawnInfo.OverrideSpawnX = args.OverrideSpawnPosition.x;
            spawnInfo.OverrideSpawnY = args.OverrideSpawnPosition.y;
            spawnInfo.OverrideSpawnZ = args.OverrideSpawnPosition.z;
            spawnInfo.LeaveType = args.LeaveType;
            spawnInfo.TargetDeskCode = args.TargetDeskCode;
            spawnInfo.ShouldFade = args.ShouldFade;
        }

        GuestSpawnAction.Send(fsm.RuntimeId, spawnInfo);
    }

    /// <summary>
    /// 客机收到主机发来的顾客生成事件，注册顾客并重放顾客生成逻辑，但阻止生成的后续移动逻辑
    /// </summary>
    /// <param name="runtimeId"></param>
    /// <param name="guestSpawnInfo"></param>
    public static void DoSpawn(int runtimeId, GuestSpawnInfo guestSpawnInfo)
    {
        var fsm = new GuestFSM();
        fsm.CurrentState = State.Constructed;
        fsm.Controller = null;
        fsm.GuestType = guestSpawnInfo.GuestType;
        fsm.Ids = guestSpawnInfo.Ids;
        fsm.Fund = guestSpawnInfo.Fund;
        fsm.MaxFundCarry = guestSpawnInfo.MaxFundCarry;

        GuestsMap.StoreGuest(runtimeId, fsm);

        if (fsm.GuestType == GuestType.Normal)
        {
            GuestService.ReplaySpawnNormalGuestGroupExtern(ref fsm, guestSpawnInfo);
            return;
        }
        if (fsm.GuestType == GuestType.Special)
        {
            GuestService.ReplaySpawnSpecialGuestGroup(ref fsm);
            return;
        }

        Log.Error($"Guest #{fsm.RuntimeId} spawned with {fsm.GuestType}");
    }

    /// <summary>
    /// 主机用于广播顾客组和桌号的入座信息
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="deskCode"></param>
    public static void OnMoveToDesk(GuestGroupController controller, int deskCode)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);

        // 情况一：顾客组刚生成即可入座
        // 主机调用栈: PostInitializeGuestGroup -> TrySendToSeat(firstSpawn: true) -> MoveToDesk
        // FSM: Constructed -> SeatMoving

        // 情况二：顾客组因座满先入队，后被送出队伍入座
        // 主机调用栈: CheckAndSendFromQueue -> TrySendToSeat(firstSpawn: false) -> MoveToDesk
        // FSM: Queued -> SeatMoving
        if (fsm.CurrentState == State.Constructed || fsm.CurrentState == State.Queued)
        {
            MoveToDeskAction.Send(fsm.RuntimeId, deskCode);
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
        if (fsm == null || fsm.Controller == null) return false;
        if (fsm.CurrentState != State.Constructed && fsm.CurrentState != State.Queued) return false;
        if (deskCode < 0) return false;

        var guestCount = fsm.Controller.guestInstances.Length;
        var deskAvailable = GuestsManager.Instance.TrueAvailableDesks.TryGetValue(deskCode, out var capacity) &&
                            capacity >= guestCount;
        if (!deskAvailable)
        {
            // TODO: 能否直接返回 false
            if (!GuestsManager.Instance.AllGuestInDeskCode.Contains(deskCode)) return false;

            var inDesk = GuestsManager.Instance.GetInDeskGuest(deskCode);
            if (inDesk == null || inDesk.Pointer != fsm.Controller.Pointer) return false;
        }

        // 目标桌位可用 => 直接尝试入座
        var firstSpawn = fsm.CurrentState == State.Constructed;
        if (!GuestService.ReplayTrySendToSeat(fsm.Controller, firstSpawn, deskCode, true))
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
    public static void OnMoveToQueue(GuestGroupController controller)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        if (fsm.CurrentState == State.Constructed)
        {
            fsm.To(State.Queued);
            FlowLog($"Guest #{GuestsMap.GetRuntimeId(controller)} moved to queue, FSM: Constructed -> Queued");
            MoveToQueueAction.Send(fsm.RuntimeId);
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
        if (!GuestGroupController.CanQueue(fsm.Controller.guestInstances.Length)) return false; // 因队满而临界阻塞

        // 客机不自驱排队耐心耗尽：等主机的 PatientDepletedQueueAction 同步。
        // 这里给 controller 挂一个 no-op OnPatientDepeletedCallback
        var OnPatientDepleted = (GuestGroupController guest) =>
        {
            // 客机端：所有耐心耗尽决定权归主机，本地不自驱耐心耗尽离开
        };

        var OnMoveFinish = (GuestGroupController groupController) =>
        {
            if (fsm.CurrentState != State.Queued || !groupController.queued) return;
            groupController.OnStopInQueueCallback?.Invoke(groupController);
            GuestsManager.Instance.AddToPatientCountdown(groupController, OnPatientDepleted);
        };

        // 无法入座但能入队，对应 PostInitializeGuestGroup 中 TrySendToSeat 失败后的尝试入队逻辑
        fsm.Controller.MoveToQueue(OnMoveFinish, false);
        GuestsManager.Instance.SpawnGuest(fsm.Controller);
        fsm.To(State.Queued);
        return true;

        // 如果客机始终无法入队(!CanQueue)，对应 PostInitializeGuestGroup 末尾的 MoveToSpawn(); 会因超时而自动 Kill
    }

    /// <summary>
    /// 主机或客机玩家赶客时
    /// </summary>
    /// <param name="deskCode"></param>
    public static void OnPlayerRepell(int deskCode)
    {
        var controller = GuestsManager.Instance.GetInDeskGuest(deskCode);
        if (controller == null) return;

        var fsm = GuestsMap.GetGuestFsm(controller);
        if (fsm == null) return;

        PlayerRepellAction.Send(fsm.RuntimeId);
        fsm.To(State.Left);
    }

    /// <summary>
    /// 主机和客机重放玩家赶客
    /// </summary>
    /// <param name="runtimeId"></param>
    public static bool DoPlayerRepell(int runtimeId)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState == State.Dead || fsm.CurrentState == State.Left) return true;
        var deskCode = fsm.Controller.DeskCode;

        // 正常调用栈为: PlayerRepell -> RepellInternal -> LeaveFromDesk
        // 然而本 Mod 默认会阻止 RepellInternal LeaveFromDesk 等方法的调用，因此需要逐级设置 Skip*Patch 以跳过客机 Prefix 中的跳过逻辑
        GuestsManagerPatch.SkipPlayerRepellPatch.Grant();
        GuestsManager.Instance.PlayerRepell(deskCode);
        fsm.To(State.Left);
        return true;
    }

    /// <summary>
    /// 主机用于确定 SeatMoving => SeatedDelay 的状态更新。
    /// </summary>
    /// <param name="controller"></param>
    public static void OnRefreshCurrentFundAndOrder(GuestGroupController controller)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        FlowLog($"Guest #{fsm.RuntimeId} refreshed fund and order, current FSM state: {fsm.CurrentState}");

        if (fsm.CurrentState == State.SeatMoving)
        {
            fsm.To(State.SeatedDelay);
            return;
        }

        // do NOT kill
    }

    /// <summary>
    /// 客机落座回调，便于及时推进客机 SeatMoving => SeatedDelay 状态更新
    /// </summary>
    /// <param name="controller"></param>
    public static void ClientGuestGroupOnArrive(GuestGroupController controller)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        if (fsm.CurrentState == State.SeatMoving)
        {
            FlowLog($"Guest #{fsm.RuntimeId} arrived at desk {fsm.DeskCode}, FSM: SeatMoving -> SeatedDelay");
            fsm.To(State.SeatedDelay);
            return;
        }

        fsm.Kill(State.SeatedDelay);
    }


    /// <summary>
    /// 主机在 ReplayCheckAndSendFromQueue -> TrySendToSeat(firstSpawn: false) 后捕获可以出队入座的顾客组并广播。
    /// 但注意，主机端是先执行了 TrySendToSeat 然后才获知需要出队的顾客组，因此初始状态为 SeatMoving。也可考虑 Hook TrySendToSeat。
    /// </summary>
    /// <param name="controller"></param>
    public static void OnSendFromQueue(GuestGroupController controller)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        FlowLog($"Guest #{fsm.RuntimeId} sent from queue, current FSM state: {fsm.CurrentState}");

        if (fsm.CurrentState == State.SeatMoving)
        {
            fsm.To(State.SeatMoving);
            SendFromQueueAction.Send(fsm.RuntimeId);
            return;
        }

        fsm.Kill(State.SeatMoving);
    }

    /// <summary>
    /// 客机对 CheckAndSendFromQueue 的部分重放，指定顾客出队入座。
    /// 但注意：客机是先重放了 ReplayTrySendToSeat -> MoveToDesk，
    /// 然后才在此执行 OnLeaveQueueCallback 等一系列<b>后续</b>操作，因此初始状态为 SeatMoving。
    /// </summary>
    public static bool DoSendFromQueue(int runtimeId)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState == State.SeatMoving)
        {
            var controller = fsm.Controller;
            controller.OnLeaveQueueCallback?.Invoke(controller);
            GuestsManager.Instance.RemoveFromPatientCountdown(controller);
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
    public static void OnGenerateOrderInternal(OrderGenerationResult orderGenerationResult, GuestGroupController controller, GuestsManager.OrderBase orderData)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        FlowLog($"Guest #{fsm.RuntimeId} generated order, current FSM state: {fsm.CurrentState}");

        if (fsm.CurrentState == State.SeatedDelay || fsm.CurrentState == State.ContinueDecision)
        {
            OrderGenerationResult? overrideResult = null;
            if (controller.ControllType == GuestType.Special)
            {
                // 游戏中仅对 Special Guest 执行无副作用的 CheckRemainingFund 以做等价预测
                overrideResult = CheckRemainingFund(orderGenerationResult, controller);
            }
            GenerateOrderAction.Send(fsm.RuntimeId, orderGenerationResult, overrideResult, orderData);

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
    private static OrderGenerationResult CheckRemainingFund(OrderGenerationResult oldResult, GuestGroupController toGenerate)
    {
        var filtered = toGenerate.AllOrders.ToArray().Where(x => !x.FreeOrder).ToArray();
        int spent = filtered.Length > 0
            ? filtered.Select(x => x.Price).Aggregate((a, b) => a + b)
            : 0;
        int totalFund = toGenerate.MaxFundCarry + toGenerate.ExtraFundByBuff;
        if (spent <= totalFund)
        {
            return oldResult;
        }
        float enduranceMultiplier = 1f;
        if (toGenerate.Mood > 50)
        {
            enduranceMultiplier = 1f + Mathf.Log(51f / (float)(101 - Mathf.Min(toGenerate.Mood, 100)), 25f);
        }
        return spent > totalFund * (toGenerate.EnduranceLimit * enduranceMultiplier)
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
    public static bool DoGenerateOrderSession(int runtimeId, OrderGenerationResult orderGenerationResult, OrderGenerationResult? overrideResult, OrderBase orderData)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState != State.SeatedDelay && fsm.CurrentState != State.ContinueDecision) return false;

        var controller = fsm.Controller;
        var info = new GeneratedOrderInfo()
        {
            RuntimeId = runtimeId,
            OrderGenerationResult = orderGenerationResult,
            OrderData = orderData,
            OverrideResult = overrideResult
        };

        // 暂存主机口径的订单数据：让本地 GenerateOrderSession 重入 GenerateOrderInternal 时
        // 客机 Prefix 能从 PendingOrder 回写一致的 orderData/result。
        fsm.PendingOrder = info;

        if (fsm.IsFirstOrder) // FirstOrder
        {
            GuestsManager.Instance.FirstOrder(controller); // FirstOrder 中会调用 GenerateOrderSession
            fsm.IsFirstOrder = false;
        }
        else // MainOrderCycle
        {
            GuestsManager.Instance.Register(GuestsManager.Instance.CanPlayerRepellGuest, controller);
            // doContinue 已在 GenerateOrderInternal 内被计算为 Result，因此此处无需同步 doContinue
            GuestsManager.Instance.GenerateOrderSession(controller, true);
        }

        fsm.PendingOrder = null;

        var finalResult = overrideResult ?? orderGenerationResult;
        if (finalResult == OrderGenerationResult.Succeed)
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
    /// 订单序号校验：以 <see cref="GuestGroupController.AllOrdersCount"/> 栈深为序，主客双侧 PushToOrder 锁步同步。
    /// 不一致时记录并消费该 Action，避免应用到错误的栈顶订单上。
    /// </summary>
    private static bool OrderSeqMismatch(GuestFSM fsm, int orderSeq, string tag)
    {
        var local = fsm.Controller.AllOrdersCount;
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

    private static void RestoreFood(Sellable food)
    {
        if (food == null) return;

        if (!food.HasModifier && food.AdditiveTags.Count == 0)
        {
            Il2CppSystem.Collections.Generic.List<int> toRestore = new Il2CppSystem.Collections.Generic.List<int>(1);
            toRestore.Add(food.Id);
            RunTimeStorage.FoodInRange(toRestore.ToIEnumerable(), false);
            return;
        }

        IzakayaConfigure.Instance.StoreFood(food);
    }

    /// <summary>
    /// 主机或客机在执行
    /// <see cref="NightScene.UI.GuestManagementUtility.WorkSceneServePannel.Send"/>/<see cref="NightScene.UI.GuestManagementUtility.WorkSceneServePannel.Cancel"/> 时，
    /// 将 Sellable 从 Tray 送入 WillServe，同步状态以使其他玩家更新 UI
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="sellable"></param>
    /// <param name="type"></param>
    public static void OnServe(GuestGroupController controller, Sellable sellable, Sellable.SellableType type)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
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
            ServeSellableAction.Send(fsm.RuntimeId, controller.AllOrdersCount, sellable, basedOn, type);
            fsm.To(State.WaitingServe);
        }
        else
        {
            fsm.Kill(State.WaitingServe);
        }
    }


    /// <summary>
    /// 主机或客机收到客机或主机的 <see cref="ServeSellableAction"/> 后，执行状态更新和 UI 刷新。
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
        if (MpManager.IsRoomHost)
        {
            return DoServeHost(runtimeId, orderSeq, requested, baseOn, type, senderUid);
        }
        else
        {
            return DoServeClient(runtimeId, orderSeq, requested, baseOn, type);
        }
    }

    /// <summary>
    /// 主机收到客机的 <see cref="ServeSellableAction"/> 后，进行冲突检查，裁定后选择广播更新。
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
        var order = fsm.Controller.AllOrdersData.Peek();


        var hostSlot = type == Sellable.SellableType.Food ? fsm.WillServeFood : fsm.WillServeBeverage;
        var hostDeskSlot = type == Sellable.SellableType.Food ? order.ServFood : order.ServBeverage;
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
            order.ServedFoodInAir = null;
            order.ServFood = null;
        }
        else
        {
            fsm.WillServeBeverage = requested;
            order.ServedBeverageInAir = null;
            order.ServBeverage = null;
        }

        TryUpdateServePanel(fsm.DeskCode, requested, type, canCancel: true);
        UpdateServeDesk(fsm.DeskCode, requested, type);

        // 传原 senderUid，让原发起客机自己 echo-filter 掉，避免在客机上重复跑一次。
        ServeSellableAction.Send(fsm.RuntimeId, orderSeq, requested, baseOn, type, senderUid);

        return true;
    }

    /// <summary>
    /// 客机收到主机的 <see cref="ServeSellableAction"/> 后，客机需要忠实重放同步，检查冲突后执行回滚。
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
        if (fsm.CurrentState != State.WaitingServe) return false;
        if (OrderSeqMismatch(fsm, orderSeq, nameof(DoServe))) return true;
        var controller = fsm.Controller;
        var order = fsm.Controller.AllOrdersData.Peek();

        // TODO: 冲突检查和回滚执行
        if (type == Sellable.SellableType.Food)
        {
            if (fsm.WillServeFood != null && !SellableEquals(fsm.WillServeFood, baseOn))
            {
                RestoreFood(fsm.WillServeFood);
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

        TryUpdateServePanel(fsm.DeskCode, sellable, type, canCancel: true);
        UpdateServeDesk(fsm.DeskCode, sellable, type);

        return true;
    }


    /// <summary>
    /// 更新桌面 displayer 的 food/bev 贴图。
    /// 额外包含 type 以区分 Send/Cancel。
    /// </summary>
    /// <param name="deskCode"></param>
    /// <param name="sellable"></param>
    /// <param name="type"></param>
    public static void UpdateServeDesk(int deskCode, Sellable sellable, Sellable.SellableType type)
    {
        if (!TileManager.Instance.GuestTables.ContainsKey(deskCode)) return;
        var displayer = TileManager.Instance.GuestTables[deskCode].tableDisplayer;
        if (displayer == null) return;

        if (type == Sellable.SellableType.Food)
        {
            displayer.SetFoodVisual(sellable?.Text?.Visual);
        }
        else // type == Sellable.SellableType.Beverage
        {
            displayer.SetBeverageVisual(sellable?.Text?.Visual);
        }
    }

    /// <summary>
    /// 尝试更新上菜面板上的 food/bev 贴图
    /// </summary>
    /// <param name="deskCode"></param>
    /// <param name="sellable"></param>
    /// <param name="type"></param>
    /// <param name="canCancel"></param>
    /// <returns></returns>
    public static bool TryUpdateServePanel(int deskCode, Sellable sellable, Sellable.SellableType type, bool canCancel)
    {
        var panelDeskCode = WorkSceneServePannelPatch.PanelDeskCode;
        if (panelDeskCode != deskCode) return false;

        if (type == Sellable.SellableType.Food)
        {
            WorkSceneServePannelPatch.instanceRef?.willServeFood = canCancel ? sellable : null;
            if (sellable == null)
            {
                WorkSceneServePannelPatch.instanceRef?.ResetServedVisualOnUI(
                    WorkSceneServePannelPatch.instanceRef?.servFood,
                    WorkSceneServePannelPatch.instanceRef?.servFoodOutline);
            }
            else
            {
                WorkSceneServePannelPatch.instanceRef?.SetServedVisualOnUI(
                    WorkSceneServePannelPatch.instanceRef?.servFood,
                    WorkSceneServePannelPatch.instanceRef?.servFoodOutline,
                    sellable,
                    canCancel);
            }
        }
        else // type == Sellable.SellableType.Beverage
        {
            WorkSceneServePannelPatch.instanceRef?.willServeBeverage = canCancel ? sellable : null;
            if (sellable == null)
            {
                WorkSceneServePannelPatch.instanceRef?.ResetServedVisualOnUI(
                    WorkSceneServePannelPatch.instanceRef?.servBev,
                    WorkSceneServePannelPatch.instanceRef?.servBevOutline);
            }
            else
            {
                WorkSceneServePannelPatch.instanceRef?.SetServedVisualOnUI(
                    WorkSceneServePannelPatch.instanceRef?.servBev,
                    WorkSceneServePannelPatch.instanceRef?.servBevOutline,
                    sellable,
                    canCancel);
            }
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
        if (WorkSceneServePannelPatch.PanelDeskCode != deskCode) return false;

        WorkSceneServePannelPatch.instanceRef?.willServeFood = null;
        WorkSceneServePannelPatch.instanceRef?.willServeBeverage = null;
        WorkSceneServePannelPatch.SkipOnPanelClosePatch.Grant();
        WorkSceneServePannelPatch.instanceRef?.CloseExternPanel();
        return true;
    }

    /// <summary>
    /// 主机评价，捕获评价结果
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="evalResult"></param>
    /// <returns></returns>
    public static bool OnEvaluateOrder(GuestGroupController controller, GuestGroupController.EvaluationResult evalResult)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        if (fsm == null) return false;
        if (fsm.CurrentState == State.WaitingServe)
        {
            var order = controller.PeekOrders();
            if (controller.HasEvaluated && order != null && order.IsFullfilled)
            {
                fsm.WillServeFood = null;
                fsm.WillServeBeverage = null;
                EvaluateOrderAction.Send(fsm.RuntimeId, controller.AllOrdersCount, order.ServFood, order.ServBeverage, evalResult);
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
    public static bool DoEvaluateOrder(int runtimeId, int orderSeq, Sellable food, Sellable beverage, GuestGroupController.EvaluationResult evalResult)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState != State.WaitingServe) return false;
        if (OrderSeqMismatch(fsm, orderSeq, nameof(DoEvaluateOrder))) return true;
        var controller = fsm.Controller;
        var order = controller.PeekOrders();

        fsm.WillServeFood = null;
        fsm.WillServeBeverage = null;
        order.ServedFoodInAir = null;
        order.ServedBeverageInAir = null;
        order.ServFood = food;
        order.ServBeverage = beverage;
        fsm.OverrideEvalResult = evalResult;

        TryCloseServePanel(fsm.DeskCode);
        UpdateServeDesk(fsm.DeskCode, food, Sellable.SellableType.Food);
        UpdateServeDesk(fsm.DeskCode, beverage, Sellable.SellableType.Beverage);

        fsm.To(State.Evaluating);
        GuestsManager.Instance.EvaluateOrder(controller, isTriggerByPartner: false);
        fsm.OverrideEvalResult = GuestGroupController.EvaluationResult.Null;
        return true;
    }

    /// <summary>
    /// 主机或客机确定上菜
    /// </summary>
    /// <param name="controller"></param>
    /// <param name="food"></param>
    /// <param name="beverage"></param>
    public static void OnConfirmServe(GuestGroupController controller, Sellable food, Sellable beverage)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        if (fsm == null) return;
        if (fsm.CurrentState == State.WaitingServe)
        {
            ConfirmServeAction.Send(fsm.RuntimeId, controller.AllOrdersCount, food, beverage);
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
        if (fsm.CurrentState != State.WaitingServe) return false;
        if (OrderSeqMismatch(fsm, orderSeq, nameof(DoConfirmServe))) return true;

        if (MpManager.IsRoomHost)
        {
            // 主机已上该 料理/酒水 => 丢弃
            if ((fsm.CurrentOrder?.ServFood != null && food != null)
                || (fsm.CurrentOrder?.ServBeverage != null && beverage != null))
            {
                return true;
            }

            // 主机已上该 料理/酒水 只不过还在投掷中 => 丢弃
            if ((fsm.CurrentOrder?.ServedFoodInAir != null && food != null)
                || (fsm.CurrentOrder?.ServedBeverageInAir != null && beverage != null))
            {
                return true;
            }

            // 无冲突 => 接受客机的上菜确认，更新状态并广播，然后更新本地状态
            ConfirmServeAction.Send(fsm.RuntimeId, orderSeq, food, beverage, senderUid);
        }


        var controller = fsm.Controller;
        var order = fsm.CurrentOrder;
        if (order == null)
        {
            fsm.Kill();
            return true;
        }

        if (food != null)
        {
            var local = order.ServFood ?? order.ServedFoodInAir;
            if (local != null && !SellableEquals(local, food))
            {
                RestoreFood(local);
            }

            order.ServedFoodInAir = null;
            order.ServFood = food;
            fsm.WillServeFood = null;
            UpdateServeDesk(fsm.DeskCode, food, Sellable.SellableType.Food);
            TryUpdateServePanel(fsm.DeskCode, food, Sellable.SellableType.Food, canCancel: false);
        }
        if (beverage != null)
        {
            var local = order.ServBeverage ?? order.ServedBeverageInAir;
            if (local != null && !SellableEquals(local, beverage))
            {
                Il2CppSystem.Collections.Generic.List<int> toRestore = new Il2CppSystem.Collections.Generic.List<int>(1);
                toRestore.Add(local.Id);
                RunTimeStorage.BeverageInRange(toRestore.ToIEnumerable());
            }

            order.ServedBeverageInAir = null;
            order.ServBeverage = beverage;
            fsm.WillServeBeverage = null;
            UpdateServeDesk(fsm.DeskCode, beverage, Sellable.SellableType.Beverage);
            TryUpdateServePanel(fsm.DeskCode, beverage, Sellable.SellableType.Beverage, canCancel: false);
        }

        // 收到并处理 ConfirmServeAction 后发现订单已满 => 关闭活动面板，主机端执行评价
        // 注意：guest 可能已因 OnPanelClose 等路径离开 WaitingServe，此时不应重复触发评价
        if (order.IsFullfilled)
        {
            TryCloseServePanel(fsm.DeskCode);
            if (MpManager.IsRoomHost && fsm.CurrentState == State.WaitingServe)
            {
                GuestsManager.Instance.EvaluateOrder(controller, false, null);
            }
        }
        return true;
    }

    /// <summary>
    /// 主机或客机顾客 <see cref="GuestsManager.EvaluateOrder"/> 结束，推进 Evaluating -> EatingDelay
    /// </summary>
    /// <param name="controller"></param>
    public static void OnEatingDelay(GuestGroupController controller)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
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
    public static void OnPostEvaluation(GuestGroupController controller)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
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
    public static void OnPatientDepletedInQueue(GuestGroupController controller)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        if (fsm.CurrentState == State.Queued)
        {
            FlowLog($"Guest #{fsm.RuntimeId} patient depleted in queue, FSM: Queued -> Leaving");
            PatientDepletedQueueAction.Send(fsm.RuntimeId);
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
        var controller = fsm.Controller;
        GuestsManager.Instance.RemoveFromPatientCountdown(controller);
        controller.MoveToSpawn();
        fsm.To(State.Leaving);
        return true;
    }

    /// <summary>
    /// 主机判定桌上顾客耐心耗尽。同步并推进 WaitingServe -> Leaving。
    /// </summary>
    public static void OnPatientDepletedAtDesk(GuestGroupController controller)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        if (fsm.CurrentState == State.WaitingServe)
        {
            FlowLog($"Guest #{fsm.RuntimeId} patient depleted at desk, FSM: WaitingServe -> Leaving");
            PatientDepletedDeskAction.Send(fsm.RuntimeId);
            fsm.To(State.Leaving);
            return;
        }

        fsm.Kill(State.Leaving);
    }

    /// <summary>
    /// 客机放权 LeaveFromDesk 并重放桌上耐心耗尽。同步推进 WaitingServe -> Leaving
    /// </summary>
    public static bool DoPatientDepletedAtDesk(int runtimeId)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState != State.WaitingServe) return false;
        var controller = fsm.Controller;

        // PatientDepletedLeave 内含有 LeaveFromDesk 需进行放权。
        GuestsManagerPatch.SkipLeaveFromDeskPatch.SetCount(1);
        GuestsManager.Instance.PatientDepletedLeave(controller);
        GuestsManagerPatch.SkipLeaveFromDeskPatch.Reset();

        fsm.To(State.Leaving);
        return true;
    }

    /// <summary>
    /// 主机客人离桌，部分来源将被 LeaveFromDesk SkipPatch。
    /// </summary>
    public static void OnLeaveFromDesk(
        GuestGroupController controller,
        GuestGroupController.LeaveType leaveType,
        bool triggerLeaveBuff,
        bool broadcast = true)
    {
        var fsm = GuestsMap.GetGuestFsm(controller);
        if (fsm == null) return;
        FlowLog($"Guest #{fsm.RuntimeId} OnLeaveFromDesk from {fsm.CurrentState}, leaveType={leaveType}, triggerLeaveBuff={triggerLeaveBuff}, broadcast={broadcast}");
        if (broadcast)
        {
            GuestLeaveAction.Send(fsm.RuntimeId, leaveType, triggerLeaveBuff);
        }
        fsm.To(State.Left);
    }

    /// <summary>
    /// 客机重放离桌。放权 LeaveFromDesk。无条件推进状态到终态。
    /// </summary>
    public static bool DoLeaveFromDesk(int runtimeId, GuestGroupController.LeaveType leaveType, bool triggerLeaveBuff)
    {
        var fsm = GuestsMap.GetGuestFsm(runtimeId);
        if (fsm == null) return false;
        if (fsm.CurrentState == State.Dead || fsm.CurrentState == State.Left) return true;
        FlowLog($"Guest #{runtimeId} DoLeaveFromDesk from {fsm.CurrentState}, leaveType={leaveType}");
        GuestsManagerPatch.SkipLeaveFromDeskPatch.Grant();
        GuestsManager.Instance.LeaveFromDesk(fsm.Controller, leaveType, null, triggerLeaveBuff);
        fsm.To(State.Left);
        return true;
    }

    /// <summary>
    /// 执行状态转移、记录日志并更新同步队列
    /// </summary>
    /// <param name="state"></param>
    private void To(State state)
    {
        FlowLog($"Guest #{GuestsMap.GetRuntimeId(Controller)} FSM: {CurrentState} -> {state}");
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

        if (MpManager.IsRoomHost)
        {
            GuestKillAction.Send(rid, stateBefore, Controller?.DeskCode ?? -1);
        }

        To(State.Dead);
        GuestService.ReplayForceCleanupGuest(Controller);
        GuestsMap.Remove(rid);
    }
}
