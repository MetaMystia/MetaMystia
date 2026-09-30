using HarmonyLib;

using GameData.Profile;
using NightScene.EventUtility;

using MetaMystia.ResourceEx.SpellCollection;

using ThrowDeliver = GameData.Profile.PartnerWaitressBehaviour._ExecuteThrowDeliver_d__41;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.Profile.PartnerWaitressBehaviour._ExecuteThrowDeliver_d__41))]
[AutoLog]
public static partial class PartnerWaitressThrowDeliverPatch
{
    // ExecuteThrowDeliver 在飞行 yield 之后返还酒水；作用域只覆盖本次 MoveNext，不跨帧。
    [HarmonyPatch(nameof(ThrowDeliver.MoveNext))]
    [HarmonyPrefix]
    public static void MoveNext_Prefix(ThrowDeliver __instance, out EventManager __state)
    {
        __state = null;
        var partner = __instance.__4__this;
        if (partner.waitressType != PartnerWaitressBehaviour.WaitressType.Barmaid)
            return;
        var beverage = partner.FocusingOrder?.ServedBeverageInAir ?? partner.InspectBuffer();
        __state = Spell_Minoriko.BeginFreeSakeServe(beverage);
    }

    [HarmonyPatch(nameof(ThrowDeliver.MoveNext))]
    [HarmonyPostfix]
    public static void MoveNext_Postfix(EventManager __state) => __state?.registeredExtraBevCostModifier.Remove(-1f);
}
