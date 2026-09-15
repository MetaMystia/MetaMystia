using Common.UI;
using HarmonyLib;
using PrepNightScene;

namespace MetaMystia;


[HarmonyPatch(typeof(PrepNightScene.SceneManager))]
[AutoLog]
public partial class PrepNightSceneManagerPatch
{
    [HarmonyPatch(nameof(SceneManager.Start))]
    [HarmonyPostfix]
    public static void PrepNightScene_Start_Postfix()
    {
        GameFlow.OnSceneTransit(Scene.IzakayaPrepScene);
        PrepSceneManager.Initialize();
        PrepSceneManager.FlushBufferedTables();
    }
}