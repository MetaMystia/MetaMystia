using HarmonyLib;

using GameData.Utils;

namespace MetaMystia.MystiaLib.Keybinding;

[HarmonyPatch(typeof(GameData.Utils.SaveManagement))]
internal static class SaveManagementPatch
{
    // 改键、重置后原版保存整个资源的覆盖：模组条目改存插件文件。Patch 抛出时 Il2CppInterop 会跳过原方法，本次保存丢失。
    [HarmonyPatch(nameof(SaveManagement.SaveBindingData))]
    [HarmonyPrefix]
    public static void SaveBindingData_Prefix(ref string data)
    {
        if (KeybindStore.Split(data) is { } vanilla)
            data = vanilla;
    }
}
