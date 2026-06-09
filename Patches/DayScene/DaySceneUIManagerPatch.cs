using HarmonyLib;

using Common.UI;
using DEYU.AdpUISystem.Managers;
using GameData.RunTime.DaySceneUtility.Collection;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.UI.UIManager))]
[AutoLog]
public partial class DaySceneUIManagerPatch
{
    [HarmonyPatch(nameof(DayScene.UI.UIManager.OpenShopPannel))]
    [HarmonyPrefix]
    public static bool OpenSoldOutResourceExMerchantDialog_Prefix(TrackedMerchant merchantData, Il2CppSystem.Action onFinishCallback)
    {
        if (merchantData == null)
            return RunOriginal;

        var merchantKey = merchantData.key;
        if (!ResourceExManager.IsTelephoneMerchant(merchantKey) || ResourceExManager.HasSellableProducts(merchantData.products))
            return RunOriginal;

        if (!ResourceExManager.TryGetMerchantNullDialog(merchantKey, out var dialog))
        {
            Log.Warning($"ResourceEx merchant {merchantKey} is sold out but has no null dialog package.");
            onFinishCallback?.Invoke();
            return SkipOriginal;
        }

        Log.Info($"Open sold-out ResourceEx merchant dialog before shop panel: {merchantKey}, dialog={dialog?.name}");
        UniversalGameManager.OpenDialogMenu(
            dialog,
            onFinishCallback: onFinishCallback,
            overrideReplaceTextCallback: null,
            previousPanelVisualMode: AdpUIPanelManager.PanelVisualMode.HideVisual);
        return SkipOriginal;
    }
}
