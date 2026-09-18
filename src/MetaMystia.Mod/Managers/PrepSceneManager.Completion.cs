using System.Collections;
using System.Linq;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Patch;

namespace MetaMystia;

public static partial class PrepSceneManager
{
    private static bool completingPrep;
    public static void TryCompletePrep()
    {
        if (IsYuyukoChallenge)
        {
            TryConfirmYuyukoPrep();
            return;
        }
        if (!GameSession.IsRoomHost || completingPrep || GameFlow.LocalScene != Scene.IzakayaPrepScene) return;
        if (PlayerManager.LocalIsPrepOver && PlayerManager.Peers.Values.All(p => p.IsPrepOver))
        {
            completingPrep = true;
            PrepAllReadyMessage.Send();
            PluginHost.Instance.StartManagedCoroutine(FinishPrep());
        }
    }

    public static bool ContinuePrep()
    {
        if (IsYuyukoChallenge) return false;
        if (!GameSession.IsRoomHost || completingPrep || GameFlow.LocalScene != Scene.IzakayaPrepScene || !PlayerManager.LocalIsPrepOver)
            return false;
        foreach (var peer in PlayerManager.Peers.Values) peer.IsPrepOver = true;
        completingPrep = true;
        PrepAllReadyMessage.Send();
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
