using static NightScene.GuestManagementUtility.GuestsManager;

namespace MetaMystia.Patch;

public struct GeneratedOrderInfo
{
    /// <summary>
    /// 目标顾客的 Runtime ID
    /// </summary>
    public int RuntimeId;

    /// <summary>
    /// GenerateOrderInternal 订单结果
    /// </summary>
    public OrderGenerationResult OrderGenerationResult;

    /// <summary>
    /// CheckRemainingFund 覆盖结果，仅对 guestGroup.ControllType == GuestsManager.GuestType.Special 有效
    /// </summary>
    public OrderGenerationResult? OverrideResult;

    /// <summary>
    /// 实际点单信息，分 Special 和 Normal
    /// </summary>
    public OrderBase OrderData;
}
