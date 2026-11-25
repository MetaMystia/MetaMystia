using System;
using HarmonyLib;

using NightScene.EventUtility;

using MetaMystia.Multiplayer.Spells;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.EventUtility.EventManager.ScheduledFunction))]
[AutoLog]
public static partial class EventManager__ScheduledFunctionPatch
{
    /// <summary>限时 buff 的计时。客机符卡晚于主机执行时扣除迟到的秒数，与主机同时结束。</summary>
    [HarmonyPatch(MethodType.Constructor, [typeof(int), typeof(Il2CppSystem.Action), typeof(Il2CppSystem.Action<int>), typeof(EventManager)])]
    [HarmonyPrefix]
    public static void ScheduledFunction_Constructor_Prefix(ref int durationSec)
    {
        if (durationSec > 0 && SpellRun.Current is { Lag: > 0 } run) durationSec = Math.Max(1, durationSec - run.Lag);
    }
}
