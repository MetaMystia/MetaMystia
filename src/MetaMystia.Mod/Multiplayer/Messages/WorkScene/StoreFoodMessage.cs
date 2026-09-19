using MemoryPack;

using MetaMystia.Patch;

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

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        IzakayaConfigurePatch.StoreFood_Original(Food.ToSellable());
        WorkSceneStoragePannelPatch.instanceRef?.UpdateFoodField();
        WorkSceneStoragePannelPatch.instanceRef?.m_FoodsGroup?.UpdateElements();
    }

    public static void Send(SellableFood food) =>
        new StoreFoodMessage { Food = food }.Enqueue();
}
