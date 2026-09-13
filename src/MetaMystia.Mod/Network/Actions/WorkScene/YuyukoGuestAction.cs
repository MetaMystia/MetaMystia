using System;

using MemoryPack;

using GameData.Core.Collections;
using NightScene.GuestManagementUtility;

namespace MetaMystia.Network;

// 本体保留原版实体和回调；同步主机确认的手动服务、阶段结果与吞厨具目标。
[MemoryPackable]
[AutoLog]
public partial class YuyukoGuestAction : Action
{
    public Guid Session { get; set; }
    public int RuntimeId { get; set; }
    public YuyukoGuestEvent Event { get; set; }
    public int OrderSeq { get; set; }
    public GuestsManager.OrderBase.OrderType OrderType { get; set; }
    public int FoodRequest { get; set; }
    public int BeverageRequest { get; set; }
    public int DeskCode { get; set; }
    public bool NotShowInUI { get; set; }
    public bool FreeOrder { get; set; }
    public int Mood { get; set; }
    public int Fund { get; set; }
    public int MaxFund { get; set; }
    public int PhaseState { get; set; }
    public int PositiveSpellCount { get; set; }
    public int Life { get; set; }
    public int CookerIndex { get; set; }
    public string EvaluationMessage { get; set; }
    public bool ComboProtect { get; set; }
    public float DamageMultiplier { get; set; }
    public SellableFood Food { get; set; }
    public SellableFood Beverage { get; set; }
    public GuestGroupController.EvaluationResult Result { get; set; }

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => YuyukoGuestSync.Receive(this);

    public static void Send(YuyukoGuestAction message) => message.Enqueue();
}
