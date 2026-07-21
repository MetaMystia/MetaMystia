using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Dynamic.Utils;

using DayScene.UI;
using DEYU.AdpUISystem.Managers;

using SgrYuki.Utils;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.UI.UIManager))]
[AutoLog]
public partial class DaySceneUIManagerPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(
        nameof(UIManager.OpenAfterChatMenu),
        typeof(Il2CppReferenceArray<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback>),
        typeof(string),
        typeof(DaySceneChatSelectionPannel.GeneralOpenContext.EndButtonCallback),
        typeof(Il2CppSystem.Action),
        typeof(int),
        typeof(AdpUIPanelManager.PanelVisualMode))]
    public static void OpenAfterChatMenu_Prefix(
        ref Il2CppReferenceArray<DaySceneChatSelectionPannel.GetSelectionConfigurationCallback> configurationCallbacks,
        string endButtonTitleKey,
        Il2CppSystem.Action onExitCallback,
        int indexToSelct) // ignore: typo
    {
        if (!CollabBehaviourComponentPatch.PendingCollabMenu.TryConsume()) return;
        configurationCallbacks = configurationCallbacks
            .ToIl2CppReferenceArray()
            .AddLast(StoryReplayManager.CreateCollabMenuSelection())
            .ToIl2CppReferenceArray();
    }
}
