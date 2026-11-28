using HarmonyLib;

using GameData.Profile;
using GameData.RunTime.Common;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Profile.DLC3_MausoleumCuisineCompetitionBossData))]
public static class MausoleumBossDataPatch
{
    /// <summary>原版只在此把 Boss 加入伙伴；料理对决清空伙伴且不加入 Boss，场上没有任何伙伴。</summary>
    [HarmonyPatch(nameof(DLC3_MausoleumCuisineCompetitionBossData.GotoBossChallengeScene))]
    [HarmonyPrefix]
    public static bool GotoBossChallengeScene_Prefix()
    {
        if (!VersusManager.IsActive) return RunOriginal;
        StatusTracker.Instance.CleanPartner();
        Log.Info("versus: boss partner skipped");
        return SkipOriginal;
    }
}
