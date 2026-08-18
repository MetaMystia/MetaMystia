using HarmonyLib;

using GameData.RunTime.Common;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeScheduler))]
[AutoLog]
[TracePatch("Method_Internal_Static_Void_Action_PDM_0", DisplayName = "ReimuProtection")]
public partial class RunTimeSchedulerPatch
{
    public static readonly PatchBypassToken DuringReimuProtection = new();
    public static bool IsDuringReimuProtection => DuringReimuProtection.Pending > 0;

    // <AddReimuPositiveSpellToWorkScene>g__ReimuProtection|160_0(Action onFinish)
    // VA = 0x18064F250 in Release 4.3.0c
    [HarmonyPatch(nameof(RunTimeScheduler.Method_Internal_Static_Void_Action_PDM_0))]
    [HarmonyPrefix]
    public static void ReimuProtection_Prefix()
    {
        Log.Info("ReimuProtection prefix called.");
        DuringReimuProtection.Grant();
    }

    [HarmonyPatch(nameof(RunTimeScheduler.Method_Internal_Static_Void_Action_PDM_0))]
    [HarmonyPostfix]
    public static void ReimuProtection_Postfix()
    {
        Log.Info("ReimuProtection postfix called.");
        DuringReimuProtection.Reset();
    }
}
