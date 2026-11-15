using HarmonyLib;

using Common.TimelineExtestion;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(Common.TimelineExtestion.GameTimeManager))]
[AutoLog]
public partial class GameTimeManagerPatch
{
    /// <summary>
    /// 面板、QTE 与符卡对话通过此入口冻结或恢复 timeScale。
    /// 普通营业联机时只记录本地意向，由房主统一裁定；最终试炼暂不时停。
    /// </summary>
    [HarmonyPatch(nameof(GameTimeManager.SetGameTimeMode))]
    [HarmonyPrefix]
    public static bool SetGameTimeMode_Prefix(ref GameTimeManager.TimeMode mode)
    {
        if (RoomTimeStop.Intercepts)
        {
            RoomTimeStop.SetLocalDemand(mode != GameTimeManager.TimeMode.Resume);
            return SkipOriginal;
        }
        if (GameFlow.LocalScene == Common.UI.Scene.WorkScene && !GameFlow.ShouldSkipAction)
        {
            mode = GameTimeManager.TimeMode.Resume;
        }
        Log.DebugCaller($"time mode changed to {mode}");
        return RunOriginal;
    }

    [HarmonyPatch(nameof(GameTimeManager.SetGameTimeMode))]
    [HarmonyReversePatch]
    private static void SetGameTimeMode_ReversePatch(GameTimeManager __instance, GameTimeManager.TimeMode mode) { }

    public static bool IsFrozen => GameTimeManager.Instance is { } instance
        && instance.CurrentTimeMode != GameTimeManager.TimeMode.Resume;

    public static void Apply(bool freeze)
    {
        if (GameTimeManager.Instance is not { } instance) return;
        SetGameTimeMode_ReversePatch(instance, freeze ? GameTimeManager.TimeMode.Freeze : GameTimeManager.TimeMode.Resume);
        Log.Info($"room time {(freeze ? "frozen" : "resumed")}");
    }
}
