using Common.UI;
using GameData.Profile;
using NightScene.CookingUtility;

using MetaMystia.Patch;

namespace MetaMystia;

public static class CheatManager
{
    public static bool TryApplyFever()
    {
        if (!ConfigManager.CheatFever.Value || MpManager.LocalScene != Scene.WorkScene) return false;

        var reward = QTERewardManager.Instance?.CurrentBuffReward?.TryCast<MystiaQTEBuffReward>();
        if (reward == null) return false;

        MystiaQTEBuffRewardPatch.Player_Fever_Infinite_Reverse(reward);
        return true;
    }
}
