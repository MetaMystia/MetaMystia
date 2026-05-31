using HarmonyLib;

using NightScene.CookingUtility;

using SgrYuki;
using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.CookingUtility.QTERewardManager))]
[AutoLog]
public partial class QTERewardManagerPatch
{
    public static volatile bool BuffLocalTrigger = true;
    public static volatile bool OnQTESucceededExecuting = false;

    [HarmonyPatch(nameof(QTERewardManager.OnQTESucceeded))]
    [HarmonyPrefix]
    public static bool OnQTESucceeded_Prefix(QTERewardManager __instance, int index, bool mustSuccess)
    {
        Log.Debug($"OnQTESucceeded Prefix, index {index}, mustSuccess {mustSuccess}");
        CommandScheduler.Enqueue(
            executeWhen: () => !OnQTESucceededExecuting,
            executeInfo: "OnQTESucceededExecuting",
            execute: () =>
            {
                OnQTESucceeded(__instance, index, mustSuccess);
            },
            timeoutSeconds: 10f
        );
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(QTERewardManager.OnQTESucceeded))]
    [HarmonyReversePatch]
    private static void OnQTESucceeded_ReversePatch(QTERewardManager __instance, int index, bool mustSuccess)
    { }

    [OnMainThread]
    public static void OnQTESucceeded(QTERewardManager __instance, int index, bool mustSuccess)
    {
        OnQTESucceededExecuting = true;
        OnQTESucceeded_ReversePatch(__instance, index, mustSuccess);
        OnQTESucceededExecuting = false;
    }
}
