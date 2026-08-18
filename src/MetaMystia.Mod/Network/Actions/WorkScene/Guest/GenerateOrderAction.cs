using Il2CppSystem.IO;
using MemoryPack;
using System.Linq;

using GameData.Core.Collections.CharacterUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

[MemoryPackable]
[AutoLog]
public partial class GenerateOrderAction : Action
{

    public int RuntimeId { get; set; }
    public GuestsManager.OrderGenerationResult Result { get; set; }
    public GuestsManager.OrderGenerationResult? OverrideResult { get; set; }
    public GuestsManager.OrderBase.OrderType OrderType { get; set; }
    public int RequestFood { get; set; }
    public int RequestBev { get; set; }
    public int DeskCode { get; set; }
    public bool NotShowInUI { get; set; }
    public bool FreeOrder { get; set; }

    [DiscardOnStory]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var result = Result;
        var overrideResult = OverrideResult;
        var orderType = OrderType;
        var requestFood = RequestFood;
        var requestBev = RequestBev;
        var deskCode = DeskCode;
        var notShowInUI = NotShowInUI;
        var freeOrder = FreeOrder;

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

    public static void Send(int runtimeId, GuestsManager.OrderGenerationResult result, GuestsManager.OrderGenerationResult? overrideResult, GuestsManager.OrderBase orderData) =>
        new GenerateOrderAction
        {
            RuntimeId = runtimeId,
            Result = result,
            OverrideResult = overrideResult,
            OrderType = orderData?.Type ?? GuestsManager.OrderBase.OrderType.Normal,
            RequestFood = orderData?.foodRequest ?? 0,
            RequestBev = orderData?.beverageRequest ?? 0,
            DeskCode = orderData?.DeskCode ?? -1,
            NotShowInUI = orderData?.NotShowInUI ?? false,
            FreeOrder = orderData?.FreeOrder ?? false
        }.Enqueue();
}
