using HarmonyLib;

using Common.UI;
using NightScene;

namespace MetaMystia;

[HarmonyPatch(typeof(ResultScene.SceneManager))]
[AutoLog]
public partial class ResultSceneManagerPatch
{
    [HarmonyPatch(nameof(SceneManager.Start))]
    [HarmonyPostfix]
    public static void SceneManager_Start_Postfix()
    {
        MpManager.OnSceneTransit(Scene.ResultScene);
        var callback = NightScene.GuestManagementUtility.GuestsManager.Instance?.getPostprocessCharacterCallback;
        NightScene.GuestManagementUtility.GuestsManager.Instance?.Initialize(callback);
    }
}