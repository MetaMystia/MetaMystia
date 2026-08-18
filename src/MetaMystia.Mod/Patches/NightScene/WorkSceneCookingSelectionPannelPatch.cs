using System.Linq;
using HarmonyLib;

using GameData.CoreLanguage.Collections;
using NightScene.UI.CookingUtility;

using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.UI.CookingUtility.WorkSceneCookingSelectionPannel.__c__DisplayClass79_0))]
[AutoLog]
public partial class WorkSceneCookingSelectionPannel__c__DisplayClass79_0Patch
{

    // NightScene.UI.CookingUtility.WorkSceneCookingSelectionPannel.__c__DisplayClass79_0$$_OnOutputSelected_g__OnSubmit_1
    // VA = 0x18057E710 in Release 4.3.1
    // `OnSubmit` is setting in `WorkSceneCookingSelectionPannel.OnOutputSelected` and called when the player clicks the button in the cooking selection panel
    [HarmonyPatch(nameof(WorkSceneCookingSelectionPannel.__c__DisplayClass79_0.Method_Internal_Void_PDM_0))]
    [HarmonyPrefix]
    public static bool Method_Internal_Void_PDM_0_Prefix(WorkSceneCookingSelectionPannel.__c__DisplayClass79_0 __instance)
    {
        if (!MpManager.IsConnected)
            return RunOriginal;

        var solved = __instance.solved;

        if (solved?.Recipe == null)
            return RunOriginal;

        if (!PlayerManager.RecipeAvailable(solved.Recipe.Id))
        {
            Log.Warning($"Peer does not have recipe {solved.Recipe.Id}, blocking OnSubmit");
            InGameConsole.ShowPassive(TextId.DLCPeerRecipeNotAvailable.Get(solved.Recipe.Id));
            return SkipOriginal;
        }

        if (solved?.Modifiers == null)
            return RunOriginal;

        var unavailable = solved.Modifiers.Where(id => !PlayerManager.IngredientAvailable(id));

        if (unavailable.Any())
        {
            var ingredientNames = unavailable.Select(id => $"{DataBaseLanguage.Ingredients[id]?.Name ?? "Unknown"}({id})");
            var ingredientList = string.Join(", ", ingredientNames);
            Log.Warning($"Peer does not have modifier ingredient {ingredientList}, blocking OnSubmit");
            InGameConsole.ShowPassive(TextId.DLCPeerIngredientNotAvailable.Get(ingredientList));
            return SkipOriginal;
        }

        return RunOriginal;
    }
}
