using HarmonyLib;

using GameData.Utils;

using MetaMystia.MetaLib.Storage;

namespace MetaMystia.MetaLib.Patches;

[HarmonyPatch(typeof(GameData.Utils.SaveManagement))]
[AutoLog]
public static partial class SaveManagementPatch
{
    [HarmonyPatch(nameof(SaveManagement.LoadPlayerData))]
    [HarmonyPrefix]
    public static void LoadPlayerData_Prefix() => ModSaveData.BeginLoad();

    [HarmonyPatch(nameof(SaveManagement.LoadPlayerData))]
    [HarmonyPostfix]
    public static void LoadPlayerData_Postfix(bool __runOriginal)
    {
        if (!__runOriginal)
            return;
        ModSaveData.CompleteLoad();
        Log.Info("模组存储已绑定当前存档。");
    }

    [HarmonyPatch(nameof(SaveManagement.WriteCurrentPlayerDataToSlotAsync))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    public static void WriteCurrentPlayerDataToSlotAsync_Prefix(bool __runOriginal)
    {
        // 入口随后启动线程池；在此完成主线程收集，后台只读取已发布的数据。
        if (__runOriginal && !ModSaveData.TryPrepareSave(out var error))
            Log.Error($"模组数据未更新，本次游戏保存沿用原记录：{error}");
    }

    [HarmonyPatch(nameof(SaveManagement.DisposeGameStatusAndBackToMainMenu))]
    [HarmonyPrefix]
    public static void DisposeGameStatusAndBackToMainMenu_Prefix() => ModSaveData.Close();
}
