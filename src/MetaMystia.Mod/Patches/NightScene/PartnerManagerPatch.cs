using HarmonyLib;

using NightScene.PartnerUtility;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.PartnerUtility.PartnerManager))]
public static class PartnerManagerPatch
{
    /// <summary>料理对决中占桌由房主裁定，原版上菜送达时的占桌不生效。</summary>
    [HarmonyPatch(nameof(PartnerManager.TryAddPlayerOccupiedDeskCode))]
    [HarmonyPrefix]
    public static bool TryAddPlayerOccupiedDeskCode_Prefix(int deskCode) =>
        VersusMatch.AllowOriginalDeskWrite(deskCode, release: false) ? RunOriginal : SkipOriginal;

    /// <summary>料理对决中顾客离桌的原版释放改为房主广播释放（含 Boss 方桌）。</summary>
    [HarmonyPatch(nameof(PartnerManager.ReleasePlayerOccupiedDeskCode))]
    [HarmonyPrefix]
    public static bool ReleasePlayerOccupiedDeskCode_Prefix(int deskCode) =>
        VersusMatch.AllowOriginalDeskWrite(deskCode, release: true) ? RunOriginal : SkipOriginal;
}
