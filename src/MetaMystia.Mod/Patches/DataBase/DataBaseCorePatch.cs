using HarmonyLib;

using GameData.Core.Collections;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Core.Collections.DataBaseCore))]
public class DataBaseCorePatch
{
    [HarmonyPatch(nameof(DataBaseCore.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        ResourceExManager.OnDataBaseCoreInitialized();
    }
}
