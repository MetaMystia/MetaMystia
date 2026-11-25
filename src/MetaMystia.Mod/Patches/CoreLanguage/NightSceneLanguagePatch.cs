using HarmonyLib;

using GameData.CoreLanguage.Collections;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(GameData.CoreLanguage.Collections.NightSceneLanguage))]
public partial class NightSceneLanguagePatch
{
    [HarmonyPatch(nameof(NightSceneLanguage.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        Log.Info("NightSceneLanguage.Initialize Postfix called.");
        ResourceExManager.OnNightSceneLanguageInitialized();
    }
}

