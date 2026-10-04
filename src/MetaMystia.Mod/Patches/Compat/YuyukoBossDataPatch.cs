#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using System.Collections;

using HarmonyLib;
using Il2CppSystem.Linq;
using UnityEngine;

// 与旧 Challange 实现一致：消除 System.Object 与 UnityEngine.Object 的 Object 二义性。
using Object = UnityEngine.Object;

using GameData.Profile;
using NightScene.GuestManagementUtility;

using MetaMystia.Listeners;
using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using SgrYuki.Utils;

namespace MetaMystia.Patch;

/// <summary>
/// 挑战闭包上的两项纯数值（伤害倍率、单阶段基准时长），以及主机的失败结果在客机上的整段重放。
/// <para>
/// 缺口说明：框架的挑战时间线只报告 <c>OnChallengeFailureStarted</c>，不提供「用挑战自己的失败协程在
/// 客机上重放」的能力。重放必须停止主循环及其嵌套计时协程，并用挑战闭包构造失败状态机（
/// <c>ObjectCompilerGenerated…InObObObUnique</c>），因此本文件保留为兼容缺口；相应地，基准阶段时长也
/// 只能从这里读出后交给挑战服务，见 <see cref="YuyukoChallengeSync"/>。
/// </para>
/// </summary>
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData))]
[AutoLog]
public partial class YuyukoBossDataPatch
{
    private static GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16 currentLoop;
    private static bool failureStarted;
    private static bool failurePending;

    private static YuyukoBossData.__c__DisplayClass16_0 CurrentContext => currentLoop?.__8__1;

    [HarmonyPatch(nameof(YuyukoBossData.MainChallengeLoop))]
    [HarmonyPostfix]
    public static void MainChallengeLoop_Postfix(Il2CppSystem.Collections.IEnumerator __result)
    {
        currentLoop = GameSession.HasRoomPeers ? __result.Cast<GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16>() : null;
        failureStarted = false;
        failurePending = false;
        YuyukoGuestSync.ResetLife();
    }

    /// <summary>
    /// 失败剧情开始：主机广播一次失败结果。原实现挂在失败协程的第一个恢复位置上，
    /// 现由挑战监听的 <c>OnChallengeFailureStarted</c> 调用，语义不变。
    /// </summary>
    internal static void OnFailureStarted()
    {
        if (!GameSession.HasRoomPeers || failureStarted) return;
        failureStarted = true;
        if (GameSession.IsRoomHost) YuyukoFailedMessage.Send();
    }

    public static void ReceiveFailure()
    {
        if (!PrepSceneManager.IsYuyukoChallenge || currentLoop == null || failureStarted || failurePending) return;
        failurePending = true;
        YuyukoGuestSync.EndPhase3();
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

        // 常驻调度器持有这条收尾协程；它只等待剧情结束与面板淡出，不依赖挑战自己的 MonoBehaviour。
        var coroutines = ModRuntime.Coroutines;
        coroutines.StartOn(coroutines.Owner, _ => FinishFailure(loop));
    }

    private static IEnumerator FinishFailure(GameData.Profile.YuyukoBossData._MainChallengeLoop_d__16 loop)
    {
        // 不把失败消息按 DiscardOnStory 丢弃，也不强行打断正在播放的剧情。
        while (GameFlow.InStory)
        {
            if (!GameSession.HasRoomPeers || currentLoop?.Pointer != loop.Pointer || !PrepSceneManager.IsYuyukoChallenge)
                yield break;
            yield return null;
        }
        if (!GameSession.HasRoomPeers || currentLoop?.Pointer != loop.Pointer || !PrepSceneManager.IsYuyukoChallenge)
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
            // 面板视图按打开的面板缓存，因此它给出的名字与实例仍是原版面板本身；先关掉压在其上的面板，
            // 再走原版关闭路径，并等待关闭淡出结束。
            var panel = PrepSync.ConfigPanel;
            if (panel is { IsOpen: true })
            {
                Panel.ClosePanelUntil(panel.Name, []);
                var closed = panel.CloseWithFadeToken();
                while (!closed.IsCancellationRequested)
                {
                    if (currentLoop?.Pointer != loop.Pointer || !GameSession.HasRoomPeers) yield break;
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
