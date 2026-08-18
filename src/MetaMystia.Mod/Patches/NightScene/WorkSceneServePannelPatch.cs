using HarmonyLib;
using Il2CppSystem.Linq;
using System;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;
using NightScene.UI.GuestManagementUtility;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.UI.GuestManagementUtility.WorkSceneServePannel))]
[TracePatch(nameof(WorkSceneServePannel.OnPanelInitialize))]
[TracePatch(nameof(WorkSceneServePannel.OnPanelOpen))]
[TracePatch(nameof(WorkSceneServePannel.OnPanelDestroyed))]
[TracePatch(nameof(WorkSceneServePannel.OnPanelClose))]
[TracePatch(nameof(WorkSceneServePannel.Send))]
[TracePatch(nameof(WorkSceneServePannel.Cancel))]
[AutoLog]
public partial class WorkSceneServePannelPatch
{
    public static WorkSceneServePannel instanceRef;
    public static readonly PatchSkipPermit SkipOnPanelClosePatch = new();
    public static int PanelDeskCode => instanceRef?.currentGuestController?.DeskCode
    ?? instanceRef?.operatingOrder?.DeskCode
    ?? -1;

    [HarmonyPatch(nameof(WorkSceneServePannel.OnPanelOpen))]
    [HarmonyPostfix]
    public static void OnPanelOpen_Postfix(WorkSceneServePannel __instance)
    {
        instanceRef = __instance;
        
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        
        var panelDeskCode = WorkSceneServePannelPatch.instanceRef?.currentGuestController?.DeskCode
                            ?? WorkSceneServePannelPatch.instanceRef?.operatingOrder?.DeskCode
                            ?? -1;
        if (panelDeskCode != -1 && GuestsManager.Instance.AllGuestInDeskCode.Contains(panelDeskCode))
        {
            var controller = GuestsManager.Instance.GetInDeskGuest(panelDeskCode);
            var fsm = GuestsMap.GetGuestFsm(controller);

            __instance.willServeFood = fsm.WillServeFood;
            __instance.willServeBeverage = fsm.WillServeBeverage;

            if (__instance.willServeFood != null)
            {
                instanceRef?.SetServedVisualOnUI(
                    instanceRef?.servFood,
                    instanceRef?.servFoodOutline,
                    __instance.willServeFood,
                    true);
            }
            if (__instance.willServeBeverage != null)
            {
                instanceRef?.SetServedVisualOnUI(
                    instanceRef?.servBev,
                    instanceRef?.servBevOutline,
                    __instance.willServeBeverage,
                    true);
            }
        }
    }

    [HarmonyPatch(nameof(WorkSceneServePannel.OnPanelClose))]
    [HarmonyPrefix]
    public static bool OnPanelClose_Prefix(WorkSceneServePannel __instance)
    {
        // isPanelOpen = false;
        instanceRef = null;
        if (SkipOnPanelClosePatch.TryConsume())
        {
            return RunOriginal;
        }

        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsRoomHost)
        {
            GuestFSM.OnConfirmServe(__instance.currentGuestController, __instance.willServeFood, __instance.willServeBeverage);
            return RunOriginal;
        }
        if (MpManager.IsRoomClient)
        {
            GuestFSM.OnConfirmServe(__instance.currentGuestController, __instance.willServeFood, __instance.willServeBeverage);
            return RunOriginal;
        }
        throw new InvalidOperationException("Unexpected network state in OnPanelClose_Prefix");
    }

    [HarmonyPatch(nameof(WorkSceneServePannel.Send))]
    [HarmonyPrefix]
    public static bool Send_Prefix(ref WorkSceneServePannel __instance, Sellable toSend)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;

        if ((toSend.Type == Sellable.SellableType.Food && __instance.operatingOrder.ServFood != null) ||
            (toSend.Type == Sellable.SellableType.Beverage && __instance.operatingOrder.ServBeverage != null))
        {
            // 已有 料理/酒水，跳过此 Patch 和 WorkSceneServePannel.Send
            Log.Info($"Already have {(toSend.Type == Sellable.SellableType.Food ? "food" : "beverage")} in order, skipping Patch & Send");
            return SkipOriginal;
        }

        if (MpManager.IsRoomHost)
        {
            Log.Warning($"Send {toSend?.Text?.BriefName}");
            GuestFSM.OnServe(__instance.currentGuestController, toSend, toSend.Type);
            return RunOriginal;
        }
        if (MpManager.IsRoomClient)
        {
            Log.Warning($"Send {toSend?.Text?.BriefName}");
            GuestFSM.OnServe(__instance.currentGuestController, toSend, toSend.Type);
            return RunOriginal;
        }
        throw new InvalidOperationException("Unexpected network state in Send_Postfix");
    }

    [HarmonyPatch(nameof(WorkSceneServePannel.Cancel))]
    [HarmonyPrefix]
    public static bool Cancel_Prefix(ref WorkSceneServePannel __instance, Sellable toCancel)
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;

        if ((toCancel.Type == Sellable.SellableType.Food && __instance.willServeFood == null) ||
            (toCancel.Type == Sellable.SellableType.Beverage && __instance.willServeBeverage == null) ||
            (IzakayaTray.Instance?.IsTrayFull ?? true))
        {
            // 没有待上菜 料理/酒水，跳过此 Patch 和 WorkSceneServePannel.Cancel
            Log.Info($"No {(toCancel.Type == Sellable.SellableType.Food ? "food" : "beverage")} to cancel in order, skipping Patch & Cancel");
            return SkipOriginal;
        }

        if (MpManager.IsRoomHost)
        {
            Log.Warning($"Cancel {toCancel?.Text?.BriefName}");
            GuestFSM.OnServe(__instance.currentGuestController, null, toCancel.Type);
            return RunOriginal;
        }
        if (MpManager.IsRoomClient)
        {
            Log.Warning($"Cancel {toCancel?.Text?.BriefName}");
            GuestFSM.OnServe(__instance.currentGuestController, null, toCancel.Type);
            return RunOriginal;
        }
        throw new InvalidOperationException("Unexpected network state in Cancel_Postfix");
    }
}
