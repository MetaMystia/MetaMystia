using HarmonyLib;
using UnityEngine;

using Common.CharacterUtility;
using Common.UI;



using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(Common.CharacterUtility.CharacterControllerInputGeneratorComponent))]
public class CharacterControllerInputGeneratorComponentPatch
{
    [HarmonyPatch(nameof(CharacterControllerInputGeneratorComponent.UpdateInputDirection))]
    [HarmonyPrefix]
    public static void UpdateInputDirection_Prefix(CharacterControllerInputGeneratorComponent __instance, ref Vector2 inputDirection)
    {
        if (!GameSession.IsOnline)
        {
            return;
        }

        if (GameFlow.LocalScene != Scene.DayScene && GameFlow.LocalScene != Scene.WorkScene)
        {
            return;
        }

        try
        {
            var characterCollection = Common.SceneDirector.Instance.characterCollection;
            if (!characterCollection.ContainsKey("Self"))
            {
                Log.Warning($"characterCollection does not contain 'Self' key");
                return;
            }
            if (__instance.name == characterCollection["Self"].name)
            {
                PlayerManager.LocalInputDirection = inputDirection;
                PlayerProfile.SendMotion();
            }
        }
        catch (System.Exception e)
        {
            Log.Error($"Error in UpdateInputDirection_Prefix: {e.Message}");
        }
    }
}
