using MemoryPack;

using Mystia.Scenes;

using GameData.Core.Collections.NightSceneUtility;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
[AutoLog]
public partial class GenerateOrderMessage : MultiplayerMessage
{

    public int RuntimeId { get; set; }
    public GuestsManager.OrderGenerationResult Result { get; set; }
    public GuestsManager.OrderGenerationResult? OverrideResult { get; set; }
    public OrderKind OrderKind { get; set; }
    public int RequestFood { get; set; }
    public int RequestBev { get; set; }
    public int DeskCode { get; set; }
    public bool NotShowInUI { get; set; }
    public bool FreeOrder { get; set; }

    protected override bool DiscardOnStory => true;
    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        var rid = RuntimeId;
        var result = Result;
        var overrideResult = OverrideResult;
        var orderKind = OrderKind;
        var requestFood = RequestFood;
        var requestBev = RequestBev;
        var deskCode = DeskCode;
        var notShowInUI = NotShowInUI;
        var freeOrder = FreeOrder;

        var fsm = GuestsMap.GetGuestFsm(rid);
        if (fsm == null) return;
        // 订单对象属于下这一单的那台机器，所以本机只把「滚出来的内容」交给重放，
        // 由它在服务作用域内造出本机自己的一单（IWorkSceneGuests.CreateOrder）。
        QueueForGuest(fsm, nameof(GuestFSM.DoGenerateOrderSession),
            () => GuestFSM.DoGenerateOrderSession(
                rid, result, overrideResult, orderKind, requestFood, requestBev, deskCode, notShowInUI, freeOrder));
    }

    public static void Send(
        int runtimeId,
        GuestsManager.OrderGenerationResult result,
        GuestsManager.OrderGenerationResult? overrideResult,
        OrderProxy order) =>
        new GenerateOrderMessage
        {
            RuntimeId = runtimeId,
            Result = result,
            OverrideResult = overrideResult,
            OrderKind = order?.Kind ?? OrderKind.Normal,
            RequestFood = order?.FoodRequest ?? 0,
            RequestBev = order?.BeverageRequest ?? 0,
            DeskCode = order?.DeskCode ?? -1,
            NotShowInUI = order?.Hidden ?? false,
            FreeOrder = order?.IsFree ?? false
        }.Enqueue();
}
