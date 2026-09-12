using HarmonyLib;

using MetaMystia.UI;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0))]
[AutoLog]
public partial class YuyukoChallengeContextPatch
{
    // MainChallengeLoop 的 Timing_2（VA 0x18078CA80），三个阶段共用的计时协程入口。
    [HarmonyPatch(nameof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.Method_Internal_IEnumerator_Func_1_Boolean_0))]
    [HarmonyPrefix]
    public static void Method_Internal_IEnumerator_Func_1_Boolean_0_Prefix(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0 __instance)
    {
        if (!MpManager.IsConnected) return;

        if (PrepSceneManager.YuyukoPrepRound == 3)
            InGameConsole.ShowPassive(TextId.YuyukoPhase3PatientExtended.Get());

        int originalDuration = __instance.__4__this.singleRoundDuration;
#if DEBUG
        __instance.thisSingleRoundDuration = originalDuration * 64;
#else
        __instance.thisSingleRoundDuration = originalDuration * 2;
#endif
        Log.Info($"幽幽子试炼本阶段时长：{originalDuration}s → {__instance.thisSingleRoundDuration}s");
    }
}
