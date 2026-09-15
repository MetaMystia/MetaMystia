using HarmonyLib;

using DayScene.UI;
using GameData.RunTime.DaySceneUtility;

using static MetaMystia.Patch.HarmonyPrefixFlow;

using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.UI.DaySceneSustainedPannel))]
[AutoLog]
public partial class DaySceneSustainedPannelPatch
{
    [HarmonyPatch(nameof(DaySceneSustainedPannel.OnFastForwardSubmit))]
    [HarmonyPrefix]
    public static bool OnFastForwardSubmit_Prefix(DaySceneSustainedPannel __instance)
    {
        if (!GameSession.HasPeers || DayDestinationManager.ReplayingBusiness) return RunOriginal;
        // WarpToNight 会先耗尽行动点；在此之前等待，才能改选挑战。
        DayDestinationManager.Submit(DayDestination.Business, () =>
        {
            if (RunTimeDayScene.RemainActions == 0) DaySceneManagerPatch.OnDayOver();
            else __instance.OnFastForwardSubmit();
        });
        return SkipOriginal;
    }
}
