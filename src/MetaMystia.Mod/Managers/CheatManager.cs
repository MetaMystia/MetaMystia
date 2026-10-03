using Common.UI;
using GameData.Profile;
using NightScene.CookingUtility;

using MetaMystia.Listeners;

namespace MetaMystia;

public static class CheatManager
{
    public static bool TryApplyFever()
    {
        if (!ConfigManager.CheatFever.Value || GameFlow.LocalScene != Scene.WorkScene) return false;

        var reward = QTERewardManager.Instance?.CurrentBuffReward?.TryCast<MystiaQTEBuffReward>();
        if (reward == null) return false;

        QteSync.TriggerInfiniteFeverLocally(reward);
        return true;
    }
}
