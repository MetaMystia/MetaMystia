using System.Linq;
using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using Il2CppSystem.Linq;
using MetaMystia.Network.Utilities;
using MetaMystia.Patch;
using MetaMystia.Protocol.Messages.WorkScene;
using MetaMystia.Protocol.Messages.WorkScene.Guest;
using MetaMystia.UI;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using SgrYuki;
using Scene = Common.UI.Scene;

namespace MetaMystia.Network.Handlers;

[AutoLog]
public static partial class WorkSceneHandlers
{
    public static void Register()
    {
        // Guest FSM
        MessageDispatcher.Register<GuestSpawnMessage>(HandleGuestSpawn);
        MessageDispatcher.Register<GuestInviteMessage>(HandleGuestInvite);
        MessageDispatcher.Register<GuestLeaveMessage>(HandleGuestLeave);
        MessageDispatcher.Register<GuestKillMessage>(HandleGuestKill);
        MessageDispatcher.Register<MoveToDeskMessage>(HandleMoveToDesk);
        MessageDispatcher.Register<MoveToQueueMessage>(HandleMoveToQueue);
        MessageDispatcher.Register<PlayerRepellMessage>(HandlePlayerRepell);
        MessageDispatcher.Register<GenerateOrderMessage>(HandleGenerateOrder);
        MessageDispatcher.Register<ServeSellableMessage>(HandleServeSellable);
        MessageDispatcher.Register<EvaluateOrderMessage>(HandleEvaluateOrder);
        MessageDispatcher.Register<ConfirmServeMessage>(HandleConfirmServe);
        MessageDispatcher.Register<SendFromQueueMessage>(HandleSendFromQueue);
        MessageDispatcher.Register<PatientDepletedDeskMessage>(HandlePatientDepletedDesk);
        MessageDispatcher.Register<PatientDepletedQueueMessage>(HandlePatientDepletedQueue);

        // Cooking
        MessageDispatcher.Register<NightCookMessage>(HandleNightCook);
        MessageDispatcher.Register<ExtractFromCookerMessage>(HandleExtractFromCooker);
        MessageDispatcher.Register<QTEMessage>(HandleQTE);
        MessageDispatcher.Register<BuffMessage>(HandleBuff);

        // Storage
        MessageDispatcher.Register<StoreFoodMessage>(HandleStoreFood);
        MessageDispatcher.Register<StoreSellableMessage>(HandleStoreSellable);
        MessageDispatcher.Register<ExtractFoodMessage>(HandleExtractFood);

        // Edit
        MessageDispatcher.Register<FundEditMessage>(HandleFundEdit);
        MessageDispatcher.Register<TipEditMessage>(HandleTipEdit);
        MessageDispatcher.Register<ExpEditMessage>(HandleExpEdit);
        MessageDispatcher.Register<PassionEditMessage>(HandlePassionEdit);

        // Close
        MessageDispatcher.Register<IzakayaCloseMessage>(HandleIzakayaClose);
    }

    // ─── Guest FSM Handlers ──────────────────────────────────────────────

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleGuestSpawn(GuestSpawnMessage msg)
    {
        var runtimeId = msg.RuntimeId;
        var spawnInfoData = msg.SpawnInfo;

        PluginManager.Instance.RunOnMainThread(() => GuestFSM.DoSpawn(runtimeId, spawnInfoData));
    }

    public static void HandleGuestInvite(GuestInviteMessage msg)
    {
        if (!MpManager.IsRoomHost) return;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var tracker = StatusTracker.Instance;
            if (tracker == null) return;

            foreach (var guestId in msg.InvitedGuestIds.Distinct().Where(PlayerManager.SpecialGuestAvailable))
            {
                StatusTrackerPatch.RecordInvitedGuest_ReversePatch(tracker, guestId);
            }
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleGuestLeave(GuestLeaveMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        var rid = msg.RuntimeId;
        var leaveType = (GuestGroupController.LeaveType)msg.LeaveType;
        var triggerLeaveBuff = msg.TriggerLeaveBuff;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoLeaveFromDesk),
                () => GuestFSM.DoLeaveFromDesk(rid, leaveType, triggerLeaveBuff));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleGuestKill(GuestKillMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        var rid = msg.RuntimeId;
        var deskCode = msg.DeskCode;
        var hostState = msg.HostStateBeforeKill;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null)
            {
                GuestService.CleanGuestOrderRegistrationForDesk(deskCode);
                return;
            }

            Log.Error($"Guest #{rid} is being killed by host (host was {hostState}, client was {fsm.CurrentState})");
            fsm.Kill();
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleMoveToDesk(MoveToDeskMessage msg)
    {
        var rid = msg.RuntimeId;
        var deskCode = msg.DeskCode;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoMoveToDesk),
                () => GuestFSM.DoMoveToDesk(rid, deskCode));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleMoveToQueue(MoveToQueueMessage msg)
    {
        var rid = msg.RuntimeId;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoMoveToQueue),
                () => GuestFSM.DoMoveToQueue(rid));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandlePlayerRepell(PlayerRepellMessage msg)
    {
        var rid = msg.RuntimeId;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoPlayerRepell),
                () => GuestFSM.DoPlayerRepell(rid));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleGenerateOrder(GenerateOrderMessage msg)
    {
        var rid = msg.RuntimeId;
        var result = EnumConverter.ToGame(msg.Result);
        var overrideResult = msg.OverrideResult.HasValue ? EnumConverter.ToGame(msg.OverrideResult.Value) : (GuestsManager.OrderGenerationResult?)null;
        var orderType = EnumConverter.ToGame(msg.OrderType);
        var requestFood = msg.RequestFood;
        var requestBev = msg.RequestBev;
        var deskCode = msg.DeskCode;
        var notShowInUI = msg.NotShowInUI;
        var freeOrder = msg.FreeOrder;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoGenerateOrderSession), () =>
            {
                GuestsManager.OrderBase orderData;
                if (orderType == GuestsManager.OrderBase.OrderType.Normal)
                {
                    var guest = fsm.Controller.GetAllGuests().ToArray().First();
                    orderData = new GuestsManager.NormalOrder(guest, requestFood, requestBev, deskCode, notShowInUI, freeOrder);
                }
                else
                {
                    var specialGuest = fsm.Ids[0].RefSGuest();
                    orderData = new GuestsManager.SpecialOrder(specialGuest, requestFood, requestBev, deskCode, notShowInUI, freeOrder);
                }
                return GuestFSM.DoGenerateOrderSession(rid, result, overrideResult, orderData);
            });
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleServeSellable(ServeSellableMessage msg)
    {
        if (msg.SenderUid == PlayerManager.Local.Uid)
        {
            return;
        }

        var rid = msg.RuntimeId;
        var seq = msg.OrderSeq;
        var sellableType = EnumConverter.ToGame(msg.SellableType);

        var requested = msg.Requested.ToGameSellable();
        var basedOn = msg.BasedOn.ToGameSellable();
        var senderUid = msg.SenderUid;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoServe),
                () => GuestFSM.DoServe(rid, seq, requested, basedOn, sellableType, senderUid));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleEvaluateOrder(EvaluateOrderMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        var rid = msg.RuntimeId;
        var seq = msg.OrderSeq;
        var food = msg.Food.ToGameSellable();
        var bev = msg.Beverage.ToGameSellable();
        var result = EnumConverter.ToGame(msg.EvalResult);
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            fsm?.Enqueue(nameof(GuestFSM.DoEvaluateOrder),
                () => GuestFSM.DoEvaluateOrder(rid, seq, food, bev, result));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleConfirmServe(ConfirmServeMessage msg)
    {
        if (msg.SenderUid == PlayerManager.Local.Uid)
        {
            return;
        }

        var rid = msg.RuntimeId;
        var seq = msg.OrderSeq;
        var food = msg.Food.ToGameSellable();
        var bev = msg.Beverage.ToGameSellable();
        var senderUid = msg.SenderUid;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoConfirmServe),
                () => GuestFSM.DoConfirmServe(rid, seq, food, bev, senderUid));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleSendFromQueue(SendFromQueueMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        var rid = msg.RuntimeId;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoSendFromQueue),
                () => GuestFSM.DoSendFromQueue(rid));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandlePatientDepletedDesk(PatientDepletedDeskMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        var rid = msg.RuntimeId;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoPatientDepletedAtDesk),
                () => GuestFSM.DoPatientDepletedAtDesk(rid));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandlePatientDepletedQueue(PatientDepletedQueueMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        var rid = msg.RuntimeId;
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var fsm = GuestsMap.GetGuestFsm(rid);
            if (fsm == null) return;
            fsm.Enqueue(nameof(GuestFSM.DoPatientDepletedInQueue),
                () => GuestFSM.DoPatientDepletedInQueue(rid));
        });
    }

    // ─── Cooking Handlers ───────────────────────────────────────────────

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleNightCook(NightCookMessage msg)
    {
        var foodData = msg.Food;

        var foodSellable = foodData.ToGameSellable();
        Log.LogInfo($"Received COOK: CookerIndex={msg.GridIndex}, FoodId={foodData.Id}, Modifiers=[{string.Join(",", foodData.ModifierIds ?? [])}]");
        PluginManager.Instance.RunOnMainThread(() =>
        {
            if (!PlayerManager.RecipeAvailable(msg.RecipeId))
            {
                Log.Error($"RecipeId {msg.RecipeId} not available!");
                return;
            }
            var recipe = msg.RecipeId.RefRecipe();
            if (recipe == null)
            {
                Log.LogWarning($"Failed to create recipe");
                return;
            }

            var cookerController = CookManager.GetCookerControllerByIndex(msg.GridIndex);
            if (cookerController == null)
            {
                Log.LogWarning($"Failed to find CookerController with GridIndex={msg.GridIndex}");
                return;
            }

            CookControllerPatch.SetCook_ReversePatch(cookerController, foodSellable, recipe, false);
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleExtractFromCooker(ExtractFromCookerMessage msg)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var cookerController = CookManager.GetCookerControllerByIndex(msg.GridIndex);
            if (cookerController == null)
            {
                Log.LogWarning($"Failed to find CookerController with GridIndex={msg.GridIndex}");
                return;
            }
            CookControllerPatch.Extract_ReversePatch(cookerController, null);
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleQTE(QTEMessage msg)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            var cookerController = CookManager.GetCookerControllerByIndex(msg.GridIndex);
            if (cookerController == null)
            {
                Log.LogWarning($"Failed to find CookerController with GridIndex={msg.GridIndex}");
                return;
            }
            CookControllerPatch.StartCookCountDown_ReversePatch(cookerController, msg.QTEScore);
        });
    }

    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleBuff(BuffMessage msg)
    {
        CommandScheduler.Enqueue(
            executeWhen: () => !QTERewardManagerPatch.OnQTESucceededExecuting,
            executeInfo: "BuffAction OnQTESucceededExecuting",
            execute: () =>
            {
                QTERewardManagerPatch.BuffLocalTrigger = false;
                QTERewardManagerPatch.OnQTESucceeded(NightScene.CookingUtility.QTERewardManager.Instance, (int)msg.Buff, true);
                QTERewardManagerPatch.BuffLocalTrigger = true;
                Log.Message($"triggered buff {msg.Buff}");
            },
            timeoutSeconds: 10f
        );
    }

    // ─── Storage Handlers ────────────────────────────────────────────────

    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleStoreFood(StoreFoodMessage msg)
    {
        var foodData = msg.Food;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var food = foodData.ToGameSellable();
            IzakayaConfigurePatch.StoreFood_Original(food);
            WorkSceneStoragePannelPatch.instanceRef?.UpdateFoodField();
            WorkSceneStoragePannelPatch.instanceRef?.m_FoodsGroup?.UpdateElements();
        });
    }

    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleStoreSellable(StoreSellableMessage msg)
    {
        Sellable sellable;
        switch (msg.FoodType)
        {
            case StoreSellableMessage.StoreType.Food:
                var foodData = msg.Food;
                sellable = foodData.ToGameSellable();
                break;
            case StoreSellableMessage.StoreType.Beverage:
                sellable = msg.BeverageId.RefBeverage();
                break;
            default:
                Log.LogError($"HandleStoreSellable called with unsupported FoodType: {msg.FoodType}");
                return;
        }

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var cookerController = CookManager.GetCookerControllerByIndex(msg.GridIndex);
            if (cookerController == null)
            {
                Log.LogWarning($"Failed to find CookerController with GridIndex={msg.GridIndex}");
                return;
            }
            CookControllerPatch.Store_ReversePatch(cookerController, sellable);
        });
    }

    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleExtractFood(ExtractFoodMessage msg)
    {
        var foodData = msg.Food;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var izakaya = IzakayaConfigure.Instance;
            if (izakaya == null) return;
            var matchingFood = izakaya.GetStoredFoods()?.ToArray().FirstOrDefault(f =>
                f.Id == foodData.Id && f.level == foodData.Level);
            if (matchingFood != null)
                izakaya.RemoveStoredFood(matchingFood);
            WorkSceneStoragePannelPatch.instanceRef?.UpdateFoodField();
            WorkSceneStoragePannelPatch.instanceRef?.m_FoodsGroup?.UpdateElements();
        });
    }

    // ─── Editor Handlers ────────────────────────────────────────────────

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleFundEdit(FundEditMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var em = EventManager.Instance;
            if (em == null) return;
            NightSceneEventManagerPatch.FundEdit_ReversePatch(em, msg.Value, EnumConverter.ToGame(msg.MathOp));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleTipEdit(TipEditMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var em = EventManager.Instance;
            if (em == null) return;
            NightSceneEventManagerPatch.TipEdit_ReversePatch(em, msg.IntValue, EnumConverter.ToGame(msg.ServeType), msg.ComboBuff, msg.MoodBuff, msg.ExtraBuff);
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleExpEdit(ExpEditMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var em = EventManager.Instance;
            if (em == null) return;
            NightSceneEventManagerPatch.ExpEdit_ReversePatch(em, msg.Value, EnumConverter.ToGame(msg.MathOp));
        });
    }

    [HandlerAttributes.DiscardOnStory]
    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandlePassionEdit(PassionEditMessage msg)
    {
        if (MpManager.IsRoomHost) return;

        PluginManager.Instance.RunOnMainThread(() =>
        {
            var em = EventManager.Instance;
            if (em == null) return;
            NightSceneEventManagerPatch.PassionEdit_ReversePatch(em, msg.Value, EnumConverter.ToGame(msg.MathOp));
        });
    }

    // ─── Close Handler ──────────────────────────────────────────────────

    [HandlerAttributes.CheckScene(Scene.WorkScene)]
    public static void HandleIzakayaClose(IzakayaCloseMessage msg)
    {
        PluginManager.Instance.RunOnMainThread(() =>
        {
            Log.Message($"Received close command from host");
            InGameConsole.ShowPassive(TextId.PeerClosedIzakaya.Get(PlayerManager.GetPeerName(msg.SenderUid)));
            var eventManager = EventManager.Instance;
            if (eventManager == null)
            {
                Log.Warning("EventManager is null when replaying host close.");
                return;
            }

            NightSceneEventManagerPatch.HostCloseReplay.Grant();
            NightSceneEventManagerPatch.StopInstantiationLoopAndCloseIzakaya_ReversePatch(eventManager);
            NightSceneEventManagerPatch.HostCloseReplay.Reset();
        });
    }
}
