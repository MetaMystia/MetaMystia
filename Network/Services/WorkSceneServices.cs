using System.Collections.Generic;
using GameData.Core.Collections;
using MetaMystia.Network.Utilities;
using MetaMystia.Protocol.Data;
using MetaMystia.Protocol.Enums;
using MetaMystia.Protocol.Messages.WorkScene;
using MetaMystia.Protocol.Messages.WorkScene.Guest;
using NightScene.GuestManagementUtility;
using NightScene.EventUtility;

namespace MetaMystia.Network.Services;
using State = GuestFsmState;

[AutoLog]
public static partial class WorkSceneServices
{
    // ─── Guest FSM Services ───────────────────────────────────────────────

    public static void SendGuestSpawn(int runtimeId, GuestSpawnInfoData spawnInfo)
    {
        var msg = new GuestSpawnMessage
        {
            RuntimeId = runtimeId,
            SpawnInfo = spawnInfo
        };
        MpWire.Send(msg);
    }

    public static void SendGuestInvite(List<int> invitedGuestIds)
    {
        if (!MpManager.IsRoomClient) return;
        var msg = new GuestInviteMessage
        {
            InvitedGuestIds = invitedGuestIds ?? []
        };
        MpWire.Send(msg);
    }

    public static void SendGuestLeave(int runtimeId, GuestGroupController.LeaveType leaveType, bool triggerLeaveBuff)
    {
        var msg = new GuestLeaveMessage
        {
            RuntimeId = runtimeId,
            LeaveType = (byte)leaveType,
            TriggerLeaveBuff = triggerLeaveBuff
        };
        MpWire.Send(msg);
    }

    public static void SendGuestKill(int runtimeId, State hostStateBeforeKill, int deskCode)
    {
        var msg = new GuestKillMessage
        {
            RuntimeId = runtimeId,
            HostStateBeforeKill = hostStateBeforeKill,
            DeskCode = deskCode
        };
        MpWire.Send(msg);
    }

    public static void SendMoveToDesk(int runtimeId, int deskCode)
    {
        var msg = new MoveToDeskMessage
        {
            RuntimeId = runtimeId,
            DeskCode = deskCode
        };
        MpWire.Send(msg);
    }

    public static void SendMoveToQueue(int runtimeId)
    {
        var msg = new MoveToQueueMessage
        {
            RuntimeId = runtimeId
        };
        MpWire.Send(msg);
    }

    public static void SendPlayerRepell(int runtimeId)
    {
        var msg = new PlayerRepellMessage
        {
            RuntimeId = runtimeId
        };
        MpWire.Send(msg);
    }

    public static void SendGenerateOrder(
        int runtimeId,
        GuestsManager.OrderGenerationResult result,
        GuestsManager.OrderGenerationResult? overrideResult,
        GuestsManager.OrderBase orderData)
    {
        var msg = new GenerateOrderMessage
        {
            RuntimeId = runtimeId,
            Result = EnumConverter.ToProtocol(result),
            OverrideResult = overrideResult.HasValue ? EnumConverter.ToProtocol(overrideResult.Value) : null,
            OrderType = EnumConverter.ToProtocol(orderData?.Type ?? GuestsManager.OrderBase.OrderType.Normal),
            RequestFood = orderData?.foodRequest ?? 0,
            RequestBev = orderData?.beverageRequest ?? 0,
            DeskCode = orderData?.DeskCode ?? -1,
            NotShowInUI = orderData?.NotShowInUI ?? false,
            FreeOrder = orderData?.FreeOrder ?? false
        };
        MpWire.Send(msg);
    }

    public static void SendServeSellable(
        int runtimeId,
        int orderSeq,
        Sellable requested,
        Sellable basedOn,
        Sellable.SellableType sellableType,
        int senderUid = -1)
    {
        var msg = new ServeSellableMessage
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Requested = requested.ToSellableFoodData(),
            BasedOn = basedOn.ToSellableFoodData(),
            SellableType = EnumConverter.ToProtocol(sellableType),
            SenderUid = senderUid == -1 ? PlayerManager.Local.Uid : senderUid
        };
        MpWire.Send(msg);
    }

    public static void SendEvaluateOrder(
        int runtimeId,
        int orderSeq,
        Sellable food,
        Sellable beverage,
        GuestGroupController.EvaluationResult result)
    {
        var msg = new EvaluateOrderMessage
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Food = food.ToSellableFoodData(),
            Beverage = beverage.ToSellableFoodData(),
            EvalResult = EnumConverter.ToProtocol(result)
        };
        MpWire.Send(msg);
    }

    public static void SendConfirmServe(
        int runtimeId,
        int orderSeq,
        Sellable food,
        Sellable beverage,
        int senderUid = -1)
    {
        var msg = new ConfirmServeMessage
        {
            RuntimeId = runtimeId,
            OrderSeq = orderSeq,
            Food = food.ToSellableFoodData(),
            Beverage = beverage.ToSellableFoodData(),
            SenderUid = senderUid == -1 ? PlayerManager.Local.Uid : senderUid
        };
        MpWire.Send(msg);
    }

    public static void SendSendFromQueue(int runtimeId)
    {
        var msg = new SendFromQueueMessage
        {
            RuntimeId = runtimeId
        };
        MpWire.Send(msg);
    }

    public static void SendPatientDepletedDesk(int runtimeId)
    {
        var msg = new PatientDepletedDeskMessage
        {
            RuntimeId = runtimeId
        };
        MpWire.Send(msg);
    }

    public static void SendPatientDepletedQueue(int runtimeId)
    {
        var msg = new PatientDepletedQueueMessage
        {
            RuntimeId = runtimeId
        };
        MpWire.Send(msg);
    }

    // ─── Cooking Services ────────────────────────────────────────────────

    public static void SendNightCook(int gridIndex, SellableFoodData food, int recipeId)
    {
        var msg = new NightCookMessage
        {
            GridIndex = gridIndex,
            RecipeId = recipeId,
            Food = food
        };
        MpWire.Send(msg);
    }

    public static void SendExtractFromCooker(int gridIndex)
    {
        var msg = new ExtractFromCookerMessage
        {
            GridIndex = gridIndex
        };
        MpWire.Send(msg);
    }

    public static void SendQTE(int gridIndex, float qteScore)
    {
        var msg = new QTEMessage
        {
            GridIndex = gridIndex,
            QTEScore = qteScore
        };
        MpWire.Send(msg);
    }

    public static void SendBuff(QTEBuff buff)
    {
        var msg = new BuffMessage
        {
            Buff = buff
        };
        MpWire.Send(msg);
    }

    // ─── Storage Services ────────────────────────────────────────────────

    public static void SendStoreFood(SellableFoodData food)
    {
        var msg = new StoreFoodMessage
        {
            Food = food
        };
        MpWire.Send(msg);
    }

    public static void SendStoreSellable(int gridIndex, Sellable sellable)
    {
        switch (sellable.type)
        {
            case Sellable.SellableType.Food:
            {
                var msg = new StoreSellableMessage
                {
                    GridIndex = gridIndex,
                    Food = sellable.ToSellableFoodData(),
                    FoodType = StoreSellableMessage.StoreType.Food
                };
                MpWire.Send(msg);
                break;
            }
            case Sellable.SellableType.Beverage:
            {
                int beverageId = sellable.id;
                var msg = new StoreSellableMessage
                {
                    GridIndex = gridIndex,
                    BeverageId = beverageId,
                    FoodType = StoreSellableMessage.StoreType.Beverage
                };
                MpWire.Send(msg);
                break;
            }
            default:
                Log.LogError($"SendStoreSellable called with unsupported sellable type: {sellable.type}");
                return;
        }
    }

    public static void SendExtractFood(SellableFoodData food)
    {
        var msg = new ExtractFoodMessage
        {
            Food = food
        };
        MpWire.Send(msg);
    }

    // ─── Editor Services ─────────────────────────────────────────────────

    public static void SendFundEdit(float value, EventManager.MathOperation mathOp)
    {
        var msg = new FundEditMessage
        {
            Value = value,
            MathOp = EnumConverter.ToProtocol(mathOp)
        };
        MpWire.Send(msg);
    }

    public static void SendTipEdit(int value, EventManager.ServeType serveType, float comboBuff, float moodBuff, float extraBuff)
    {
        var msg = new TipEditMessage
        {
            IntValue = value,
            ServeType = EnumConverter.ToProtocol(serveType),
            ComboBuff = comboBuff,
            MoodBuff = moodBuff,
            ExtraBuff = extraBuff
        };
        MpWire.Send(msg);
    }

    public static void SendExpEdit(float value, EventManager.MathOperation mathOp)
    {
        var msg = new ExpEditMessage
        {
            Value = value,
            MathOp = EnumConverter.ToProtocol(mathOp)
        };
        MpWire.Send(msg);
    }

    public static void SendPassionEdit(float value, EventManager.MathOperation mathOp)
    {
        var msg = new PassionEditMessage
        {
            Value = value,
            MathOp = EnumConverter.ToProtocol(mathOp)
        };
        MpWire.Send(msg);
    }

    // ─── Close Service ──────────────────────────────────────────────────

    public static void SendIzakayaCloseBroadcast()
    {
        MpWire.Send(new IzakayaCloseMessage());
    }
}
