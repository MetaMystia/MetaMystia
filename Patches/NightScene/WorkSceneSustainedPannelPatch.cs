using HarmonyLib;

using NightScene.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.UI.WorkSceneSustainedPannel))]
[AutoLog]
public partial class WorkSceneSustainedPannelPatch
{
    /// <summary>
    /// 客机：阻止快进（跳过夜晚），仅主机可操作
    /// </summary>
    [HarmonyPatch(nameof(WorkSceneSustainedPannel.OnFastForwardSubmit))]
    [HarmonyPrefix]
    public static bool OnFastForwardSubmit_Prefix()
    {
        if (MpManager.IsConnectedClient)
        {
            Log.Message("Client attempted to fast forward, blocked");
            return SkipOriginal;
        }
        return RunOriginal;
    }
}
