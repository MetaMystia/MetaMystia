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

    [HarmonyPatch(nameof(IzakayaConfigPannel.LoadPresetInternal))]
    [HarmonyPrefix]
    public static void LoadPreset_Prefix(out UpdatePrepMessage.Table __state)
    {
        __state = PrepSceneManager.CanSyncEdits ? PrepSceneManager.CaptureTable() : null;
        PrepSceneManager.IsEditingPreset = true;
    }

    [HarmonyPatch(nameof(IzakayaConfigPannel.LoadPresetInternal))]
    [HarmonyPostfix]
    public static void LoadPreset_Postfix(UpdatePrepMessage.Table __state)
    {
        PrepSceneManager.IsEditingPreset = false;
        PrepSceneManager.FinishLocalEdit(__state, true);
    }

    [HarmonyPatch(nameof(IzakayaConfigPannel.GoToSpecific))]
    [HarmonyPostfix]
    public static void IzakayaConfigPannel_GoToSpecific_Postfix()
    {
        if (GameSession.HasRoomPeers == false)
        {
            Log.LogDebug($"Not in multiplayer session, skipping patch");
            return;
        }

        if (!PrepSceneManager.CanSyncEdits) return;

        // MetaMiku 注:
        //     游戏原生的 GoToSpecific 会变更玩家的活跃选项面板，即 菜谱/酒水/厨具 三选一
        //     但是还会附带检查除去不合法的 厨具 选项
        //     如果在联机中直接调用该方法，可能会导致 厨具 选项出现不同步的问题
        //     因此这里做了一个补丁，强制在调用 GoToSpecific 之后再重新更新厨具选项
        PluginManager.RunOnMainThread(() =>
        {
            PrepSceneManager.UpdateCookers();
            PrepSceneManager.UpdateUI();
        });

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
