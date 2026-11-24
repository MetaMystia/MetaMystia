using HarmonyLib;

using Common.UI.EscapeUtility;

namespace MetaMystia.MystiaLib.Keybinding;

[HarmonyPatch(typeof(Common.UI.EscapeUtility.EscBindingPannel))]
internal static class EscBindingPannelPatch
{
    // 面板在启动加载时初始化一次，原版 DrawBindingElements 按 inputBindingDatas 生成全部行：生成前追加模组行。
    [HarmonyPatch(nameof(EscBindingPannel.OnPanelInitialize))]
    [HarmonyPrefix]
    public static void OnPanelInitialize_Prefix(EscBindingPannel __instance) => Keybinds.AppendRows(__instance);

    // 原版打开时按标题键重新取文本。
    [HarmonyPatch(nameof(EscBindingPannel.OnPanelOpen))]
    [HarmonyPrefix]
    public static void OnPanelOpen_Prefix() => Keybinds.SetTitles();
}
