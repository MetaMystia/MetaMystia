using HarmonyLib;

using GameData.RunTime.Common;
using MetaMystia.Network;


namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeAlbum))]
[AutoLog]
public partial class RunTimeAlbumPatch
{
    [HarmonyPatch(nameof(RunTimeAlbum.RefSpecialNPCId), [typeof(string)])]
    [HarmonyPrefix]
    public static bool RefSpecialNPCId_Prefix(string characterLabel, ref int __result)
    {
        var config = ResourceExManager.GetCharacterConfig(characterLabel);
        if (config == null) return true;

        __result = config.id;
        return false;
    }

    [HarmonyPatch(nameof(RunTimeAlbum.RefOrGenerateSpecialRunTimeData), [typeof(string)])]
    [HarmonyPrefix]
    public static bool RefOrGenerateSpecialRunTimeData_Prefix(
        string npcLabel,
        ref RunTimeAlbum.SpecialGuestRunTimeData __result)
    {
        var config = ResourceExManager.GetCharacterConfig(npcLabel);
        if (config == null) return true;

        __result = RunTimeAlbum.RefOrGenerateSpecialRunTimeData(config.id);
        return false;
    }

    [HarmonyPatch(nameof(RunTimeAlbum.IfGuestHaveSpawnHere), [typeof(string), typeof(int)])]
    [HarmonyPrefix]
    public static bool IfGuestHaveSpawnHere_Prefix(
        string specialGuestLabel,
        int izakayaId,
        ref bool __result)
    {
        var config = ResourceExManager.GetCharacterConfig(specialGuestLabel);
        if (config == null) return true;

        __result = RunTimeAlbum.IfGuestHaveSpawnHere(config.id, izakayaId);
        return false;
    }

    [HarmonyPatch(nameof(RunTimeAlbum.SetGuestSpawnIzakayaId), [typeof(string), typeof(int)])]
    [HarmonyPrefix]
    public static bool SetGuestSpawnIzakayaId_Prefix(string specialGuestLabel, int izakayaId)
    {
        var config = ResourceExManager.GetCharacterConfig(specialGuestLabel);
        if (config == null) return true;

        RunTimeAlbum.SetGuestSpawnIzakayaId(config.id, izakayaId);
        return false;
    }

    [HarmonyPatch(nameof(RunTimeAlbum.ChangePlayerSkin))]
    [HarmonyPostfix]
    public static void ChangePlayerSkin_Postfix(int skinSelectionInfo)
    {
        Log.Info($"Player skin changed to {skinSelectionInfo}");
        PlayerManager.Local.IsCustomSkinOverride = false;
        PlayerManager.InitLocalSkin();
        PlayerChangeSkinAction.Send(PlayerManager.Local.Skin);
    }
}
