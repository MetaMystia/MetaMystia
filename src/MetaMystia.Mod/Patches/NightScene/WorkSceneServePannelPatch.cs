using HarmonyLib;

using GameData.Core.Collections;
using GameData.RunTime.NightSceneUtility;
using NightScene.UI.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;
using MetaMystia.Multiplayer.Messages;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.UI.GuestManagementUtility.WorkSceneServePannel))]
[TracePatch(nameof(WorkSceneServePannel.OnPanelInitialize))]
[TracePatch(nameof(WorkSceneServePannel.OnPanelOpen))]
[TracePatch(nameof(WorkSceneServePannel.OnPanelDestroyed))]
[TracePatch(nameof(WorkSceneServePannel.OnPanelClose))]
[TracePatch(nameof(WorkSceneServePannel.OnExitExtern))]
[TracePatch(nameof(WorkSceneServePannel.Send))]
[TracePatch(nameof(WorkSceneServePannel.Cancel))]
[AutoLog]
public partial class WorkSceneServePannelPatch
{
    public static WorkSceneServePannel instanceRef;
    public static int PanelDeskCode => instanceRef?.currentGuestController?.DeskCode
    ?? instanceRef?.operatingOrder?.DeskCode
    ?? -1;

    /// <summary>联机时预填该桌当前的待上菜。</summary>
    [HarmonyPatch(nameof(WorkSceneServePannel.OnPanelOpen))]
    [HarmonyPostfix]
    public static void OnPanelOpen_Postfix(WorkSceneServePannel __instance)
    {
        instanceRef = __instance;
        var guest = GuestRegistry.Of(__instance.currentGuestController);
        if (guest == null) return;
        __instance.willServeFood = guest.WillServeFood;
        __instance.willServeBeverage = guest.WillServeBeverage;
        if (guest.WillServeFood != null)
            __instance.SetServedVisualOnUI(__instance.servFood, __instance.servFoodOutline, guest.WillServeFood, true);
        if (guest.WillServeBeverage != null)
            __instance.SetServedVisualOnUI(__instance.servBev, __instance.servBevOutline, guest.WillServeBeverage, true);
    }

    /// <summary>关闭面板即确认上桌。强制关闭（顾客离开、评价重放）经 <see cref="Bypass"/> 放行。</summary>
    [HarmonyPatch(nameof(WorkSceneServePannel.OnPanelClose))]
    [HarmonyPrefix]
    public static void OnPanelClose_Prefix(WorkSceneServePannel __instance)
    {
        instanceRef = null;
        if (Bypass.Active(__instance)) return;
        ConfirmServeMessage.Local(__instance.currentGuestController, __instance.willServeFood, __instance.willServeBeverage);
    }

    /// <summary>
    /// 外部关闭面板：原版退回时总是读取料理槽，酒水丢失。改为逐槽退回；联机只退回并撤回本端放入的物品，他人放入的保留在槽位。
    /// </summary>
    [HarmonyPatch(nameof(WorkSceneServePannel.OnExitExtern))]
    [HarmonyPrefix]
    public static void OnExitExtern_Prefix(WorkSceneServePannel __instance)
    {
        var controller = __instance.currentGuestController;
        var guest = GuestRegistry.Of(controller);
        if (guest == null)
        {
            if (__instance.willServeFood != null) IzakayaTray.Instance.Receive(__instance.willServeFood);
            if (__instance.willServeBeverage != null) IzakayaTray.Instance.Receive(__instance.willServeBeverage);
        }
        else
        {
            foreach (var type in new[] { Sellable.SellableType.Food, Sellable.SellableType.Beverage })
            {
                var claim = guest.Claim(type);
                if (claim == null) continue;
                IzakayaTray.Instance.Receive(claim);
                ServeSellableMessage.Local(controller, null, type);
                guest.SetClaim(type, null);
            }
        }
        __instance.willServeFood = null;
        __instance.willServeBeverage = null;
    }

    [HarmonyPatch(nameof(WorkSceneServePannel.Send))]
    [HarmonyPrefix]
    public static bool Send_Prefix(WorkSceneServePannel __instance, Sellable toSend)
    {
        if (GuestRegistry.Of(__instance.currentGuestController) == null) return RunOriginal;
        // 已上桌或正在投掷时原版不放入，不能记为本端放入。
        var order = __instance.operatingOrder;
        if ((toSend.Type == Sellable.SellableType.Food ? order.ServFood ?? order.ServedFoodInAir : order.ServBeverage ?? order.ServedBeverageInAir) != null)
        {
            Log.Info($"Already have {toSend.Type} in order, skipping Send");
            return SkipOriginal;
        }
        ServeSellableMessage.Local(__instance.currentGuestController, toSend, toSend.Type);
        return RunOriginal;
    }

    [HarmonyPatch(nameof(WorkSceneServePannel.Cancel))]
    [HarmonyPrefix]
    public static bool Cancel_Prefix(WorkSceneServePannel __instance, Sellable toCancel)
    {
        if (GuestRegistry.Of(__instance.currentGuestController) == null) return RunOriginal;
        var slot = toCancel.Type == Sellable.SellableType.Food ? __instance.willServeFood : __instance.willServeBeverage;
        if (slot == null || (IzakayaTray.Instance?.IsTrayFull ?? true))
        {
            Log.Info($"No {toCancel.Type} to cancel or tray full, skipping Cancel");
            return SkipOriginal;
        }
        ServeSellableMessage.Local(__instance.currentGuestController, null, toCancel.Type);
        return RunOriginal;
    }
}
