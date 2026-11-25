
using HarmonyLib;

using GameData.CoreLanguage.Collections;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.CoreLanguage.Collections.DataBaseLanguage))]
public class DataBaseLanguagePatch
{
    [HarmonyPatch(nameof(DataBaseLanguage.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        Log.Info("DataBaseLanguage.Initialize Postfix called.");
        ResourceExManager.OnDataBaseLanguageInitialized();
    }
}
