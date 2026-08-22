using System.Collections.Generic;

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
        var flags = DlcPack.Core;
        var tags = new List<string>();
        foreach (var dlc in __result)
        {
            flags |= DlcStandardTable.KeyToDlc(dlc);
            tags.Add(dlc);
        }
        Plugin.DlcFlags = flags;
        ResourceExManager.SetActiveDlcTags(tags);
        ResourceExManager.OnDlcFlagsDetermined();
        Log.Warning($"Active DLC Flags: {flags}");
    }
}
