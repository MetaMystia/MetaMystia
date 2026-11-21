using HarmonyLib;
using System;

using NightScene.GuestManagementUtility;

using static MetaMystia.Patch.HarmonyPrefixFlow;
using static NightScene.GuestManagementUtility.GuestsManager;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Guests;
using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Patch;


// 4.4.0e：GenerateOrderSession 的闭包，..._GuestGroupController_byref_OrderBase_0 为 <GenerateOrderSession>g__GenerateOrderInternal|1，..._SpecialGuestsController_0 为 g__CheckRemainingFund|0。
[HarmonyPatch(typeof(NightScene.GuestManagementUtility.GuestsManager.__c__DisplayClass174_0))]
[AutoLog]
public partial class GuestsManager__c__DisplayClass174_0Patch
{
    /// <summary>
    /// 顾客生成点单的内部方法。主机正常进行，并在 <see cref="GenerateOrderInternal_Postfix"/> 广播；客机回填主机订单并跳过原方法。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.__c__DisplayClass174_0.Method_Internal_OrderGenerationResult_GuestGroupController_byref_OrderBase_0))]
    [HarmonyPrefix]
    public static bool GenerateOrderInternal_Prefix(ref OrderGenerationResult __result, GuestGroupController toGenerate, ref GuestsManager.OrderBase orderData)
    {
        if (GuestRegistry.Mode(toGenerate) != GuestMode.Client) return RunOriginal;
        if (GuestRegistry.Of(toGenerate).PendingOrder is { } pending)
        {
            toGenerate.GetFund = pending.Fund;
            toGenerate.remainOrderCount = pending.RemainOrderCount;
            toGenerate.RefreshCurrentFundAndOrder();
            orderData = pending.OrderData;
            __result = pending.OrderGenerationResult;
        }
        return SkipOriginal;
    }

    /// <summary>主机点单收口。</summary>
    [HarmonyPatch(nameof(GuestsManager.__c__DisplayClass174_0.Method_Internal_OrderGenerationResult_GuestGroupController_byref_OrderBase_0))]
    [HarmonyPostfix]
    public static void GenerateOrderInternal_Postfix(GuestsManager.OrderGenerationResult __result, GuestGroupController toGenerate, ref GuestsManager.OrderBase orderData)
    {
        if (GuestRegistry.Mode(toGenerate) == GuestMode.Host) GenerateOrderMessage.Detect(__result, toGenerate, orderData);
    }


    /// <summary>
    /// 稀客在重放 CheckRemainingFund 时，根据需要覆盖订单结果以跳过后续流程（如入队、点单失败等）。主机正常进行。
    /// </summary>
    [HarmonyPatch(nameof(GuestsManager.__c__DisplayClass174_0.Method_Internal_OrderGenerationResult_OrderGenerationResult_SpecialGuestsController_0))]
    [HarmonyPostfix]
    public static void CheckRemainingFund_Postfix(ref OrderGenerationResult __result, SpecialGuestsController toGenerate)
    {
        if (GuestRegistry.Mode(toGenerate) == GuestMode.Client && GuestRegistry.Of(toGenerate).PendingOrder is { } pending)
            __result = pending.OverrideResult ?? __result;
    }
}
