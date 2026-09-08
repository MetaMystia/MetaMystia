using HarmonyLib;
using UnityEngine;

using Common.CharacterUtility;
using Common.UI;


namespace MetaMystia.Patch;

[HarmonyPatch(typeof(Common.CharacterUtility.CharacterControllerInputGeneratorComponent))]
[AutoLog]
public partial class CharacterControllerInputGeneratorComponentPatch
{
    [HarmonyPatch(nameof(CharacterControllerInputGeneratorComponent.UpdateInputDirection))]
    [HarmonyPrefix]
    public static void UpdateInputDirection_Prefix(CharacterControllerInputGeneratorComponent __instance, ref Vector2 inputDirection)
    {
        if (!MpManager.CanSeeOnlinePlayers)
        {
            return;
        }

        if (MpManager.LocalScene != Scene.DayScene && MpManager.LocalScene != Scene.WorkScene)
        {
            return;
        }

        var self = PlayerManager.Local.unit;
        if (self != null && __instance.Character == self)
        {
            PlayerManager.LocalInputDirection = inputDirection;
        }
    }
}
