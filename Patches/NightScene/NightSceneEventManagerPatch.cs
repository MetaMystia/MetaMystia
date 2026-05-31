using System;

using HarmonyLib;

using NightScene.EventUtility;

using MetaMystia.Network;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(EventManager))]
[AutoLog]
public static partial class NightSceneEventManagerPatch
{
    public static readonly PatchBypassToken HostCloseReplay = new();
    public static bool IsHostCloseReplay => HostCloseReplay.Pending > 0;

    [HarmonyPatch(nameof(EventManager.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix(EventManager __instance)
    {
        if (!MpManager.IsConnected) return;

        Func<int> getWholeNightTime = () => MpManager.WorkTimeSecondOverride;
        __instance.GetWholeNightTime = getWholeNightTime;
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
    public static void StartGuestSpawningAndTiming_Prefix(ref int gameTotalSeconds)
    {
        if (MpManager.IsConnected)
        {
            gameTotalSeconds = MpManager.WorkTimeSecondOverride;
            Log.InfoCaller($"gameTotalSeconds set to {gameTotalSeconds}s");
        }
    }

    /// <summary>
    /// 客机本地倒计时不能自行触发打烊，等待主机广播完整关闭路径。
    /// </summary>
    [HarmonyPatch(nameof(EventManager.ModifyTotalTime))]
    [HarmonyPrefix]
    public static bool ModifyTotalTime_Prefix(EventManager __instance, int time)
    {
        if (!MpManager.IsConnectedClient || IsHostCloseReplay || time >= 0) return RunOriginal;

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
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsConnectedHost) return RunOriginal;
        if (MpManager.IsConnectedClient) return SkipOriginal;
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
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsConnectedHost)
        {
            FundEditAction.Send(value, mathOperation);
        }
    }

    [HarmonyPatch(nameof(EventManager.TipEdit))]
    [HarmonyPrefix]
    public static bool TipEdit_Prefix()
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsConnectedHost) return RunOriginal;
        if (MpManager.IsConnectedClient) return SkipOriginal;
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
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsConnectedHost)
        {
            TipEditAction.Send(value, serveType, comboBuff, moodBuff, extraBuff);
        }
    }

    [HarmonyPatch(nameof(EventManager.ExpEdit))]
    [HarmonyPrefix]
    public static bool ExpEdit_Prefix()
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsConnectedHost) return RunOriginal;
        if (MpManager.IsConnectedClient) return SkipOriginal;
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
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsConnectedHost)
        {
            ExpEditAction.Send(value, mathOperation);
        }
    }

    [HarmonyPatch(nameof(EventManager.PassionEdit))]
    [HarmonyPrefix]
    public static bool PassionEdit_Prefix()
    {
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return RunOriginal;
        if (MpManager.IsConnectedHost) return RunOriginal;
        if (MpManager.IsConnectedClient) return SkipOriginal;
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
        if (MpManager.ShouldSkipAction || !MpManager.IsConnected) return;
        if (MpManager.IsConnectedHost)
        {
            PassionEditAction.Send(value, mathOperation);
        }
    }
}
