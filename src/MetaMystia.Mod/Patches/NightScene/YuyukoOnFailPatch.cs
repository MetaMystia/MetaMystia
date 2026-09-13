#if !TMI_RELEASE_4_4_0E
#error 请核对本文件依赖的游戏协程、状态机及编译器生成成员，完成版本适配后再更新此标记。
#endif

using HarmonyLib;

namespace MetaMystia.Patch;

// 原版 <<MainChallengeLoop>g__OnFail|4>d：失败剧情结束后自行调用 CloseIzakayaDelayed(Challenge)。
[HarmonyPatch(typeof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObObObUnique))]
[AutoLog]
public partial class YuyukoOnFailPatch
{
    [HarmonyPatch(nameof(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObObObUnique.MoveNext))]
    [HarmonyPrefix]
    public static void MoveNext_Prefix(GameData.Profile.YuyukoBossData.__c__DisplayClass16_0.ObjectCompilerGeneratedNPrivateSealedIEnumerator1ObjectIEnumeratorIDisposableInObObObUnique __instance)
    {
        if (__instance.__1__state == 0) YuyukoBossDataPatch.OnFailureStarted();
    }
}
