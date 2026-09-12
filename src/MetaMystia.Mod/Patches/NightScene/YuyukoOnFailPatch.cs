using HarmonyLib;

using FailureLoop = GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObObObUnique;

namespace MetaMystia.Patch;

// 原版 <<MainChallengeLoop>g__OnFail|4>d：失败剧情结束后自行调用 CloseIzakayaDelayed(Challenge)。
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObObObUnique))]
[AutoLog]
public partial class YuyukoOnFailPatch
{
    [HarmonyPatch(nameof(FailureLoop.MoveNext))]
    [HarmonyPrefix]
    public static void MoveNext_Prefix(FailureLoop __instance)
    {
        if (__instance.__1__state == 0) YuyukoBossDataPatch.OnFailureStarted();
    }
}
