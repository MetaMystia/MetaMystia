using HarmonyLib;

using GameData.Core.Collections;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Core.Collections.DataBaseCore))]
[AutoLog]
public partial class DataBaseCorePatch
{
    [HarmonyPatch(nameof(DataBaseCore.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        Log.LogInfo("DataBaseCore.Initialize Postfix called.");
        ResourceExManager.OnDataBaseCoreInitialized();
    }
}
