using Il2CppInterop.Runtime.Attributes;

using Common.CharacterUtility;
using NightScene.EventUtility;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.DecorationCollection;

public class DovePendantDecoration : DecorationBaseEx, IDecorationDependencies
{
    private const float DaySpeedBonus = 0.12f;
    private const float NightSpeedMultiplier = 1.2f;

    [HideFromIl2Cpp]
    public static string CheckDependencies(DecorationConfig config) => null;

    [HideFromIl2Cpp]
    public override void OnDayEquip(CharacterControllerUnit player) => player.MoveSpeedMultiplier += DaySpeedBonus;

    [HideFromIl2Cpp]
    public override void OnDayUnequip(CharacterControllerUnit player) => player.MoveSpeedMultiplier -= DaySpeedBonus;

    public override void DecorationBuffEnterNight(EventManager eventManager)
    {
        eventManager.registeredExtraMoveSpeedModifier.Add(NightSpeedMultiplier);
        eventManager.SetMystiaMoveSpeed();
        eventManager.SetPartnerExtraWorkSpeed(NightSpeedMultiplier, false);
        eventManager.SetPartnerExtraMoveSpeed(NightSpeedMultiplier, false);
    }
}
