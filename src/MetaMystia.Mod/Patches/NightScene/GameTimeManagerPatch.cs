using HarmonyLib;

using Common.TimelineExtestion;

using MetaMystia.Multiplayer;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(Common.TimelineExtestion.GameTimeManager))]
public class GameTimeManagerPatch
{
    /// <summary>
    /// 面板、QTE 与符卡对话通过此入口冻结或恢复 timeScale。
    /// 普通营业联机时只记录本地意向，由房主统一裁定；最终试炼暂不时停。
    /// 单机开启 NoPanelPause 时除 ESC 外不时停。
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
        if (GameFlow.LocalScene == Common.UI.Scene.WorkScene && (!GameFlow.ShouldSkipAction || NoPanelPause && !OpeningEsc))
        {
            mode = GameTimeManager.TimeMode.Resume;
        }
        Log.Debug($"SetGameTimeMode: time mode changed to {mode}");
        return RunOriginal;
    }

    [HarmonyPatch(nameof(GameTimeManager.SetGameTimeMode))]
    [HarmonyReversePatch]
    private static void SetGameTimeMode_ReversePatch(GameTimeManager __instance, GameTimeManager.TimeMode mode) { }

    public static bool NoPanelPause => ConfigManager.NoPanelPause.Value && !GameSession.HasRoomPeers && !GameFlow.InStory;

    /// <summary>ESC 与其他面板共用冻结入口，入口在冻结前已记下当前面板。</summary>
    private static bool OpeningEsc => NightScene.UI.UIManager.Instance?.SustainedPannel?.m_CurrentPanelParent is { } parent
        && parent.Pointer == Common.UI.UniversalGameManager.GetEscapePanelParent()?.Pointer;

    public static bool IsFrozen => GameTimeManager.Instance is { } instance
        && instance.CurrentTimeMode != GameTimeManager.TimeMode.Resume;

    public static void Apply(bool freeze)
    {
        if (GameTimeManager.Instance is not { } instance) return;
        SetGameTimeMode_ReversePatch(instance, freeze ? GameTimeManager.TimeMode.Freeze : GameTimeManager.TimeMode.Resume);
        Log.Info($"room time {(freeze ? "frozen" : "resumed")}");
    }
}
