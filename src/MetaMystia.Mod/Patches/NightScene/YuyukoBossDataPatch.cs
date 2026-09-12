using System.Collections;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using Il2CppSystem.Linq;
using UnityEngine;

using GameData.Profile;
using NightScene.GuestManagementUtility;

using MetaMystia.Network;
using SgrYuki.Utils;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData))]
[AutoLog]
public partial class YuyukoBossDataPatch
{
    private static GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16 currentLoop;
    private static bool failureStarted;
    private static bool failurePending;

    internal static YuyukoBossData.__c__DisplayClass16_0 CurrentContext => currentLoop?.__8__1;

    internal static void ResetChallenge()
    {
        currentLoop = null;
        failureStarted = false;
        failurePending = false;
        IncomeControllerYuyukoPatch.ResetProgress();
    }

    [HarmonyPatch(nameof(YuyukoBossData.MainChallengeLoop))]
    [HarmonyPostfix]
    public static void MainChallengeLoop_Postfix(Il2CppSystem.Collections.IEnumerator __result)
    {
        currentLoop = MpManager.IsConnected ? __result.Cast<GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16>() : null;
        failureStarted = false;
        failurePending = false;
        IncomeControllerYuyukoPatch.ResetProgress();
    }

    internal static void OnFailureStarted()
    {
        if (!MpManager.IsConnected || failureStarted) return;
        failureStarted = true;
        if (MpManager.IsRoomHost) YuyukoFailedAction.Send();
    }

    public static void ReceiveFailure()
    {
        if (!PrepSceneManager.IsYuyukoChallenge || currentLoop == null || failureStarted || failurePending) return;
        failurePending = true;
        var loop = currentLoop;
        var events = loop.__8__1.eventManager;

        // 主线由 EventManager 启动；停止它也取消正在等待的嵌套计时协程。
        events.StopCoroutine(loop.Cast<Il2CppSystem.Collections.IEnumerator>());
        if (loop._mainLoop_5__6 != null) events.StopCoroutine(loop._mainLoop_5__6);
        if (loop._negativeSpellLoop_5__7 != null) events.StopCoroutine(loop._negativeSpellLoop_5__7);
        if (loop._standSpawnLoop_5__9 != null) events.StopCoroutine(loop._standSpawnLoop_5__9);

        var retake = loop.__8__3;
        if (retake != null)
        {
            foreach (var coroutine in retake.lockCookerCorotine.ToArray())
                if (coroutine != null) events.StopCoroutine(coroutine);
            foreach (var effect in retake.eatingGameObejct.ToArray())
                if (effect != null) Object.Destroy(effect);
        }

        events.StartCoroutine(FinishFailure(loop).WrapToIl2Cpp());
    }

    private static IEnumerator FinishFailure(GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16 loop)
    {
        // 不把失败消息按 DiscardOnStory 丢弃，也不强行打断正在播放的剧情。
        while (MpManager.InStory)
        {
            if (!MpManager.IsConnected || currentLoop?.Pointer != loop.Pointer || !PrepSceneManager.IsYuyukoChallenge)
                yield break;
            yield return null;
        }
        if (!MpManager.IsConnected || currentLoop?.Pointer != loop.Pointer || !PrepSceneManager.IsYuyukoChallenge)
            yield break;

        var context = loop.__8__1;
        var guests = context.guestsManager;
        // 只移除本次挑战登记的观察回调，保留其他营业回调。
        if (guests.OnPositiveSpellTriggered != null)
            foreach (var callback in guests.OnPositiveSpellTriggered.GetInvocationList())
                if (callback.Target?.Pointer == context.Pointer)
                    guests.OnPositiveSpellTriggered -= callback.Cast<Il2CppSystem.Action<SpecialGuestsController>>();
        if (context.statusDisplayer != null && context.eventManager.OnFundUpdateCallback != null)
            foreach (var callback in context.eventManager.OnFundUpdateCallback.GetInvocationList())
                if (callback.Target?.Pointer == context.statusDisplayer?.Pointer)
                    context.eventManager.remove_OnFundUpdateCallback(callback.Cast<Il2CppSystem.Action<int>>());

        if (PrepSceneManager.IsYuyukoPrepActive)
        {
            PrepSceneManager.EndYuyukoPrep();
            var panel = IzakayaConfigPannelPatch.instanceRef;
            if (panel != null && panel.IsPanelOpened)
            {
                Panel.ClosePanelUntil(panel.name, []);
                var closed = panel.OnPanelCloseFadeFinishToken;
                panel.ClosePanel();
                while (!closed.IsCancellationRequested)
                {
                    if (currentLoop?.Pointer != loop.Pointer || !MpManager.IsConnected) yield break;
                    yield return null;
                }
            }
        }
        if (currentLoop?.Pointer != loop.Pointer || !PrepSceneManager.IsYuyukoChallenge) yield break;
        if (context.statusDisplayer != null) context.statusDisplayer.gameObject.SetActive(false);
        if (context.yuyuko != null && context.yuyuko.AllOrdersCount > 0)
            guests.CleanOrderInfo(context.yuyuko);
        foreach (var guest in guests.AllGuestInDeskController.ToArray())
            guest.SetGuestCannotOrder();

        Log.Info("收到主机的幽幽子挑战失败结果，进入原版失败剧情与收尾。");
        var failure = new GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObObObUnique(0) { __4__this = context };
        context.eventManager.StartCoroutine(failure.Cast<Il2CppSystem.Collections.IEnumerator>());
    }
}
