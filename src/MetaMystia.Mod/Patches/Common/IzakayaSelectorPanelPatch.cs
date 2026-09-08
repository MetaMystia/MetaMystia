using HarmonyLib;
using System.Collections.Generic;

using Common.UI;

using MetaMystia.Network;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(Common.UI.IzakayaSelectorPanel_New))]
[AutoLog]
public partial class IzakayaSelectorPanelPatch
{
    public static IzakayaSelectorPanel_New instanceRef = null;
    public static Dictionary<MapLabel, Common.UI.GlobalMap.IGuideMapSpot> cachedSpots = new();

    [HarmonyPatch(nameof(IzakayaSelectorPanel_New.OnGuideMapInitialize))]
    [HarmonyPrefix]
    public static void OnGuideMapInitialize_Prefix(IzakayaSelectorPanel_New __instance)
    {
        instanceRef = __instance;
        Log.LogInfo($"OnGuideMapInitialize called");
    }

    [HarmonyPatch(nameof(IzakayaSelectorPanel_New._OnGuideMapInitialize_b__21_0))]
    [HarmonyPrefix]
    public static bool _OnGuideMapInitialize_b__21_0_Prefix(ref IzakayaSelectorPanel_New __instance)
    {
        // 本地只提交选择；RoomGameplay 在参与者选择一致后发布 Prep 阶段并推进原流程。

        Log.Info($"_OnGuideMapInitialize_b__21_0 called");

        if (!MpManager.IsInRoom)
        {
            Log.Info($"Not in multiplayer session, skipping patch");
            return RunOriginal;
        }

        var izakayaMapLabel = MapLabelExtensions.FromMapKey(__instance.m_CurrentSelectedSpot.PrimaryName);
        var izakayaLevel = (int)__instance.m_CurrentSelectedIzakayaLevel;
        Log.Message($"Selected Spot: {izakayaMapLabel.ToMapKey()}, Level: {izakayaLevel}");

        SelectIzakayaAction.Send(izakayaMapLabel, izakayaLevel);
        InGameConsole.ShowPassive(TextId.WaitingForHostConfirm.Get(izakayaMapLabel.FormatIzakayaSelection(izakayaLevel)));
        return SkipOriginal;
    }

    public static void TryProceedWithConfirmedSelection(MapLabel mapLabel, IzakayaLevel mapLevel)
    {
        SgrYuki.Utils.Panel.CloseActivePanelsBeforeSceneTransit();

        if (instanceRef != null)
        {
            instanceRef.m_CurrentSelectedIzakayaLevel = mapLevel;
            if (cachedSpots.TryGetValue(mapLabel, out var mapSpot))
            {
                OnGuideMapSpotSelected_ReversePatch(instanceRef, mapSpot);
            }
            _OnGuideMapInitialize_b__21_0_ReversePatch(instanceRef);
        }
        else
        {
            Log.Error("instanceRef is null, cannot call original method");
        }
    }


    [HarmonyPatch(nameof(IzakayaSelectorPanel_New._OnGuideMapInitialize_b__21_0))]
    [HarmonyReversePatch]
    public static void _OnGuideMapInitialize_b__21_0_ReversePatch(IzakayaSelectorPanel_New __instance)
    { }

    [HarmonyPatch(nameof(IzakayaSelectorPanel_New.OnGuideMapSpotSelected))]
    [HarmonyPrefix]
    public static void OnGuideMapSpotSelected_Prefix(ref Common.UI.GlobalMap.IGuideMapSpot guideMapSpot)
    {
        if (guideMapSpot != null && MapLabelExtensions.TryFromMapKey(guideMapSpot.PrimaryName, out var mapLabel))
        {
            cachedSpots[mapLabel] = guideMapSpot;
        }

        Log.Info($"OnGuideMapSpotSelected called, guideMapSpot.PrimaryName: {guideMapSpot?.PrimaryName}");
    }

    [HarmonyPatch(nameof(IzakayaSelectorPanel_New.OnGuideMapSpotSelected))]
    [HarmonyReversePatch]
    public static void OnGuideMapSpotSelected_ReversePatch(IzakayaSelectorPanel_New __instance, Common.UI.GlobalMap.IGuideMapSpot guideMapSpot)
    { }
}
