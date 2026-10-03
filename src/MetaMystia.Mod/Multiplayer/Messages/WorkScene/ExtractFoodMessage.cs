using MemoryPack;

using GameData.RunTime.NightSceneUtility;

using MetaMystia.Listeners;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 任何玩家 → 全体玩家：通告某个料理被从保温箱中取出，与 StoreFood 对应
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class ExtractFoodMessage : MultiplayerMessage
{
    public SellableFood Food { get; set; }

    protected override bool OnSendLogOnlyMessage => true;
    protected override bool OnReceiveLogOnlyMessage => true;

    protected override Common.UI.Scene? ReceiveScene => Common.UI.Scene.WorkScene;

    public override void OnReceivedDerived()
    {
        IzakayaConfigure.Instance?.RemoveStoredFood(Food.GetFromLocal());
        WorkSync.RefreshStoragePanel();
    }

    public static void Send(SellableFood food) =>
        new ExtractFoodMessage { Food = food }.Enqueue();
}
