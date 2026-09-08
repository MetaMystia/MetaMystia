using HarmonyLib;

using Common.UI;
using NightScene;

using MetaMystia.Network;

namespace MetaMystia;

[HarmonyPatch(typeof(SceneManager))]
public static class NightSceneManagerPatch
{
    [HarmonyPatch(nameof(SceneManager.Start))]
    [HarmonyPostfix]
    public static void NightScene_Start_Postfix()
    {
        MpManager.OnSceneTransit(Scene.WorkScene);
        CheatManager.TryApplyFever();
        PlayerManager.InitLocalSkin();
        PrepSceneManager.ClearPrepTable();
        if (MpManager.CanSeeOnlinePlayers) PlayerChangeSkinAction.Send(PlayerManager.Local.Skin);
        PlayerManager.SpawnPeers();
    }
}
