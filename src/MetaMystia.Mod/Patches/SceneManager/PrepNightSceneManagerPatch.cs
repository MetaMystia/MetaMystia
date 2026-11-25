using Common.UI;
using HarmonyLib;
using PrepNightScene;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(PrepNightScene.SceneManager))]
public class PrepNightSceneManagerPatch
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