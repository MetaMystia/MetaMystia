using HarmonyLib;

using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.StatusTracker))]
[AutoLog]
public static partial class StatusTrackerPatch
{
    [HarmonyPatch(nameof(StatusTracker.RecordInvitedGuest))]
    [HarmonyPostfix]
    public static void RecordInvitedGuest_Postfix(int guestId)
    {
        Log.Info($"RecordInvitedGuest_Postfix called, guestId {guestId}, invited {guestId.GetSpecialGuestLang().BriefName}");
    }

    [HarmonyPatch(nameof(StatusTracker.RecordInvitedGuest))]
    [HarmonyReversePatch]
    public static void RecordInvitedGuest_ReversePatch(StatusTracker __instance, int guestId)
    { }
}
