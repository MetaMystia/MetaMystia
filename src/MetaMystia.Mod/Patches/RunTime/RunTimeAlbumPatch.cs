using HarmonyLib;

using GameData.RunTime.Common;

using MetaMystia.ResourceEx.Registries;
namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeAlbum))]
[AutoLog]
public partial class RunTimeAlbumPatch
{
    [HarmonyPatch(nameof(RunTimeAlbum.TryRecordUsedDecoration))]
    [HarmonyPostfix]
    public static void TryRecordUsedDecoration_Postfix() => DecorationRegistry.RefreshDayEffects();

    [HarmonyPatch(nameof(RunTimeAlbum.TryRemoveUsedDecoration))]
    [HarmonyPostfix]
    public static void TryRemoveUsedDecoration_Postfix() => DecorationRegistry.RefreshDayEffects();

    [HarmonyPatch(nameof(RunTimeAlbum.ChangePlayerSkin))]
    [HarmonyPostfix]
    public static void ChangePlayerSkin_Postfix(int skinSelectionInfo)
    {
        Log.Info($"Player skin changed to {skinSelectionInfo}");
        PlayerManager.Local.IsCustomSkinOverride = false;
        PlayerManager.InitLocalSkin();
        PlayerProfile.SendProfile();
    }
}
