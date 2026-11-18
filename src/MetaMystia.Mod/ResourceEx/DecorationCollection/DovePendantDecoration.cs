using Il2CppInterop.Runtime.Attributes;

using NightScene.EventUtility;

namespace MetaMystia.ResourceEx.DecorationCollection;

/// <summary>小鸽子挂坠：白天移速提高一级；营业时玩家移速、伙伴工作效率与移速提高 20%。</summary>
public sealed class DovePendantDecoration : DecorationBaseEx, IDecorationDependencies
{
    private const float NightMultiplier = 1.2f;

    [HideFromIl2Cpp]
    public static string CheckDependencies() => null;

    [HideFromIl2Cpp]
    public override float DayMoveSpeedBonus => 0.12f;

    public override void DecorationBuffEnterNight(EventManager eventManager)
    {
        eventManager.registeredExtraMoveSpeedModifier.Add(NightMultiplier);
        eventManager.SetMystiaMoveSpeed();
        eventManager.SetPartnerExtraWorkSpeed(NightMultiplier, false);
        eventManager.SetPartnerExtraMoveSpeed(NightMultiplier, false);
    }
}
