using HarmonyLib;

using GameData.CoreLanguage.Collections;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.CoreLanguage.Collections.DaySceneLanguage))]
public class DaySceneLanguagePatch
{
    [HarmonyPatch(nameof(DaySceneLanguage.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        ResourceExManager.OnDaySceneLanguageInitialized();
    }
}
