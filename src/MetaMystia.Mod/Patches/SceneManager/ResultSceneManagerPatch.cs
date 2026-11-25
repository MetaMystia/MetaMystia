using HarmonyLib;

using Common.UI;
using NightScene;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(ResultScene.SceneManager))]
public class ResultSceneManagerPatch
{
    [HarmonyPatch(nameof(SceneManager.Start))]
    [HarmonyPostfix]
    public static void SceneManager_Start_Postfix()
    {
        GameFlow.OnSceneTransit(Scene.ResultScene);
        var callback = NightScene.GuestManagementUtility.GuestsManager.Instance?.getPostprocessCharacterCallback;
        NightScene.GuestManagementUtility.GuestsManager.Instance?.Initialize(callback);
    }
}