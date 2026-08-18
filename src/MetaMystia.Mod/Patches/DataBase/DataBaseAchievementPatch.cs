using HarmonyLib;

using GameData.RunTime.Common.AchievementSystem;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.AchievementSystem.DataBaseAchievement))]
[AutoLog]
public partial class DataBaseAchievementPatch
{
    [HarmonyPatch(nameof(DataBaseAchievement.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        Log.LogInfo("DataBaseAchievement.Initialize Postfix called.");
        ResourceExManager.OnDataBaseAchievementInitialized();
    }
}
