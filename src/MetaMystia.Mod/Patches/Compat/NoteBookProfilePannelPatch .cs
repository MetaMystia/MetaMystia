using HarmonyLib;

using Common.UI.NoteBookUtility;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(Common.UI.NoteBookUtility.NoteBookProfilePannel))]
[AutoLog]
public partial class NoteBookProfilePannelPatch
{
    /// <summary>
    /// 用于支持在 NoteBook 中替换原有立绘，但图片有可能超出范围，仍需调整
    /// 需要启用 ConfigManager.NoteBookSkinPortrait 配置项
    /// </summary>
    [HarmonyPatch(nameof(NoteBookProfilePannel.OnPanelOpen))]
    [HarmonyPostfix]
    public static void OnPanelOpen_Postfix(ref NoteBookProfilePannel __instance)
    {
        if (!ConfigManager.NoteBookSkinPortrait.Value) return;
        if (PlayerManager.Local?.IsCustomSkinOverride != true) return;

        var sprite = PlayerManager.Local.Skin.ResolvePortraitSprite();
        if (sprite != null)
        {
            __instance.mystiaPic.sprite = sprite;
        }
    }
}
