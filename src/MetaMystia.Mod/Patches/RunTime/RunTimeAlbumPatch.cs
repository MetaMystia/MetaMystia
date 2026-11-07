using HarmonyLib;

using GameData.RunTime.Common;

using MetaMystia.ResourceEx.Registries;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeAlbum))]
[AutoLog]
public partial class RunTimeAlbumPatch
{
    // ResourceEx 稀客没有 DataBaseDay NPC，原版按标签查询会失败；改用配置中的 id。
    [HarmonyPatch(nameof(RunTimeAlbum.RefSpecialNPCId), [typeof(string)])]
    [HarmonyPrefix]
    public static bool RefSpecialNPCId_Prefix(string characterLabel, ref int __result)
    {
        if (SpecialGuestRegistry.GetCharacterConfig(characterLabel) is not { } config)
            return RunOriginal;

        __result = config.id;
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(RunTimeAlbum.RefOrGenerateSpecialRunTimeData), [typeof(string)])]
    [HarmonyPrefix]
    public static bool RefOrGenerateSpecialRunTimeData_Prefix(string npcLabel, ref RunTimeAlbum.SpecialGuestRunTimeData __result)
    {
        if (SpecialGuestRegistry.GetCharacterConfig(npcLabel) is not { } config)
            return RunOriginal;

        __result = RunTimeAlbum.RefOrGenerateSpecialRunTimeData(config.id);
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(RunTimeAlbum.IfGuestHaveSpawnHere), [typeof(string), typeof(int)])]
    [HarmonyPrefix]
    public static bool IfGuestHaveSpawnHere_Prefix(string specialGuestLabel, int izakayaId, ref bool __result)
    {
        if (SpecialGuestRegistry.GetCharacterConfig(specialGuestLabel) is not { } config)
            return RunOriginal;

        __result = RunTimeAlbum.IfGuestHaveSpawnHere(config.id, izakayaId);
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(RunTimeAlbum.SetGuestSpawnIzakayaId), [typeof(string), typeof(int)])]
    [HarmonyPrefix]
    public static bool SetGuestSpawnIzakayaId_Prefix(string specialGuestLabel, int izakayaId)
    {
        if (SpecialGuestRegistry.GetCharacterConfig(specialGuestLabel) is not { } config)
            return RunOriginal;

        RunTimeAlbum.SetGuestSpawnIzakayaId(config.id, izakayaId);
        return SkipOriginal;
    }

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
