using System.Collections.Generic;

using HarmonyLib;

using Common.UI;
using GameData.Core.Collections.DaySceneUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Network;
using MetaMystia.ResourceEx.Registries;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;


[HarmonyPatch(typeof(Common.UI.UniversalGameManager))]
[AutoLog]
public partial class UniversalGameManagerPatch
{
    public static bool WaitingForAdmission => waitingClient != null && waitingClient == GameSession.Client
        && waitingMembership == GameSession.Membership;
    private static Client waitingClient;
    private static long waitingMembership;
    private static bool replayScene;
    [HarmonyPatch(nameof(UniversalGameManager.OpenDialogMenu))]
    [HarmonyPrefix]
    public static bool OpenDialogMenu_Prefix(ref GameData.Profile.DialogPackage dialogPackage, Il2CppSystem.Action onFinishCallback, ref Il2CppSystem.Action<Dictionary<int, string>> overrideReplaceTextCallback, DEYU.AdpUISystem.Managers.AdpUIPanelManager.PanelVisualMode previousPanelVisualMode = DEYU.AdpUISystem.Managers.AdpUIPanelManager.PanelVisualMode.HideVisual)
    {
        if (dialogPackage == null)
        {
            // dialogPackage 为空时，直接调用原方法，将 onFinishCallback 传递下去
            return RunOriginal;
        }

        if (dialogPackage.dialogContext == null)
        {
            if (DialogRegistry.ExampleDialog == null)
            {
                DialogRegistry.DumpExampleDialog();
            }
            dialogPackage.dialogContext = DialogRegistry.ExampleDialog.dialogContext;
            Log.Info($"Replaced dialogPackage.dialogContext with ExampleDialog.dialogContext");
        }
        
        StoryReplayRecentHistory.Record(dialogPackage);
        
        if (DialogRegistry.ExistsDialogPackage(dialogPackage.name) && overrideReplaceTextCallback == null)
        {
            UniversalGameManager.OpenDialogMenu(
                dialogPackage,
                onFinishCallback: onFinishCallback,
                overrideReplaceTextCallback: DialogRegistry.GetOverrideReplaceTextCallback(dialogPackage),
                previousPanelVisualMode: previousPanelVisualMode
            );
            return SkipOriginal;
        }

        // MetaMiku 注:
        //     该 hook 用于在 多人模式 中跳过原有 单人模式 下结束白天时的 OnTransitionToNight 对话
        //     直接执行 onFinishCallback?.Invoke() 会有异步问题，导致挂起
        //     使用携带原有 onFinishCallback 回调的空对话包替代原有对话包实现

        // Log.LogInfo($"OpenDialogMenu called with dialogPackage: {dialogPackage?.name}");

        if (!GameSession.HasPeers || dialogPackage?.name != "OnTransitionToNight") // dialogPackage 可能为空
        {
            return RunOriginal;
        }

        Log.LogInfo($"In multiplayer session and dialogPackage is OnTransitionToNight -> show empty dialog instead");
        UniversalGameManager.OpenDialogMenu(
            null,
            onFinishCallback: onFinishCallback,
            overrideReplaceTextCallback: null,
            previousPanelVisualMode: previousPanelVisualMode
        );
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(UniversalGameManager.LoadScene))]
    [HarmonyPrefix]
    public static bool LoadScene_Prefix(Scene scene, Il2CppSystem.Action onFadeFinishCallback)
    {
        if (scene != Scene.MainScene && GameSession.IsRoomHost && GameFlow.LocalScene == Scene.DayScene && !replayScene)
        {
            if (!WaitingForAdmission)
                PluginHost.Instance.StartManagedCoroutine(WaitForAdmission(scene, onFadeFinishCallback));
            return SkipOriginal;
        }
        if (GameSession.HasPeers)
        {
            if (GameFlow.LocalScene == Scene.DayScene && scene == Scene.WorkScene)
            {
                InGameConsole.ShowPassive(TextId.ChallengeWarning.Get());
            }
        }
        GameFlow.OnSceneTransit(Scene.LoadScene);
        Log.LogInfo($"LoadScene called, scene {scene}");
        return RunOriginal;
    }

    private static System.Collections.IEnumerator WaitForAdmission(Scene scene, Il2CppSystem.Action callback)
    {
        var client = GameSession.Client;
        var membership = GameSession.Membership;
        waitingClient = client;
        waitingMembership = membership;
        var closed = GameSession.SetJoinable(false);
        while (!closed.IsCompleted) yield return null;
        if (waitingClient == client && waitingMembership == membership) waitingClient = null;
        if (!closed.IsCompletedSuccessfully || client != GameSession.Client || membership != GameSession.Membership
            || GameFlow.LocalScene != Scene.DayScene) yield break;
        replayScene = true;
        try { UniversalGameManager.LoadScene(scene, callback); }
        finally { replayScene = false; }
    }
}
