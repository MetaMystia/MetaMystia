using HarmonyLib;

using GameData.Profile;
using NightScene.EventUtility;

using MetaMystia.ResourceEx.SpellCollection;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Profile.PartnerWaitressBehaviour))]
[AutoLog]
public static partial class PartnerWaitressBehaviourPatch
{
    // OnInitialize 中 Barmaid 的 executeServeCallback；Interop 的第二个 GuestTableDisplayer 回调。
    [HarmonyPatch(nameof(PartnerWaitressBehaviour._OnInitialize_b__14_6))]
    [HarmonyPrefix]
    public static void _OnInitialize_b__14_6_Prefix(PartnerWaitressBehaviour __instance, out EventManager __state)
        => __state = Spell_Minoriko.BeginFreeSakeServe(__instance.InspectBuffer());

    [HarmonyPatch(nameof(PartnerWaitressBehaviour._OnInitialize_b__14_6))]
    [HarmonyPostfix]
    public static void _OnInitialize_b__14_6_Postfix(EventManager __state) => __state?.registeredExtraBevCostModifier.Remove(-1f);
}
