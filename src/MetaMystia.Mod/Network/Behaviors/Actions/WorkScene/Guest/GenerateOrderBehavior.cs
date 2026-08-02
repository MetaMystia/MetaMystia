using System.Linq;

using GameData.Core.Collections.CharacterUtility;
using Il2CppSystem.IO;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class GenerateOrderBehavior
{
    public static void Send(
        int runtimeId,
        GuestsManager.OrderGenerationResult result,
        GuestsManager.OrderGenerationResult? overrideResult,
        GuestsManager.OrderBase orderData) =>
        new GenerateOrderAction
        {
            RuntimeId = runtimeId,
            Result = result.ToWire(),
            OverrideResult = overrideResult?.ToWire(),
            OrderType = orderData?.Type.ToWire() ?? WireOrderType.Normal,
            RequestFood = orderData?.foodRequest ?? 0,
            RequestBev = orderData?.beverageRequest ?? 0,
            DeskCode = orderData?.DeskCode ?? -1,
            NotShowInUI = orderData?.NotShowInUI ?? false,
            FreeOrder = orderData?.FreeOrder ?? false
        }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<GenerateOrderAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(GenerateOrderAction action)
    {
        var rid = action.RuntimeId;
        var result = action.Result.ToGameOrderGenerationResult();
        var overrideResult = action.OverrideResult?.ToGameOrderGenerationResult();
        var orderType = action.OrderType.ToGameOrderType();
        var requestFood = action.RequestFood;
        var requestBev = action.RequestBev;
        var deskCode = action.DeskCode;
        var notShowInUI = action.NotShowInUI;
        var freeOrder = action.FreeOrder;

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
                var specialGuest = DataBaseCharacter.RefSGuest(fsm.Ids[0]);
                orderData = new GuestsManager.SpecialOrder(specialGuest, requestFood, requestBev, deskCode, notShowInUI, freeOrder);
            }
            return GuestFSM.DoGenerateOrderSession(rid, result, overrideResult, orderData);
        });
    }
}
