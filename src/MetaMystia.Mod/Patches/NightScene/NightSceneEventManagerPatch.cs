using System;

using HarmonyLib;

using NightScene.EventUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.EventUtility.EventManager))]
[AutoLog]
public static partial class NightSceneEventManagerPatch
{
    public static readonly PatchBypassToken HostCloseReplay = new();
    public static bool IsHostCloseReplay => HostCloseReplay.Pending > 0;

    [HarmonyPatch(nameof(EventManager.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix(EventManager __instance)
    {
        if (!GameSession.HasPeers) return;

        Func<int> getWholeNightTime = () => GameFlow.WorkTimeSecondOverride;
        __instance.GetWholeNightTime = getWholeNightTime;
    }

    [HarmonyPatch(nameof(EventManager.StartGuestInstantiateLoop))]
    [HarmonyPrefix]
    public static bool StartGuestInstantiateLoop_Prefix() => ShouldSkipGuestLoopWhenFlowRateZero();

    [HarmonyPatch(nameof(EventManager.StartChallengeGuestInstantiateLoop))]
    [HarmonyPrefix]
    public static bool StartChallengeGuestInstantiateLoop_Prefix() => ShouldSkipGuestLoopWhenFlowRateZero();

    private static bool ShouldSkipGuestLoopWhenFlowRateZero()
    {
        if (GameSession.IsRoomClient) return RunOriginal;
        return ConfigManager.CheatFlowRate.Value == 0f ? SkipOriginal : RunOriginal;
    }

    [HarmonyPatch(nameof(EventManager.StartGuestInstantiateLoop))]
    [HarmonyPostfix]
    public static void StartGuestInstantiateLoop_Postfix(EventManager __instance)
    {
        if ((GameSession.IsRoomClient && GameSession.HasPeers) && __instance.onCreatorBoxGuestInstantiateLoop != null)
        {
            __instance.onCreatorBoxGuestInstantiateLoop = null;
            Log.Warning("已临时禁用造物者之盒协程。");
        }
    }
    

    [HarmonyPatch(nameof(EventManager.Fever))]
    [HarmonyPrefix]
    public static void Fever_Prefix(EventManager __instance, int durationSec)
    {
        Log.Info($"Fever Prefix, durationSec {durationSec}");
        if (QTERewardManagerPatch.BuffLocalTrigger)
        {
            BuffAction.Send(QTEBuff.Fever);
        }
    }

    [HarmonyPatch(nameof(EventManager.StartGuestSpawningAndTiming))]
    [HarmonyPrefix]
    public static bool StartGuestSpawningAndTiming_Prefix(EventManager __instance, ref int gameTotalSeconds)
    {
        if (GameSession.HasPeers)
        {
            gameTotalSeconds = GameFlow.WorkTimeSecondOverride;
            Log.InfoCaller($"gameTotalSeconds set to {gameTotalSeconds}s");
        }
        if (!GameSession.IsInRoom || GameFlow.Destination != DayDestination.Business) return RunOriginal;
        // 开场事件、装饰与伙伴初始化都已完成，放行前不启动刷客和计时。
        int duration = gameTotalSeconds;
        BusinessStart.Wait(() => StartGuestSpawningAndTiming_ReversePatch(__instance, duration));
        return SkipOriginal;
    }

    [HarmonyPatch(nameof(EventManager.StartGuestSpawningAndTiming))]
    [HarmonyReversePatch]
    private static void StartGuestSpawningAndTiming_ReversePatch(EventManager __instance, int gameTotalSeconds) { }

    /// <summary>
    /// 客机本地倒计时不能自行触发打烊，等待主机广播完整关闭路径。
    /// </summary>
    [HarmonyPatch(nameof(EventManager.ModifyTotalTime))]
    [HarmonyPrefix]
    public static bool ModifyTotalTime_Prefix(EventManager __instance, int time)
    {
        if (!GameSession.IsRoomClient || IsHostCloseReplay || time >= 0) return RunOriginal;

        var remaining = __instance.TotalCountDown + __instance.extraCountDown;
        return remaining + time <= 0 ? SkipOriginal : RunOriginal;
    }

    [HarmonyPatch(nameof(EventManager.StopInstantiationLoopAndCloseIzakaya))]
    [HarmonyReversePatch]
    public static void StopInstantiationLoopAndCloseIzakaya_ReversePatch(EventManager __instance) { }

    [HarmonyPatch(nameof(EventManager.FundEdit))]
    [HarmonyPrefix]
    public static bool FundEdit_Prefix()
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasPeers) return RunOriginal;
        if (GameSession.IsRoomHost) return RunOriginal;
        if (GameSession.IsRoomClient) return SkipOriginal;
        return RunOriginal;
    }

    [HarmonyPatch(nameof(EventManager.FundEdit))]
    [HarmonyReversePatch]
    public static void FundEdit_ReversePatch(
        EventManager __instance,
        float value,
        EventManager.MathOperation mathOperation = EventManager.MathOperation.Add)
    { }

    [HarmonyPatch(nameof(EventManager.FundEdit))]
    [HarmonyPostfix]
    public static void FundEdit_Postfix(float value, EventManager.MathOperation mathOperation)
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasPeers) return;
        if (GameSession.IsRoomHost)
        {
            FundEditAction.Send(value, mathOperation);
        }
    }

    [HarmonyPatch(nameof(EventManager.TipEdit))]
    [HarmonyPrefix]
    public static bool TipEdit_Prefix()
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasPeers) return RunOriginal;
        if (GameSession.IsRoomHost) return RunOriginal;
        if (GameSession.IsRoomClient) return SkipOriginal;
        return RunOriginal;
    }

    [HarmonyPatch(nameof(EventManager.TipEdit))]
    [HarmonyReversePatch]
    public static void TipEdit_ReversePatch(
        EventManager __instance,
        int value,
        EventManager.ServeType serveType,
        float comboBuff = 0.0f,
        float moodBuff = 0.0f,
        float extraBuff = 0.0f)
    { }

    [HarmonyPatch(nameof(EventManager.TipEdit))]
    [HarmonyPostfix]
    public static void TipEdit_Postfix(int value, EventManager.ServeType serveType, float comboBuff, float moodBuff, float extraBuff)
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasPeers) return;
        if (GameSession.IsRoomHost)
        {
            TipEditAction.Send(value, serveType, comboBuff, moodBuff, extraBuff);
        }
    }

    [HarmonyPatch(nameof(EventManager.ExpEdit))]
    [HarmonyPrefix]
    public static bool ExpEdit_Prefix()
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasPeers) return RunOriginal;
        if (GameSession.IsRoomHost) return RunOriginal;
        if (GameSession.IsRoomClient) return SkipOriginal;
        return RunOriginal;
    }

    [HarmonyPatch(nameof(EventManager.ExpEdit))]
    [HarmonyReversePatch]
    public static void ExpEdit_ReversePatch(
        EventManager __instance,
        float value = 1f,
        EventManager.MathOperation mathOperation = EventManager.MathOperation.Add)
    { }


    [HarmonyPatch(nameof(EventManager.ExpEdit))]
    [HarmonyPostfix]
    public static void ExpEdit_Postfix(float value, EventManager.MathOperation mathOperation)
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasPeers) return;
        if (GameSession.IsRoomHost)
        {
            ExpEditAction.Send(value, mathOperation);
        }
    }

    [HarmonyPatch(nameof(EventManager.PassionEdit))]
    [HarmonyPrefix]
    public static bool PassionEdit_Prefix()
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasPeers) return RunOriginal;
        if (GameSession.IsRoomHost) return RunOriginal;
        if (GameSession.IsRoomClient) return SkipOriginal;
        return RunOriginal;
    }

    [HarmonyPatch(nameof(EventManager.PassionEdit))]
    [HarmonyReversePatch]
    public static void PassionEdit_ReversePatch(
        EventManager __instance,
        float value,
        EventManager.MathOperation mathOperation = EventManager.MathOperation.Add)
    { }


    [HarmonyPatch(nameof(EventManager.PassionEdit))]
    [HarmonyPostfix]
    public static void PassionEdit_Postfix(float value, EventManager.MathOperation mathOperation)
    {
        if (GameFlow.ShouldSkipAction || !GameSession.HasPeers) return;
        if (GameSession.IsRoomHost)
        {
            PassionEditAction.Send(value, mathOperation);
        }
    }
}
