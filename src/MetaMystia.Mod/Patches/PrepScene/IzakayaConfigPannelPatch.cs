#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

using PrepNightScene.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.UI;
using SgrYuki.Utils;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(PrepNightScene.UI.IzakayaConfigPannel))]
[AutoLog]
public partial class IzakayaConfigPannelPatch
{
    public static IzakayaConfigPannel instanceRef = null;

    [HarmonyPatch(nameof(IzakayaConfigPannel.OnPanelOpen))]
    [HarmonyPrefix]
    public static void OnPanelOpen_Prefix() => PrepSceneManager.IsOpeningPanel = true;

    [HarmonyPatch(nameof(IzakayaConfigPannel.OnPanelOpen))]
    [HarmonyPostfix]
    public static void IzakayaConfigPannel_OnPanelOpen_Postfix(IzakayaConfigPannel __instance)
    {
        instanceRef = __instance;
        PrepSceneManager.IsOpeningPanel = false;
        PrepSceneManager.TryBeginYuyukoPrep();
        if (!PrepSceneManager.IsYuyukoChallenge) PrepSceneManager.BeginPrep();
    }

    [HarmonyPatch(nameof(IzakayaConfigPannel.GoToSpecific))]
    [HarmonyPostfix]
    public static void GoToSpecific_Postfix()
    {
        if (!PrepSceneManager.CanSyncEdits) return;
        // 切页会按本机库存清理厨具，随后恢复联机配置；不产生新的修改请求。
        if (GameSession.IsRoomHost) PrepSceneManager.UpdateGroups();
        else PrepSceneManager.UpdateCookers();
        PrepSceneManager.UpdateUI();
    }

    [HarmonyPatch(nameof(IzakayaConfigPannel._SolveDailyCompletion_b__64_7))]
    [HarmonyPrefix]
    public static bool _SolveDailyCompletion_b__64_7_Prefix()
    {
        if (!GameSession.HasRoomPeers)
        {
            Log.LogDebug($"Not in multiplayer session, skipping patch");
            return RunOriginal;
        }
        if (PrepSceneManager.IsYuyukoChallenge)
        {
            if (!PrepSceneManager.IsYuyukoPrepActive) return RunOriginal;
            if (PlayerManager.LocalIsPrepOver) return SkipOriginal;
        }
        PlayerManager.LocalIsPrepOver = true;
        InGameConsole.ShowPassive(TextId.MystiaReadyForWork.Get());
        PrepReadyMessage.Send();
        if (GameSession.IsRoomHost)
        {
            PrepSceneManager.TryCompletePrep();
        }
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(IzakayaConfigPannel._SolveDailyCompletion_b__64_7))]
    [HarmonyReversePatch]
    private static void _SolveDailyCompletion_b__64_7_ReversePatch(IzakayaConfigPannel __instance)
    { }

    public static void PrepOver()
    {
        Log.Info("PrepOver called");
        if (PrepSceneManager.IsYuyukoChallenge)
        {
            if (!PrepSceneManager.IsYuyukoPrepActive) return;
            PrepSceneManager.EndYuyukoPrep();
        }
        else
        {
            PlayerManager.ResetState();
        }
        string[] ExceptPanels = ["WorkSceneTrayPannel(Clone)", "WorkSceneSustainedPannel(Clone)"];  // 白玉楼测验
        Panel.ClosePanelUntil("IzakayaConfigPannelNew(Clone)", ExceptPanels);
        _SolveDailyCompletion_b__64_7_ReversePatch(instanceRef);
    }
}
