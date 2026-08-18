using HarmonyLib;
using UnityEngine;

using Common.CharacterUtility;
using Common.UI;

using MetaMystia.Network;


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

        try
        {
            var characterCollection = Common.SceneDirector.Instance.characterCollection;
            if (!characterCollection.ContainsKey("Self"))
            {
                Log.LogWarning($"characterCollection does not contain 'Self' key");
                return;
            }
            if (__instance.name == characterCollection["Self"].name)
            {
                PlayerManager.LocalInputDirection = inputDirection;
                MoveSyncAction.Send();
            }
        }
        catch (System.Exception e)
        {
            Log.LogError($"Error in UpdateInputDirection_Prefix: {e.Message}");
        }
    }
}
