using HarmonyLib;

using GameData.RunTime.Common;

using MetaMystia.ResourceEx.Registries;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimePlayerData))]
[AutoLog]
public partial class RunTimePlayerDataPatch
{
    // 原版在此将白天移速重置为等级值。
    [HarmonyPatch(nameof(RunTimePlayerData.MountAndOverridePlayerRuntimeData))]
    [HarmonyPostfix]
    public static void MountAndOverridePlayerRuntimeData_Postfix() => DecorationRegistry.ApplyDayEffects();
}
