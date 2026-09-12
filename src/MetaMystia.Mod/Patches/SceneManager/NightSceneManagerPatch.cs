using HarmonyLib;

using Common.UI;
using NightScene;

using MetaMystia.Network;
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

        MpManager.OnSceneTransit(Scene.WorkScene);
        CheatManager.TryApplyFever();
        PlayerManager.Local.ResetState();
        PlayerManager.InitLocalSkin();

        if (!MpManager.CanSeeOnlinePlayers)
        {
            return;
        }
        PlayerChangeSkinAction.Send(PlayerManager.Local.Skin);

        if (!MpManager.IsConnected)
        {
            PlayerManager.SpawnPeers();
            return;
        }

        if (!PrepSceneManager.IsYuyukoPrepActive) PrepSceneManager.ClearPrepTable();

        PlayerManager.ResetState();
        PlayerManager.SpawnPeers();

        CommandScheduler.EnqueueKey(
            key: MpManager.PeerGetCharacterUnitNotNullCommand,
            executeWhen: () => PlayerManager.Peer?.GetCharacterUnit() != null,
            execute: () =>
            {
                PlayerManager.EnablePeerCollision(true);
            },
            timeoutSeconds: 120
        );
    }
}
