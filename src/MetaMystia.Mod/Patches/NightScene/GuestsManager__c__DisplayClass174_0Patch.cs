using HarmonyLib;
using System;

using NightScene.GuestManagementUtility;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using static NightScene.GuestManagementUtility.GuestsManager;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(NightScene.GuestManagementUtility.GuestsManager.__c__DisplayClass174_0))]
[AutoLog]
public partial class GuestsManager__c__DisplayClass174_0Patch
{
    /// <summary>
    /// 顾客生成点单的内部方法。
    /// 主机正常进行，并将在 <see cref="GenerateOrderInternal_Postfix"/> 捕获订单信息。
    /// 客机重放主机订单，并跳过原方法。
    /// </summary>
    /// <param name="__result"></param>
    /// <param name="toGenerate"></param>
    /// <param name="orderData"></param>
    /// <returns></returns>
    [HarmonyPatch(nameof(GuestsManager.__c__DisplayClass174_0.Method_Internal_OrderGenerationResult_GuestGroupController_byref_OrderBase_0))]
    [HarmonyPrefix]
    public static bool GenerateOrderInternal_Prefix(ref OrderGenerationResult __result, GuestGroupController toGenerate, ref GuestsManager.OrderBase orderData)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            return RunOriginal;
        }
        if (MpManager.IsRoomClient)
        {
            var pending = GuestsMap.GetGuestFsm(toGenerate)?.PendingOrder;
            if (pending.HasValue)
            {
                toGenerate.RefreshCurrentFundAndOrder();
                orderData = pending.Value.OrderData;
                __result = pending.Value.OrderGenerationResult;
            }
            return SkipOriginal;
        }
        
        return RunOriginal;
    }

    /// <summary>
    /// 主机捕获订单并并广播。
    /// </summary>
    /// <param name="__result"></param>
    /// <param name="toGenerate"></param>
    /// <param name="orderData"></param>
    [HarmonyPatch(nameof(GuestsManager.__c__DisplayClass174_0.Method_Internal_OrderGenerationResult_GuestGroupController_byref_OrderBase_0))]
    [HarmonyPostfix]
    public static void GenerateOrderInternal_Postfix(GuestsManager.OrderGenerationResult __result, GuestGroupController toGenerate, ref GuestsManager.OrderBase orderData)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsRoomHost)
        {
            GuestFSM.OnGenerateOrderInternal(__result, toGenerate, orderData);
        }
    }


    /// <summary>
    /// 稀客在重放 CheckRemainingFund 时，根据需要覆盖订单结果以跳过后续流程（如入队、点单失败等）。主机正常进行。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.__c__DisplayClass174_0.Method_Internal_OrderGenerationResult_OrderGenerationResult_SpecialGuestsController_0))]
    [HarmonyPostfix]
    public static void CheckRemainingFund_Postfix(ref OrderGenerationResult __result, SpecialGuestsController toGenerate)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsRoomClient)
        {
            var pending = GuestsMap.GetGuestFsm(toGenerate)?.PendingOrder;
            if (pending.HasValue)
            {
                __result = pending.Value.OverrideResult ?? __result;
            }
        }
    }
}
