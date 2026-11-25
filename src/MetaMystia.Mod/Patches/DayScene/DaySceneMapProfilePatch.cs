using HarmonyLib;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(GameData.Profile.DaySceneMapProfile))]
public class DaySceneMapProfilePatch
{
    public static GameData.Profile.DaySceneMapProfile instanceRef = null;

    [HarmonyPatch(MethodType.Constructor)]
    public static void DaySceneMapProfile_Constructor_Postfix(GameData.Profile.DaySceneMapProfile __instance)
    {
        Log.Debug($"DaySceneMapProfile created: {__instance.name}");
        instanceRef = __instance;
    }
}
