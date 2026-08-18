using HarmonyLib;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(Common.TimelineExtestion.GameTimeManager))]
[AutoLog]
public partial class GameTimeManagerPatch
{
    [HarmonyPatch(nameof(Common.TimelineExtestion.GameTimeManager.SetGameTimeMode))]
    [HarmonyPrefix]
    public static void SetGameTimeMode_Prefix(Common.TimelineExtestion.GameTimeManager __instance, ref Common.TimelineExtestion.GameTimeManager.TimeMode mode)
    {
        if (MpManager.LocalScene == Common.UI.Scene.WorkScene && !MpManager.ShouldSkipAction)
        {
            mode = Common.TimelineExtestion.GameTimeManager.TimeMode.Resume;
        }
        Log.DebugCaller($"time mode changed to {mode}");
    }

    public static void FreezeTime() => Common.TimelineExtestion.GameTimeManager.Instance?.SetGameTimeMode(Common.TimelineExtestion.GameTimeManager.TimeMode.Freeze);
    public static void ResumeTime() => Common.TimelineExtestion.GameTimeManager.Instance?.SetGameTimeMode(Common.TimelineExtestion.GameTimeManager.TimeMode.Resume);
}
