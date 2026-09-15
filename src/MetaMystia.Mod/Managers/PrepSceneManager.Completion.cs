using System.Collections;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
using MetaMystia.Patch;

namespace MetaMystia;

public static partial class PrepSceneManager
{
    public static void TryCompletePrep()
    {
        if (IsYuyukoChallenge)
        {
            TryConfirmYuyukoPrep();
            return;
        }
        if (!GameSession.IsRoomHost) return;
        if (PlayerManager.AllPrepOver)
        {
            PrepAllReadyAction.Send();
            PluginHost.Instance.StartManagedCoroutine(FinishPrep());
        }
    }

    public static bool ContinuePrep()
    {
        if (IsYuyukoChallenge) return false;
        if (!GameSession.IsRoomHost || (GameFlow.LocalScene != Scene.IzakayaPrepScene && GameFlow.LocalScene != Scene.WorkScene) || !PlayerManager.LocalIsPrepOver)
            return false;
        foreach (var peer in PlayerManager.Peers.Values) peer.IsPrepOver = true;
        PrepAllReadyAction.Send();
        PluginHost.Instance.StartManagedCoroutine(FinishPrep());
        return true;
    }

    private static IEnumerator FinishPrep()
    {
        var client = GameSession.Client;
        var membership = GameSession.Membership;
        var scene = GameFlow.LocalScene;
        yield return null;
        if (client == GameSession.Client && membership == GameSession.Membership && GameSession.IsRoomHost
            && scene == GameFlow.LocalScene && PlayerManager.LocalIsPrepOver)
            IzakayaConfigPannelPatch.PrepOver();
    }
}
