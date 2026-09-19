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
            IzakayaConfigPannelPatch.PrepOver();
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
        IzakayaConfigPannelPatch.PrepOver();
        return true;
    }

}
