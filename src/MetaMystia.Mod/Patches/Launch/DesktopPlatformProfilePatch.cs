using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

using GamePlatform.Profiles;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(GamePlatform.Profiles.SteamPlatformProfile))]
[AutoLog]
public partial class SteamPlatformProfilePatch
{
    [HarmonyPatch(nameof(SteamPlatformProfile.GetActiveKeys))]
    [HarmonyPostfix]
    public static void GetActiveKeys_Postfix(Il2CppStringArray __result)
    {
        Log.Warning("==== ==== ==== ====");
        foreach (var dlc in __result)
        {
            Log.Warning(dlc);
        }
        Log.Warning("==== ==== ==== ====");
    }
}
