using HarmonyLib;
using System.Collections.Generic;

using MetaMystia.Network;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(Common.UI.IzakayaSelectorPanel_New))]
[AutoLog]
public partial class IzakayaSelectorPanelPatch
{
    public static Common.UI.IzakayaSelectorPanel_New instanceRef = null;
    public static Dictionary<string, Common.UI.GlobalMap.IGuideMapSpot> cachedSpots = new Dictionary<string, Common.UI.GlobalMap.IGuideMapSpot>();

    [HarmonyPatch(nameof(Common.UI.IzakayaSelectorPanel_New.OnGuideMapInitialize))]
    [HarmonyPrefix]
    public static void OnGuideMapInitialize_Prefix(Common.UI.IzakayaSelectorPanel_New __instance)
    {
        instanceRef = __instance;
        Log.LogInfo($"OnGuideMapInitialize called");
    }

    [HarmonyPatch(nameof(Common.UI.IzakayaSelectorPanel_New._OnGuideMapInitialize_b__21_0))]
    [HarmonyPrefix]
    public static bool _OnGuideMapInitialize_b__21_0_Prefix(ref Common.UI.IzakayaSelectorPanel_New __instance)
    {
        // N 人联机选店流程:
        //   1. 每个玩家自由选择地图，点击「前往营业」
        //   2. 广播 SELECT 通告所有 peer 自己的选择（主机需负责转发）
        //   3. 主机收到 SELECT 或主机自己选择后，负责检查所有 peer 的选择是否一致
        //     - 若全员一致，主机广播 CONFIRM_SELECT，客机收到后才执行场景切换
        //   4. 客机仅发送 SELECT，然后等待主机的 CONFIRM_SELECT

        Log.Info($"_OnGuideMapInitialize_b__21_0 called");

        if (!MpManager.IsConnected)
        {
            Log.Info($"Not in multiplayer session, skipping patch");
            return RunOriginal;
        }

        var izakayaMapLabel = __instance.m_CurrentSelectedSpot.PrimaryName;
        var izakayaLevel = (int)__instance.m_CurrentSelectedIzakayaLevel;
        Log.Message($"Selected Spot: {izakayaMapLabel}, Level: {izakayaLevel}");

        // 记录自己的选择
        PlayerManager.Local.IzakayaMapLabel = izakayaMapLabel;
        PlayerManager.Local.IzakayaLevel = izakayaLevel;

        // 广播自己的选择
        SelectAction.Send(izakayaMapLabel, izakayaLevel);

        var mySelect = $"{MystiaUtils.GetMapLabelNameCN(izakayaMapLabel)} {MystiaUtils.GetMapLevelNameCN(izakayaLevel)}";

        if (MpManager.IsClient)
        {
            // 客机：发送 SELECT 后等待主机 CONFIRM，同时展示当前状态
            InGameConsole.ShowPassive(TextId.WaitingForHostConfirm.Get(mySelect));
            ShowSelectionStatus();
            return SkipOriginal;
        }
        // 主机：检查所有 peer 是否已选择且一致
        TryConfirmSelection();
        return SkipOriginal;
    }

    /// <summary>
    /// 主机侧：检查全员选店是否一致，若一致则广播 CONFIRM_SELECT 并本地执行切换
    /// </summary>
    public static void TryConfirmSelection()
    {
        var mapLabel = PlayerManager.Local.IzakayaMapLabel;
        var level = PlayerManager.Local.IzakayaLevel;

        // 主机自己还没选择
        if (string.IsNullOrEmpty(mapLabel) || level == 0)
        {
            Log.Info("Host has not selected izakaya yet, waiting...");
            return;
        }

        var mySelect = $"{MystiaUtils.GetMapLabelNameCN(mapLabel)} {MystiaUtils.GetMapLevelNameCN(level)}";

        if (!PlayerManager.AllPeersSelectedSameIzakaya(mapLabel, level))
        {
            var mismatch = PlayerManager.GetFirstMismatchSelection(mapLabel, level);
            Log.LogWarning($"Selection mismatch: my={mySelect}, peer={mismatch}");
            InGameConsole.ShowPassive(TextId.SelectedIzakayaMismatch.Get(mySelect, mismatch ?? "???"));
            return;
        }

        // 全员一致 → 广播 CONFIRM_SELECT → 本地执行切换
        Log.LogMessage($"All peers match selection: {mySelect}, broadcasting CONFIRM and proceeding");
        ConfirmSelectAction.Broadcast(mapLabel, level);
        InGameConsole.ShowPassive(TextId.SelectedIzakaya.Get(mySelect));
        SgrYuki.Utils.Panel.CloseActivePanelsBeforeSceneTransit();
        _OnGuideMapInitialize_b__21_0_Original(instanceRef);
    }

    /// <summary>
    /// 客机侧：收到其他玩家的 SELECT 后，显示当前全员选店状态摘要
    /// </summary>
    public static void ShowSelectionStatus()
    {
        var myMapLabel = PlayerManager.Local.IzakayaMapLabel;
        var myLevel = PlayerManager.Local.IzakayaLevel;

        // 自己还没选，不显示摘要
        if (string.IsNullOrEmpty(myMapLabel) || myLevel == 0) return;

        var mySelect = $"{MystiaUtils.GetMapLabelNameCN(myMapLabel)} {MystiaUtils.GetMapLevelNameCN(myLevel)}";

        if (!PlayerManager.AllPeersSelectedSameIzakaya(myMapLabel, myLevel))
        {
            var mismatch = PlayerManager.GetFirstMismatchSelection(myMapLabel, myLevel);
            InGameConsole.ShowPassive(TextId.SelectedIzakayaMismatch.Get(mySelect, mismatch ?? "???"));
        }
    }

    [HarmonyPatch(nameof(Common.UI.IzakayaSelectorPanel_New._OnGuideMapInitialize_b__21_0))]
    [HarmonyReversePatch]
    public static void _OnGuideMapInitialize_b__21_0_Original(Common.UI.IzakayaSelectorPanel_New __instance)
    {
        throw new System.NotImplementedException("It's a stub");
    }

    [HarmonyPatch(nameof(Common.UI.IzakayaSelectorPanel_New.OnGuideMapSpotSelected))]
    [HarmonyPrefix]
    public static void OnGuideMapSpotSelected_Prefix(ref Common.UI.GlobalMap.IGuideMapSpot guideMapSpot)
    {
        if (guideMapSpot != null && !string.IsNullOrEmpty(guideMapSpot.PrimaryName))
        {
            cachedSpots[guideMapSpot.PrimaryName] = guideMapSpot;
        }

        Log.Info($"OnGuideMapSpotSelected called, guideMapSpot.PrimaryName: {guideMapSpot.PrimaryName}");
    }
}
