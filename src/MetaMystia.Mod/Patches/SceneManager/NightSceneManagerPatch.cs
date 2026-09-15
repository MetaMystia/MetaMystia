using HarmonyLib;

using Common.UI;
using NightScene;

using MetaMystia.Multiplayer;
using MetaMystia.Patch;
using SgrYuki;

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
        // REFACTORING
        // GuestsManagerPatch.ReimuSpellCard = false;

        GameFlow.OnSceneTransit(Scene.WorkScene);
        CheatManager.TryApplyFever();
        PlayerManager.Local.ResetState();
        PlayerManager.InitLocalSkin();

        if (!GameSession.IsOnline)
        {
            return;
        }
        PlayerProfile.SendProfile();

        if (!GameSession.HasPeers)
        {
            PlayerManager.SpawnPeers();
            return;
        }

        if (!PrepSceneManager.IsYuyukoPrepActive) PrepSceneManager.ClearPrepTable();

        PlayerManager.ResetState();
        PlayerManager.SpawnPeers();

        CommandScheduler.EnqueueKey(
            key: "PeerCollision",
            executeWhen: () => PlayerManager.Peer?.GetCharacterUnit() != null,
            execute: () =>
            {
                PlayerManager.EnablePeerCollision(true);
            },
            timeoutSeconds: 120
        );
    }
}
