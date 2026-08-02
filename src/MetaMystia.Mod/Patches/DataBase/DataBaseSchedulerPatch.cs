using HarmonyLib;

using GameData.Core.Collections;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Core.Collections.DataBaseScheduler))]
[AutoLog]
public partial class DataBaseSchedulerPatch
{
    [HarmonyPatch(nameof(DataBaseScheduler.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        Log.LogInfo("DataBaseScheduler.Initialize Postfix called.");
        ResourceExManager.OnDataBaseSchedulerInitialized();
    }
}
