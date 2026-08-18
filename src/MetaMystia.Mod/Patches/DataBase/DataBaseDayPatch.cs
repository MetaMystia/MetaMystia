using HarmonyLib;

using GameData.Core.Collections.DaySceneUtility;
using GameData.Core.Collections.DaySceneUtility.Collections;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(GameData.Core.Collections.DaySceneUtility.DataBaseDay))]
[AutoLog]
public partial class DataBaseDayPatch
{

    [HarmonyPatch(nameof(DataBaseDay.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        Log.LogInfo("DataBaseDay.Initialize Postfix called.");
        ResourceExManager.OnDataBaseDayInitialized();
    }

    [HarmonyPatch(nameof(DataBaseDay.RefMerchant))]
    [HarmonyPrefix]
    public static bool RefMerchant_Prefix(ref Merchant __result, string key)
    {
        Log.Info($"DataBaseDay.RefMerchant Prefix called with key: {key}");
        if (ResourceExManager.TryGetExMerchantData(key, out Merchant merchant))
        {
            __result = merchant;
            Log.Info($"Returned custom merchant for key: {key}");
            return SkipOriginal;
        }
        // Safety net: if key doesn't exist in base game allMerchants either,
        // skip original to prevent KeyNotFoundException from orphaned save data
        if (!DataBaseDay.allMerchants.ContainsKey(key))
        {
            __result = null;
            Log.Warning($"RefMerchant: key '{key}' not found in any merchant source, returning null to prevent crash.");
            return SkipOriginal;
        }
        return RunOriginal;
    }
}
