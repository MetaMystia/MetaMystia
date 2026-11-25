using System;

using HarmonyLib;
using UnityEngine;

using NightScene.GuestManagementUtility;
using NightScene.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.UI.WorkSceneSustainedPannel))]
public class WorkSceneSustainedPannelPatch
{
    /// <summary>
    /// 幽幽子手动订单的投掷动画可能晚于主机评价和续单完成。
    /// 将桌面更新和评价回调限定到打开面板时的订单，避免旧投掷回写新桌面或评价下一单。
    /// </summary>
    [HarmonyPatch(nameof(WorkSceneSustainedPannel.OpenServePanel))]
    [HarmonyPrefix]
    public static void OpenServePanel_Prefix(GuestsManager.OrderBase order,
        GuestGroupController currentGuestController,
        ref Il2CppSystem.Action onOrderEvaluate,
        ref Il2CppSystem.Action<Sprite> onFoodDelieverStatusUpdated,
        ref Il2CppSystem.Action<Sprite> onBevDelieverStatusUpdated)
    {
        if (!YuyukoGuestSync.IsBody(currentGuestController)) return;
        var guest = GuestRegistry.Of(currentGuestController);
        if (guest == null) return;
        int seq = guest.OrderSeq;
        var evaluate = onOrderEvaluate;
        var updateFood = onFoodDelieverStatusUpdated;
        var updateBeverage = onBevDelieverStatusUpdated;

        bool IsCurrentOrder() => currentGuestController.AllOrdersCount > 0
            && guest.CurrentOrder?.Pointer == order.Pointer
            && (!GameSession.HasRoomPeers || (YuyukoGuestSync.IsBody(currentGuestController)
                && guest.OrderSeq == seq && guest.State == GuestPhase.WaitingServe));

        onOrderEvaluate = (Action)(() =>
        {
            if (IsCurrentOrder()) evaluate?.Invoke();
        });
        onFoodDelieverStatusUpdated = (Action<Sprite>)(sprite =>
        {
            if (IsCurrentOrder()) updateFood?.Invoke(sprite);
        });
        onBevDelieverStatusUpdated = (Action<Sprite>)(sprite =>
        {
            if (IsCurrentOrder()) updateBeverage?.Invoke(sprite);
        });
    }

    /// <summary>
    /// 客机：阻止快进（跳过夜晚），仅主机可操作
    /// </summary>
    [HarmonyPatch(nameof(WorkSceneSustainedPannel.OnFastForwardSubmit))]
    [HarmonyPrefix]
    public static bool OnFastForwardSubmit_Prefix()
    {
        if (GameSession.IsRoomClient)
        {
            Log.Message("Client attempted to fast forward, blocked");
            return SkipOriginal;
        }
        return RunOriginal;
    }
}
