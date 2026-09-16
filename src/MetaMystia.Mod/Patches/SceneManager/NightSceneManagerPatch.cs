using HarmonyLib;

using Common.UI;
using NightScene;

using MetaMystia.Multiplayer;
using MetaMystia.Patch;

namespace MetaMystia;


[HarmonyPatch(typeof(NightScene.SceneManager))]
[AutoLog]
public static partial class NightSceneManagerPatch
{
    [HarmonyPatch(nameof(SceneManager.Dispose))]
    [HarmonyPrefix]
    public static void Dispose_Prefix() => YuyukoBossDataPatch.ResetChallenge();

    [HarmonyPatch(nameof(SceneManager.Start))]
    [HarmonyPostfix]
    public static void NightScene_Start_Postfix()
    {
        GameFlow.OnSceneTransit(Scene.WorkScene);
        CheatManager.TryApplyFever();
        PlayerManager.Local.ResetState();
        if (GameSession.HasPeers)
        {
            if (!PrepSceneManager.IsYuyukoPrepActive) PrepSceneManager.ClearPrepTable();
            PlayerManager.ResetState();
        }
        GameFlow.OnCharactersReady(Scene.WorkScene);
    }
}
