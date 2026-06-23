using HarmonyLib;

using Common.UI;
using NightScene;

using MetaMystia.Network;
using SgrYuki;

namespace MetaMystia;


[HarmonyPatch(typeof(NightScene.SceneManager))]
[AutoLog]
public static partial class NightSceneManagerPatch
{

    [HarmonyPatch(nameof(SceneManager.Start))]
    [HarmonyPostfix]
    public static void NightScene_Start_Postfix()
    {
        // REFACTORING
        // GuestsManagerPatch.ReimuSpellCard = false;

        MpManager.OnSceneTransit(Scene.WorkScene);
        PlayerManager.Local.ResetState();
        PlayerManager.InitLocalSkin();

        if (!MpManager.CanSeeOnlinePlayers)
        {
            return;
        }
        PlayerChangeSkinBehavior.Send(PlayerManager.Local.Skin);

        PrepSceneManager.ClearPrepTable();

        PlayerManager.ResetState();
        PlayerManager.SpawnRoomPeers();

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
