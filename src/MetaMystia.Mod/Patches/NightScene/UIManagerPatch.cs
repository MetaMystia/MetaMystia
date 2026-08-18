using HarmonyLib;

using NightScene.UI;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(NightScene.UI.UIManager))]
[AutoLog]
public partial class UIManagerPatch
{
    [HarmonyPatch(nameof(UIManager.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix(UIManager __instance)
    {
        PlayerManager.RefreshPortrait(true);
    }
}
