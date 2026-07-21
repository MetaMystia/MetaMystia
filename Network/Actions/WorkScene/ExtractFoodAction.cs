using MemoryPack;

using GameData.RunTime.NightSceneUtility;

using MetaMystia.Patch;

namespace MetaMystia.Network;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个料理被从保温箱中取出，与 StoreFood 对应
/// </summary>
[MemoryPackable]
[AutoLog]
[RoomRelay]
public partial class ExtractFoodAction : Action
{
    public SellableFood Food { get; set; }

    protected override bool OnSendLogOnlyAction => true;
    protected override bool OnReceiveLogOnlyAction => true;

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        IzakayaConfigure.Instance?.RemoveStoredFood(Food.GetFromLocal());
        WorkSceneStoragePannelPatch.instanceRef?.UpdateFoodField();
        WorkSceneStoragePannelPatch.instanceRef?.m_FoodsGroup?.UpdateElements();
    }

    public static void Send(SellableFood food) =>
        new ExtractFoodAction { Food = food }.Enqueue();
}
