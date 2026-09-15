using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Il2CppSystem.Linq;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
using MetaMystia.Patch;

using static NightScene.GuestManagementUtility.GuestGroupController;
using static NightScene.GuestManagementUtility.GuestsManager;

namespace MetaMystia;

/// <summary>
/// 将剧情创建的幽幽子本体接入主机权威同步，保留两端原版实体和剧情回调。
/// 本文件处理绑定、手动订单和评价；Challenge 部分处理阶段判定与吞厨具。
/// </summary>
[AutoLog]
public static partial class YuyukoGuestSync
{
    private static GuestGroupController body;
    private static GuestFSM fsm;
    private static Guid session;
    private static YuyukoGuestAction binding;
    private static YuyukoGuestAction pendingClear;
    private static readonly HashSet<int> boundPeers = new();
    private static readonly Queue<YuyukoGuestAction> incoming = new();
    private static OrderBase pendingOrder;
    private static Il2CppSystem.Action<EvaluationResult> pendingCallback;
    private static Il2CppSystem.Action<EvaluationResult> orderCallback;
    private static Il2CppSystem.Action<EvaluationResult> wrappedCallback;
    private static EvaluationResult? localCompleted;
    private static EvaluationResult? hostCompleted;
    private static YuyukoGuestAction replayEvaluation;
    private static bool installing;
    private static bool running;
    private static int lifetime;
    private static int orderVersion;
    private static string evaluationMessage;
    private static bool comboProtect;

    /// <summary>
    /// 判断顾客是否为当前联机挑战已捕获的本体。按底层对象地址比较，避免将同角色的其他实体纳入同步。
    /// </summary>
    internal static bool IsBody(GuestGroupController controller) =>
        GameSession.HasPeers && PrepSceneManager.IsYuyukoChallenge && controller != null
        && body?.Pointer == controller.Pointer;

    /// <summary>判断网络编号是否属于已绑定本体，供上菜消息在剧情期间接收并暂存。</summary>
    internal static bool OwnsRuntimeId(int runtimeId) => fsm != null && fsm.RuntimeId == runtimeId;
    /// <summary>原版订单安装调用正在执行；安装 Hook 据此放行，避免再次将订单暂存。</summary>
    internal static bool IsInstalling => installing;
    /// <summary>当前正同步调用客机评价重放；异步评价完成由完成回调另外跟踪。</summary>
    internal static bool IsReplayingEvaluation => replayEvaluation != null;

    /// <summary>
    /// 捕获剧情创建或查询到的本体，不重放普通顾客出生。
    /// 主机建立会话并分配网络编号；客机等待绑定消息，尚未收到时请求主机补发。
    /// 同一底层实体的重复捕获不重新初始化。
    /// </summary>
    internal static void Capture(GuestGroupController controller)
    {
        if (!GameSession.HasPeers || !PrepSceneManager.IsYuyukoChallenge || controller == null) return;
        if (body?.Pointer == controller.Pointer) return;
        body = controller;
        if (GameSession.IsRoomHost)
        {
            session = Guid.NewGuid();
            fsm = GuestFSM.BindManual(body);
            SendBinding();
        }
        else if (binding == null) YuyukoGuestBoundAction.Send(Guid.Empty, 0);
        TryBind();
        Start();
    }

    /// <summary>
    /// 客机接收主机消息，校验会话和本体编号后按用途暂存。
    /// 阶段消息按恢复位置保存，吞食目标进入专用队列，订单与评价按接收顺序等待应用。
    /// 清理消息可越过尚未安装的订单，并移除其之前的待处理消息，避免阶段取消被阻塞。
    /// </summary>
    public static void Receive(YuyukoGuestAction message)
    {
        if (!PrepSceneManager.IsYuyukoChallenge) return;
        if (message.Event == YuyukoGuestEvent.Bind)
        {
            if (message.Session == Guid.Empty || message.RuntimeId <= 0) return;
            if (binding != null && binding.Session != message.Session) return;
            binding = message;
            session = message.Session;
            TryBind();
            Start();
            return;
        }
        if (message.Session != session || binding?.RuntimeId != message.RuntimeId) return;
        if (message.Event == YuyukoGuestEvent.Phase)
        {
            ReceivePhase(message);
            return;
        }
        if (message.Event == YuyukoGuestEvent.Swallow)
        {
            if (!phase3Ended) pendingSwallows.Enqueue(message.CookerIndex);
            return;
        }
        if (message.Event == YuyukoGuestEvent.Clear)
        {
            // 阶段取消不能排在一个尚未出现的本地订单回调后面。
            pendingClear = message;
            while (incoming.TryPeek(out var previous) && previous.OrderSeq <= message.OrderSeq)
                incoming.Dequeue();
            return;
        }
        incoming.Enqueue(message);
    }

    /// <summary>
    /// 客机在本地实体和主机绑定消息均已就绪后，写入资金并绑定网络编号，再发送确认。
    /// 两者到达顺序不限；已有状态机时不重复绑定。
    /// </summary>
    private static void TryBind()
    {
        if (!GameSession.IsRoomClient || body == null || binding == null || fsm != null) return;
        body.GetFund = binding.Fund;
        body.MaxFundCarry = binding.MaxFund;
        fsm = GuestFSM.BindManual(body, binding.RuntimeId);
        YuyukoGuestBoundAction.Send(session, fsm.RuntimeId);
        Log.Info($"幽幽子本体已绑定 #{fsm.RuntimeId}");
    }

    /// <summary>
    /// 主机接收当前同伴的绑定确认。空会话及零编号表示补发绑定请求；有效确认计入订单安装条件。
    /// 此握手处理场景加载先后，不提供营业中重连或中途加入能力。
    /// </summary>
    public static void ReceiveBound(int uid, Guid targetSession, int runtimeId)
    {
        if (!PlayerManager.Peers.ContainsKey(uid)) return;
        if (targetSession == Guid.Empty && runtimeId == 0)
        {
            // 客机可能在首个 Bind 发出后才进入营业场景。
            if (fsm != null) SendBinding();
            return;
        }
        if (targetSession == session && OwnsRuntimeId(runtimeId) && PlayerManager.Peers.ContainsKey(uid))
            boundPeers.Add(uid);
    }

    /// <summary>主机广播已绑定本体的身份和初始资金；调用前本体及状态机必须已建立。</summary>
    private static void SendBinding()
    {
        var message = Message(YuyukoGuestEvent.Bind);
        message.Fund = body.GetFund;
        message.MaxFund = body.MaxFundCarry;
        YuyukoGuestAction.Send(message);
    }

    /// <summary>
    /// 创建带有会话、本体编号和当前订单序号的消息。调用方补充事件数据并负责发送。
    /// </summary>
    private static YuyukoGuestAction Message(YuyukoGuestEvent kind) => new()
    {
        Session = session,
        RuntimeId = fsm.RuntimeId,
        Event = kind,
        OrderSeq = fsm.OrderSeq,
    };

    /// <summary>
    /// 暂存原版准备安装的手动订单及完成回调，原挑战协程仍等待该回调。
    /// 主机等待绑定确认；客机等待同序号的主机订单，再由处理协程安装。
    /// </summary>
    internal static void QueueOrder(OrderBase order, Il2CppSystem.Action<EvaluationResult> callback)
    {
        pendingOrder = order;
        pendingCallback = callback;
        Start();
    }

    /// <summary>启动唯一的逐帧处理协程；正在运行时不重复注册。</summary>
    private static void Start()
    {
        if (running) return;
        running = true;
        PluginHost.Instance.StartManagedCoroutine(Process(lifetime));
    }

    /// <summary>
    /// 每帧尝试绑定；剧情外处理吞食、阶段取消、订单安装和评价完成。
    /// 消息到达时本地剧情可能尚未准备好，故先暂存并在后续帧重试，而不阻塞接收调用。
    /// 主机等待全部当前同伴绑定；客机每帧最多应用一条普通事件，清理优先于订单安装。
    /// </summary>
    /// <param name="generation">启动时的生命周期编号；重置后旧协程直接退出，不能清理新会话。</param>
    /// <remarks>
    /// 断线且仍在挑战场景时，将待安装订单或已完成回调交回原版；其他退出情况重置本体同步。
    /// 该协程与 GuestFSM 的上菜队列独立，TCP 接收有序不代表订单已经安装完成。
    /// </remarks>
    private static IEnumerator Process(int generation)
    {
        while (generation == lifetime && GameSession.HasPeers && PrepSceneManager.IsYuyukoChallenge)
        {
            TryBind();
            if (fsm != null && !GameFlow.InStory)
            {
                PlayPendingSwallows();
                if (pendingClear != null)
                {
                    if (pendingClear.OrderSeq >= fsm.OrderSeq)
                    {
                        CancelOrder();
                        fsm.SetManualOrderSeq(pendingClear.OrderSeq);
                        if (body.AllOrdersCount > 0) GuestsManager.Instance.CleanOrderInfo(body);
                    }
                    pendingClear = null;
                }
                if (GameSession.IsRoomHost && pendingOrder != null
                    && PlayerManager.Peers.Keys.All(boundPeers.Contains))
                {
                    var order = pendingOrder;
                    Install(order, fsm.OrderSeq + 1);
                    var message = Message(YuyukoGuestEvent.Order);
                    message.OrderType = order.Type;
                    message.FoodRequest = order.foodRequest;
                    message.BeverageRequest = order.beverageRequest;
                    message.DeskCode = order.DeskCode;
                    message.NotShowInUI = order.NotShowInUI;
                    message.FreeOrder = order.FreeOrder;
                    message.Mood = body.Mood;
                    YuyukoGuestAction.Send(message);
                }
                if (GameSession.IsRoomClient && incoming.TryPeek(out var received) && Apply(received))
                    incoming.Dequeue();
                FinishClientOrder();
            }
            yield return null;
        }
        if (generation != lifetime) yield break;
        if (!GameSession.HasPeers && PrepSceneManager.IsYuyukoChallenge)
        {
            // 断线后交回原版；已启动的评价仍持有包装回调，等它实际完成再继续。
            running = false;
            incoming.Clear();
            pendingClear = null;
            if (pendingOrder != null)
            {
                var order = pendingOrder;
                var callback = pendingCallback;
                pendingOrder = null;
                pendingCallback = null;
                GuestsManager.Instance.SetManualControllerOrderInternal(body, callback, order);
            }
            else if (localCompleted.HasValue && orderCallback != null)
                ContinueOrder(localCompleted.Value);
        }
        else Reset();
    }

    /// <summary>
    /// 客机应用队首的订单、评价或完成通知。安装订单前需要本地剧情已提交回调，且桌位、序号匹配。
    /// 过期事件直接消费；未来事件等待本地进度追上。阶段取消由处理协程单独优先执行。
    /// </summary>
    /// <returns>true 表示处理或丢弃完毕，可以出队；false 表示保留队首，后续帧重试。</returns>
    private static bool Apply(YuyukoGuestAction message)
    {
        switch (message.Event)
        {
            case YuyukoGuestEvent.Order:
                if (message.OrderSeq <= fsm.OrderSeq) return true;
                if (pendingOrder == null || body.DeskCode != message.DeskCode) return false;
                if (message.OrderSeq != fsm.OrderSeq + 1) return false;
                OrderBase order = message.OrderType == OrderBase.OrderType.Normal
                    ? new GuestsManager.NormalOrder(body.GetAllGuests().ToArray().First(), message.FoodRequest,
                        message.BeverageRequest, message.DeskCode, message.NotShowInUI, message.FreeOrder)
                    : new GuestsManager.SpecialOrder(body.Cast<SpecialGuestsController>().SpecialGuest,
                        message.FoodRequest, message.BeverageRequest, message.DeskCode, message.NotShowInUI, message.FreeOrder);
                body.Mood = message.Mood;
                Install(order, message.OrderSeq);
                return true;
            case YuyukoGuestEvent.Evaluate:
                if (message.OrderSeq < fsm.OrderSeq) return true;
                if (message.OrderSeq != fsm.OrderSeq) return false;
                if (body.HasEvaluated || fsm.CurrentState == GuestFSM.State.Manual) return true;
                if (fsm.CurrentState != GuestFSM.State.WaitingServe) return false;
                ReplayEvaluation(message);
                return true;
            case YuyukoGuestEvent.Complete:
                if (message.OrderSeq < fsm.OrderSeq) return true;
                if (message.OrderSeq != fsm.OrderSeq) return false;
                hostCompleted = message.Result;
                return true;
            default:
                return true;
        }
    }

    /// <summary>
    /// 将当前手动订单交给原版安装，并进入等待上菜状态。
    /// 保存本地剧情回调，清除上一单完成标记；包装回调记录生命周期、订单版本和序号，以拒绝旧回调。
    /// 安装期间临时放行自身 Hook，避免原版安装调用再次进入暂存流程。
    /// </summary>
    private static void Install(OrderBase order, int seq)
    {
        orderCallback = pendingCallback;
        pendingOrder = null;
        pendingCallback = null;
        localCompleted = null;
        hostCompleted = null;
        fsm.SetManualOrderSeq(seq);
        int generation = lifetime;
        int version = ++orderVersion;
        wrappedCallback = (System.Action<EvaluationResult>)(result => OnCompleted(generation, version, seq, result));
        installing = true;
        try
        {
            GuestsManager.Instance.SetManualControllerOrderInternal(body, wrappedCallback, order);
        }
        finally { installing = false; }
        fsm.SetManualState(GuestFSM.State.WaitingServe);
        Log.Info($"幽幽子本体订单 #{fsm.RuntimeId}/{seq}: {order.Type}");
    }

    /// <summary>
    /// 普通实体直接放行；本体必须等待上菜，且由主机发起或处于客机重放期间，才允许手动评价。
    /// </summary>
    internal static bool CanEvaluate(GuestGroupController controller) =>
        !IsBody(controller) || (fsm?.CurrentState == GuestFSM.State.WaitingServe
            && (GameSession.IsRoomHost || IsReplayingEvaluation));

    /// <summary>
    /// 主机确认菜酒上齐后调用原版手动评价，并传入已包装的完成回调。
    /// 原版仍检查订单是否齐全、是否已评价；该调用返回不等于异步评价完成。
    /// </summary>
    internal static void EvaluateConfirmed()
    {
        if (fsm?.CurrentState == GuestFSM.State.WaitingServe && GameSession.IsRoomHost)
            GuestsManager.Instance.EvaulateManualOrder(body, wrappedCallback);
    }

    /// <summary>
    /// 客机重放本体评价时，以主机结果替代原始计算，并补上已评价标记。
    /// 手动评价不经过普通 TryOverrideEvaluateByBuff Hook，需在 Evaluate 入口处理。
    /// </summary>
    /// <returns>是否已提供结果；为 true 时调用方跳过原版计算。</returns>
    internal static bool OverrideEvaluation(GuestGroupController controller, ref int result)
    {
        if (!IsBody(controller) || replayEvaluation == null) return false;
        controller.HasEvaluated = true;
        result = (int)replayEvaluation.Result;
        return true;
    }

    /// <summary>
    /// 客机重放专用改判回调时，回填主机最终评价、台词、连击保护和伤害倍率。
    /// 剧情版的 Null 是有效评价，必须保留；重打版原回调会扣血或触发吞食，不能在客机再次执行。
    /// </summary>
    /// <returns>是否已替代专用回调；其他实体或非重放调用返回 false，放行原版。</returns>
    internal static bool ReplayBossEvaluation(GuestGroupController controller, ref EvaluationResult result,
        ref string message, ref bool protect)
    {
        if (!IsBody(controller) || replayEvaluation == null) return false;
        result = replayEvaluation.Result;
        message = replayEvaluation.EvaluationMessage;
        protect = replayEvaluation.ComboProtect;
        YuyukoBossDataPatch.CurrentContext.dmgMultiplier = replayEvaluation.DamageMultiplier;
        return true;
    }

    /// <summary>
    /// 在本体专用改判回调返回后记录台词和连击保护，供后续评价消息使用。
    /// 客机 Prefix 跳过原版时，Postfix 仍会执行，此时记录的是已回填的主机值。
    /// </summary>
    internal static void CaptureBossEvaluation(GuestGroupController controller, string message, bool protect)
    {
        if (!IsBody(controller)) return;
        evaluationMessage = message;
        comboProtect = protect;
    }

    /// <summary>
    /// 在最终改判已完成、稀客后续评价处理开始前同步数据，并将本体设为评价中。
    /// 主机发送菜酒、最终评价及附带状态；客机重放时对齐心情。真正完成仍由包装回调通知。
    /// </summary>
    internal static void BeforePostEvaluation(GuestGroupController controller, EvaluationResult result)
    {
        if (!IsBody(controller) || fsm == null) return;
        if (replayEvaluation != null) body.Mood = replayEvaluation.Mood;
        if (GameSession.IsRoomHost)
        {
            var message = Message(YuyukoGuestEvent.Evaluate);
            var order = body.PeekOrders();
            message.Food = SellableFood.FromSellable(order.ServFood);
            message.Beverage = SellableFood.FromSellable(order.ServBeverage);
            message.Result = result;
            message.Mood = body.Mood;
            message.EvaluationMessage = evaluationMessage;
            message.ComboProtect = comboProtect;
            message.DamageMultiplier = YuyukoBossDataPatch.CurrentContext.dmgMultiplier;
            YuyukoGuestAction.Send(message);
        }
        fsm.SetManualState(GuestFSM.State.Evaluating);
    }

    /// <summary>
    /// 客机写入主机确认的菜酒，关闭上菜面板，再调用原版手动评价以重放表现。
    /// 同步调用期间的重放标记供评价 Hook 回填结果，返回后即清除；异步完成由 wrappedCallback 跟踪。
    /// </summary>
    private static void ReplayEvaluation(YuyukoGuestAction message)
    {
        var order = body.PeekOrders();
        GuestFSM.TryCloseServePanel(body.DeskCode);
        order.ServFood = message.Food.ToSellable();
        order.ServBeverage = message.Beverage.ToSellable();
        order.ServedFoodInAir = null;
        order.ServedBeverageInAir = null;
        replayEvaluation = message;
        try
        {
            GuestsManager.Instance.EvaulateManualOrder(body, wrappedCallback);
        }
        finally
        {
            replayEvaluation = null;
        }
    }

    /// <summary>
    /// 接收原版异步评价完成回调，丢弃旧生命周期、已取消订单和重复完成通知。
    /// 主机广播完成并继续本地剧情；客机仅记录本地完成，等待主机通知。断线后直接交回原版回调。
    /// </summary>
    private static void OnCompleted(int generation, int version, int seq, EvaluationResult result)
    {
        if (generation != lifetime || version != orderVersion || fsm == null
            || fsm.OrderSeq != seq || localCompleted.HasValue) return;
        localCompleted = result;
        if (!GameSession.HasPeers)
        {
            ContinueOrder(result);
            return;
        }
        if (GameSession.IsRoomHost)
        {
            var message = Message(YuyukoGuestEvent.Complete);
            message.Result = result;
            YuyukoGuestAction.Send(message);
            ContinueOrder(result);
        }
    }

    /// <summary>客机在本地评价完成和主机完成通知都已到达后，以主机结果继续剧情；两者先后顺序不限。</summary>
    private static void FinishClientOrder()
    {
        if (GameSession.IsRoomClient && localCompleted.HasValue && hostCompleted.HasValue && orderCallback != null)
            ContinueOrder(hostCompleted.Value);
    }

    /// <summary>
    /// 恢复手动等待状态并调用本地原剧情回调。先移除回调引用，避免同步重入时重复完成同一单。
    /// 回调可能立即准备下一张订单，该订单仍进入暂存流程。
    /// </summary>
    private static void ContinueOrder(EvaluationResult result)
    {
        var callback = orderCallback;
        orderCallback = null;
        fsm.SetManualState(GuestFSM.State.Manual);
        callback?.Invoke(result);
    }

    /// <summary>
    /// 响应本体订单清理或手动离场：主机广播当前序号的取消，两端废弃本地订单回调。
    /// 此路径表示取消，不调用正常评价完成回调推进剧情。
    /// </summary>
    internal static void OnClean(GuestGroupController controller)
    {
        if (!IsBody(controller) || fsm == null) return;
        if (GameSession.IsRoomHost) YuyukoGuestAction.Send(Message(YuyukoGuestEvent.Clear));
        CancelOrder();
    }

    /// <summary>
    /// 废弃本单回调和待处理上菜，关闭面板并清空评价暂存；通过订单版本使已经发出的旧回调失效。
    /// 本方法不弹出原版订单栈，也不推进手动序号；原版清理与主机取消序号由调用方处理。
    /// </summary>
    private static void CancelOrder()
    {
        orderVersion++;
        fsm?.CancelManualOrder();
        if (body != null) GuestFSM.TryCloseServePanel(body.DeskCode);
        pendingOrder = null;
        pendingCallback = null;
        orderCallback = null;
        wrappedCallback = null;
        localCompleted = null;
        hostCompleted = null;
        evaluationMessage = null;
        comboProtect = false;
    }

    /// <summary>
    /// 清理挑战事件和本体网络映射，释放绑定、订单及消息引用。
    /// 生命周期递增使旧处理协程和异步回调失效；剧情实体本身仍由原版场景管理。
    /// </summary>
    internal static void Reset()
    {
        ResetChallengeEvents();
        lifetime++;
        CancelOrder();
        if (fsm != null) GuestsMap.Remove(fsm.RuntimeId);
        fsm = null;
        body = null;
        session = Guid.Empty;
        binding = null;
        pendingClear = null;
        boundPeers.Clear();
        incoming.Clear();
        installing = false;
        replayEvaluation = null;
        running = false;
    }
}
