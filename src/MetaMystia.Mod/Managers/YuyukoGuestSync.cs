using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Il2CppSystem.Linq;

using Mystia.Scenes;

using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;

using static NightScene.GuestManagementUtility.GuestGroupController;
using static NightScene.GuestManagementUtility.GuestsManager;

namespace MetaMystia;

/// <summary>
/// 将剧情创建的幽幽子本体接入主机权威同步，保留两端原版实体和剧情回调。
/// 本文件处理绑定、手动订单和评价；Challenge 部分处理阶段判定与吞厨具。
///
/// 本体按<b>框架句柄</b>识别：剧情实体由游戏创建，句柄由框架的挑战服务给出
/// （<c>IWorkSceneChallengeServices.BossGuest</c>），因此本类不再持有控制器；
/// 需要场景服务的动作（手动订单、评价、清理）排进营业场景循环执行，因为本类的处理协程不在服务作用域内。
/// </summary>
[AutoLog]
public static partial class YuyukoGuestSync
{
    private static GuestHandle body;
    private static GuestFSM fsm;
    private static Guid session;
    private static YuyukoGuestMessage binding;
    private static YuyukoGuestMessage pendingClear;
    private static readonly HashSet<int> boundPeers = new();
    private static readonly Queue<YuyukoGuestMessage> incoming = new();
    private static OrderHandle pendingOrder;
    private static bool hasPendingOrder;
    private static Il2CppSystem.Action<EvaluationResult> pendingCallback;
    private static Il2CppSystem.Action<EvaluationResult> orderCallback;
    private static System.Action<EvaluationResult> wrappedCallback;
    private static EvaluationResult? localCompleted;
    private static EvaluationResult? hostCompleted;
    private static YuyukoGuestMessage replayEvaluation;
    private static bool running;
    private static int lifetime;
    private static int orderVersion;
    private static string evaluationMessage;
    private static bool comboProtect;
    private static float damageMultiplier = 1f;

    /// <summary>本体的投影；尚未捕获或句柄过期时为 null。</summary>
    private static GuestProxy? Body => body.TryGet(out var guest) ? guest : null;

    /// <summary>
    /// 判断句柄是否为当前联机挑战已捕获的本体。之后一切框架面（监听器、服务、消息）都用它判断。
    /// </summary>
    internal static bool IsBody(GuestHandle handle) =>
        GameSession.HasRoomPeers && PrepSceneManager.IsYuyukoChallenge
        && !handle.IsNone && fsm != null && fsm.Handle == handle;

    /// <summary>判断网络编号是否属于已绑定本体，供上菜消息在剧情期间接收并暂存。</summary>
    internal static bool OwnsRuntimeId(int runtimeId) => fsm != null && fsm.RuntimeId == runtimeId;

    /// <summary>当前正同步调用客机评价重放；异步评价完成由完成回调另外跟踪。</summary>
    internal static bool IsReplayingEvaluation => replayEvaluation != null;

    /// <summary>
    /// 捕获剧情创建或查询到的本体，不重放普通顾客出生。
    /// 主机建立会话并分配网络编号；客机等待绑定消息，尚未收到时请求主机补发。
    /// 同一实体的重复捕获不重新初始化。
    /// </summary>
    internal static void Capture(GuestHandle handle)
    {
        if (!GameSession.HasRoomPeers || !PrepSceneManager.IsYuyukoChallenge || handle.IsNone) return;
        if (!body.IsNone && body == handle) return;
        body = handle;
        if (GameSession.IsRoomHost)
        {
            session = Guid.NewGuid();
            fsm = GuestFSM.BindManual(handle);
            SendBinding();
        }
        else if (binding == null) YuyukoGuestBoundMessage.Send(Guid.Empty, 0);
        TryBind();
        Start();
    }

    /// <summary>
    /// 客机接收主机消息，校验会话和本体编号后按用途暂存。
    /// 阶段消息按恢复位置保存，吞食目标进入专用队列，订单与评价按接收顺序等待应用。
    /// 清理消息可越过尚未安装的订单，并移除其之前的待处理消息，避免阶段取消被阻塞。
    /// </summary>
    public static void Receive(YuyukoGuestMessage message)
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
            ReceiveSwallow(message.CookerIndex);
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
        if (!GameSession.IsRoomClient || body.IsNone || binding == null || fsm != null) return;
        if (body.TryGet(out var guest))
        {
            guest.SetFund(binding.Fund);
            guest.SetMaxFundCarry(binding.MaxFund);
        }
        fsm = GuestFSM.BindManual(body, binding.RuntimeId);
        YuyukoGuestBoundMessage.Send(session, fsm.RuntimeId);
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
        var guest = Body;
        message.Fund = guest?.Fund ?? 0;
        message.MaxFund = guest?.MaxFundCarry ?? 0;
        YuyukoGuestMessage.Send(message);
    }

    /// <summary>
    /// 创建带有会话、本体编号和当前订单序号的消息。调用方补充事件数据并负责发送。
    /// </summary>
    private static YuyukoGuestMessage Message(YuyukoGuestEvent kind) => new()
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
    internal static void QueueOrder(OrderHandle order, Il2CppSystem.Action<EvaluationResult> callback)
    {
        pendingOrder = order;
        hasPendingOrder = true;
        pendingCallback = callback;
        Start();
    }

    /// <summary>启动唯一的逐帧处理协程；正在运行时不重复注册。</summary>
    private static void Start()
    {
        if (running) return;
        running = true;
        ModLoop.StartManagedCoroutine(Process(lifetime));
    }

    /// <summary>
    /// 每帧尝试绑定；剧情外处理阶段取消、订单安装和评价完成（吞厨具由挑战监听在场景循环内重放）。
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
        while (generation == lifetime && GameSession.HasRoomPeers && PrepSceneManager.IsYuyukoChallenge)
        {
            TryBind();
            if (fsm != null && !GameFlow.InStory)
            {
                if (pendingClear != null)
                {
                    if (pendingClear.OrderSeq >= fsm.OrderSeq)
                    {
                        CancelOrder();
                        fsm.SetManualOrderSeq(pendingClear.OrderSeq);
                        if (Body is { PendingOrderCount: > 0 } guest)
                            QueueService($"body clean #{fsm.RuntimeId}", services => services.Guests.CleanOrderInfo(guest.Handle));
                    }
                    pendingClear = null;
                }
                if (GameSession.IsRoomHost && hasPendingOrder
                    && PlayerManager.Peers.Keys.All(boundPeers.Contains))
                {
                    var order = pendingOrder;
                    var seq = fsm.OrderSeq + 1;
                    hasPendingOrder = false;
                    Install(order, seq);
                    if (order.TryGet(out var pending))
                    {
                        var message = Message(YuyukoGuestEvent.Order);
                        message.OrderType = (GuestsManager.OrderBase.OrderType)(int)pending.Kind;
                        message.FoodRequest = pending.FoodRequest;
                        message.BeverageRequest = pending.BeverageRequest;
                        message.DeskCode = pending.DeskCode;
                        message.NotShowInUI = pending.Hidden;
                        message.FreeOrder = pending.IsFree;
                        message.Mood = Body?.Mood ?? 0;
                        YuyukoGuestMessage.Send(message);
                    }
                }
                if (GameSession.IsRoomClient && incoming.TryPeek(out var received) && Apply(received))
                    incoming.Dequeue();
                FinishClientOrder();
            }
            yield return null;
        }
        if (generation != lifetime) yield break;
        if (!GameSession.HasRoomPeers && PrepSceneManager.IsYuyukoChallenge)
        {
            // 断线后交回原版；已启动的评价仍持有包装回调，等它实际完成再继续。
            running = false;
            incoming.Clear();
            pendingClear = null;
            if (hasPendingOrder)
            {
                var order = pendingOrder;
                hasPendingOrder = false;
                pendingOrder = default;
                var callback = pendingCallback;
                pendingCallback = null;
                QueueManualInstall(order, callback, fsm?.OrderSeq + 1 ?? 0);
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
    private static bool Apply(YuyukoGuestMessage message)
    {
        switch (message.Event)
        {
            case YuyukoGuestEvent.Order:
                if (message.OrderSeq <= fsm.OrderSeq) return true;
                if (!hasPendingOrder || Body is not { } waiting || waiting.DeskCode != message.DeskCode) return false;
                if (message.OrderSeq != fsm.OrderSeq + 1) return false;
                Body?.SetMood(message.Mood);
                InstallPending((OrderKind)(int)message.OrderType, message.FoodRequest, message.BeverageRequest,
                    message.DeskCode, message.NotShowInUI, message.FreeOrder, message.OrderSeq);
                return true;
            case YuyukoGuestEvent.Evaluate:
                if (message.OrderSeq < fsm.OrderSeq) return true;
                if (message.OrderSeq != fsm.OrderSeq) return false;
                if (Body is not { HasEvaluated: false } || fsm.CurrentState == GuestFSM.State.Manual) return true;
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

    /// <summary>主机侧安装：待处理的是本机自己的那一单（句柄已在手）。</summary>
    private static void Install(OrderHandle order, int seq)
    {
        orderCallback = pendingCallback;
        pendingCallback = null;
        pendingOrder = default;
        hasPendingOrder = false;
        localCompleted = null;
        hostCompleted = null;
        fsm.SetManualOrderSeq(seq);

        if (!order.TryGet(out var pending))
        {
            fsm.Kill();
            return;
        }

        ArmManualOrder(pending.Kind, pending.FoodRequest, pending.BeverageRequest,
            pending.DeskCode, pending.Hidden, pending.IsFree, seq);
    }

    /// <summary>客机侧安装：按主机消息里的订单内容在本机造一单再安装。</summary>
    private static void InstallPending(
        OrderKind kind, int foodRequest, int beverageRequest, int deskCode, bool hidden, bool free, int seq)
    {
        fsm.SetManualOrderSeq(seq);
        ArmManualOrder(kind, foodRequest, beverageRequest, deskCode, hidden, free, seq);
    }

    /// <summary>
    /// 将当前手动订单交给原版安装，并进入等待上菜状态。
    /// 保存本地剧情回调，清除上一单完成标记；包装回调记录生命周期、订单版本和序号，以拒绝旧回调。
    /// 造单与安装都要走场景服务，因此排进营业场景循环执行。
    /// </summary>
    private static void ArmManualOrder(
        OrderKind kind, int foodRequest, int beverageRequest, int deskCode, bool hidden, bool free, int seq)
    {
        var generation = lifetime;
        var version = ++orderVersion;
        wrappedCallback = result => OnCompleted(generation, version, seq, result);
        QueueManualInstall(kind, foodRequest, beverageRequest, deskCode, hidden, free, seq);
        QueueManualEvaluate(install: true);
        fsm.SetManualState(GuestFSM.State.WaitingServe);
        Log.Info($"幽幽子本体订单 #{fsm.RuntimeId}/{seq}: {kind}");
    }

    /// <summary>断线交回原版：待处理的那一单已经在手，直接装回（不再广播）。</summary>
    private static void QueueManualInstall(OrderHandle order, Il2CppSystem.Action<EvaluationResult> callback, int seq)
    {
        _ = seq;
        var handle = body;
        QueueService($"body install #{fsm?.RuntimeId}", services =>
        {
            if (!handle.TryGet(out _)) return;
            var created = order;
            if (created.IsNone) return;
            services.Guests.BeginManualOrder(handle, created, verdict => callback?.Invoke((EvaluationResult)(int)verdict));
        });
    }

    /// <summary>本体订单的安装（造单 + 安装）排进场景服务作用域。</summary>
    private static void QueueManualInstall(
        OrderKind kind, int foodRequest, int beverageRequest, int deskCode, bool hidden, bool free, int seq)
    {
        var handle = body;
        QueueService($"body order #{fsm?.RuntimeId}/{seq}", services =>
        {
            var order = services.Guests.CreateOrder(handle, kind, foodRequest, beverageRequest, deskCode, hidden, free);
            if (order.IsNone) return;
            services.Guests.BeginManualOrder(handle, order, Verdict);
        });
    }

    /// <summary>本体订单的评价排进场景服务作用域。</summary>
    private static void QueueManualEvaluate(bool install)
    {
        _ = install;
        var handle = body;
        QueueService($"body evaluate #{fsm?.RuntimeId}", services => services.Guests.EvaluateManual(handle, Verdict));
    }

    /// <summary>把包装回调交给框架：框架报回来的评价枚举与游戏的一一对应。</summary>
    private static void Verdict(GuestEvaluation evaluation) => wrappedCallback?.Invoke((EvaluationResult)(int)evaluation);

    /// <summary>把需要场景服务的动作排进营业场景循环（本类的协程不在服务作用域内）。</summary>
    private static void QueueService(string tag, Action<IWorkSceneServices> apply) =>
        Listeners.GuestSync.EnqueueReplay(tag, apply);

    /// <summary>
    /// 主机确认菜酒上齐后调用原版手动评价，并传入已包装的完成回调。
    /// 原版仍检查订单是否齐全、是否已评价；该调用返回不等于异步评价完成。
    /// </summary>
    internal static void EvaluateConfirmed()
    {
        if (fsm?.CurrentState == GuestFSM.State.WaitingServe && GameSession.IsRoomHost)
            QueueManualEvaluate(install: false);
    }

    /// <summary>
    /// 客机重放本体评价时，以主机结果替代原始计算，并补上已评价标记。
    /// 手动评价不经过普通 TryOverrideEvaluateByBuff Hook，需在 Evaluate 入口处理。
    /// </summary>
    /// <returns>是否已提供结果；为 true 时调用方跳过原版计算。</returns>
    internal static bool OverrideEvaluation(GuestHandle handle, ref int result)
    {
        if (!IsBody(handle) || replayEvaluation == null) return false;
        QueueService($"body evaluated #{fsm?.RuntimeId}", services => services.Guests.SetEvaluated(handle));
        result = (int)replayEvaluation.Result;
        return true;
    }

    /// <summary>
    /// 客机重放本体的专用改判：回填主机最终评价、台词、连击保护与伤害倍率。
    /// 剧情版的 Null 是有效评价，必须保留；重打版原回调会扣血或触发吞食，因此调用方整体取消原回调。
    /// 伤害倍率由框架写回挑战闭包，模组不再直接读闭包字段。
    /// </summary>
    /// <returns>是否已提供结果；为 false 时放行原回调（主机路径）。</returns>
    internal static bool ReplayBossEvaluation(ref ChallengeBossEvaluation evaluation)
    {
        if (replayEvaluation == null) return false;

        evaluation = evaluation with
        {
            Result = (ChallengeEvaluationResult)(int)replayEvaluation.Result,
            Message = replayEvaluation.EvaluationMessage,
            ComboProtect = replayEvaluation.ComboProtect,
            DamageMultiplier = replayEvaluation.DamageMultiplier,
        };
        return true;
    }

    /// <summary>
    /// 记录原回调结束时的台词、连击保护与伤害倍率，供后续评价消息使用。
    /// 客机取消原回调时框架不派发本通知（回调没跑），模组也无需在客机记录。
    /// </summary>
    internal static void CaptureBossEvaluation(in ChallengeBossEvaluation evaluation)
    {
        evaluationMessage = evaluation.Message;
        comboProtect = evaluation.ComboProtect;
        damageMultiplier = evaluation.DamageMultiplier;
    }

    /// <summary>
    /// 在最终改判已完成、稀客后续评价处理开始前同步数据，并将本体设为评价中。
    /// 主机发送菜酒、最终评价及附带状态；客机重放时对齐心情。真正完成仍由包装回调通知。
    /// </summary>
    internal static void BeforePostEvaluation(GuestHandle handle, GuestEvaluation result)
    {
        if (!IsBody(handle) || fsm == null) return;
        var guest = Body;
        if (replayEvaluation != null && guest is not null) guest.SetMood(replayEvaluation.Mood);
        if (GameSession.IsRoomHost && guest is not null)
        {
            var message = Message(YuyukoGuestEvent.Evaluate);
            if (guest.TryGetPendingOrder(out var pending))
            {
                message.Food = SellableFood.FromProxy(pending.Food);
                message.Beverage = SellableFood.FromProxy(pending.Beverage);
            }
            message.Result = (EvaluationResult)(int)result;
            message.Mood = guest.Mood;
            message.EvaluationMessage = evaluationMessage;
            message.ComboProtect = comboProtect;
            message.DamageMultiplier = damageMultiplier;
            YuyukoGuestMessage.Send(message);
        }
        fsm.SetManualState(GuestFSM.State.Evaluating);
    }

    /// <summary>
    /// 客机写入主机确认的菜酒，关闭上菜面板，再调用原版手动评价以重放表现。
    /// 同步调用期间的重放标记供评价 Hook 回填结果，返回后即清除；异步完成由 wrappedCallback 跟踪。
    /// </summary>
    private static void ReplayEvaluation(YuyukoGuestMessage message)
    {
        if (Body is not { } guest || !guest.TryGetPendingOrder(out var order)) return;

        GuestFSM.TryCloseServePanel(guest.DeskCode);
        replayEvaluation = message;
        QueueService($"body replay #{fsm?.RuntimeId}", services =>
        {
            var food = services.Dishes.DishOf(message.Food?.ToSellable());
            var beverage = services.Dishes.DishOf(message.Beverage?.ToSellable());
            order.SetFood(food);
            order.SetBeverage(beverage);
            order.SetFoodInAir(null);
            order.SetBeverageInAir(null);
        });
        QueueManualEvaluate(install: false);
        QueueService($"body replay clear #{fsm?.RuntimeId}", _ => replayEvaluation = null);
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
        if (!GameSession.HasRoomPeers)
        {
            ContinueOrder(result);
            return;
        }
        if (GameSession.IsRoomHost)
        {
            var message = Message(YuyukoGuestEvent.Complete);
            message.Result = result;
            YuyukoGuestMessage.Send(message);
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
    internal static void OnClean(GuestHandle handle)
    {
        if (!IsBody(handle) || fsm == null) return;
        if (GameSession.IsRoomHost) YuyukoGuestMessage.Send(Message(YuyukoGuestEvent.Clear));
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
        if (Body is { } guest) GuestFSM.TryCloseServePanel(guest.DeskCode);
        pendingOrder = default;
        hasPendingOrder = false;
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
        body = GuestHandle.None;
        session = Guid.Empty;
        binding = null;
        pendingClear = null;
        boundPeers.Clear();
        incoming.Clear();
        replayEvaluation = null;
        running = false;
    }
}
