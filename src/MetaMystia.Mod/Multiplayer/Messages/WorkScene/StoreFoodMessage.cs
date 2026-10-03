using MemoryPack;

using MetaMystia.Listeners;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个料理被放入保温箱中，与 ExtractFood 对应
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class StoreFoodMessage : MultiplayerMessage
{
    public SellableFood Food { get; set; }

    protected override bool OnSendLogOnlyMessage => true;
    protected override bool OnReceiveLogOnlyMessage => true;

    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        PrepSync.StoreFood(Food.ToSellable());
        WorkSync.RefreshStoragePanel();
    }

    public static void Send(SellableFood food) =>
        new StoreFoodMessage { Food = food }.Enqueue();
}
