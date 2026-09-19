using HarmonyLib;

using GameData.Utils;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Utils.SaveManagement))]
[AutoLog]
public static class SaveManagementPatch
{
    [HarmonyPatch(nameof(SaveManagement.LoadPlayerData))]
    [HarmonyPrefix]
    public static void LoadPlayerData_Prefix() => GameFlow.BeforeStateReset();

    [HarmonyPatch(nameof(SaveManagement.DisposeGameStatusAndBackToMainMenu))]
    [HarmonyPrefix]
    public static void DisposeGameStatusAndBackToMainMenu_Prefix() => GameFlow.BeforeStateReset();

    [HarmonyPatch(nameof(SaveManagement.DisposeGameStatusAndRewindDay))]
    [HarmonyPrefix]
    public static void DisposeGameStatusAndRewindDay_Prefix() => GameFlow.BeforeStateReset();
}
