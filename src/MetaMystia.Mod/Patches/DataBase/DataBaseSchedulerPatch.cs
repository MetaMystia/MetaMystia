using HarmonyLib;

using GameData.Core.Collections;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Core.Collections.DataBaseScheduler))]
public class DataBaseSchedulerPatch
{
    [HarmonyPatch(nameof(DataBaseScheduler.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        Log.Info("DataBaseScheduler.Initialize Postfix called.");
        ResourceExManager.OnDataBaseSchedulerInitialized();
    }
}
