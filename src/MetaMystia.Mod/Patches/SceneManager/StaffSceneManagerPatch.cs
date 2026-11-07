using HarmonyLib;

using Common.UI;
using StaffScene;

namespace MetaMystia;

[HarmonyPatch(typeof(StaffScene.SceneManager))]
[AutoLog]
public partial class StaffSceneManagerPatch
{
    [HarmonyPatch(nameof(SceneManager.Start))]
    [HarmonyPostfix]
    public static void StaffScene_Start_Postfix()
    {
        GameFlow.OnSceneTransit(Scene.StaffScene);
    }
}
