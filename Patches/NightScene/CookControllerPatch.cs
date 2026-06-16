using System;
using HarmonyLib;

using GameData.Core.Collections;
using NightScene.CookingUtility;

using MetaMystia.Network.Services;
using MetaMystia.UI;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NightScene.CookingUtility.CookController))]
[AutoLog]
public partial class CookControllerPatch
{

    [HarmonyPatch(nameof(CookController.SetCook))]
    [HarmonyPrefix]
    public static bool SetCook_Prefix(CookController __instance, Sellable thisResult, Recipe recipe, bool thisCouldReturnIngredients)
    {
        // Log.Debug($"SetCook_Prefix called");
        if (MpManager.IsConnected && (!PlayerManager.RecipeAvailable(recipe.Id) || !PlayerManager.FoodAvailable(thisResult.id)))
        {
            Log.LogWarning($"Peer does not have recipe {recipe.Id}, skipping SetCook.");
            InGameConsole.ShowPassive(TextId.DLCPeerRecipeNotAvailable.Get(recipe.Id));
            return SkipOriginal;
        }
        return RunOriginal;
    }


    [HarmonyPatch(nameof(CookController.SetCook))]
    [HarmonyReversePatch]
    public static void SetCook_ReversePatch(CookController __instance, Sellable thisResult, Recipe recipe, bool thisCouldReturnIngredients)
    { }

    [HarmonyPatch(nameof(CookController.SetCook))]
    [HarmonyPostfix]
    public static void SetCook_Postfix(CookController __instance, Sellable thisResult, Recipe recipe, bool thisCouldReturnIngredients)
    {
        if (MpManager.ShouldSkipAction) return;
        var gridIndex = __instance.GridIndex;
        var recipeId = recipe.Id;
        var food = thisResult.ToSellableFoodData();
        WorkSceneServices.SendNightCook(gridIndex, food, recipeId);
    }

    [HarmonyPatch(nameof(CookController.Extract))]
    [HarmonyReversePatch]
    public static void Extract_ReversePatch(CookController __instance, Il2CppSystem.Action<Sellable> targetAssignmentCallBack)
    { }

    [HarmonyPatch(nameof(CookController.Extract))]
    [HarmonyPrefix]
    public static void Extract_Prefix(CookController __instance)
    {
        if (MpManager.ShouldSkipAction) return;
        var gridIndex = __instance.GridIndex;
        WorkSceneServices.SendExtractFromCooker(gridIndex);
    }

    [HarmonyPatch(nameof(CookController.Store))]
    [HarmonyReversePatch]
    public static void Store_ReversePatch(CookController __instance, Sellable value)
    { }

    [HarmonyPatch(nameof(CookController.Store))]
    [HarmonyPrefix]
    public static void Store_Prefix(CookController __instance, Sellable value)
    {
        if (MpManager.ShouldSkipAction) return;
        var gridIndex = __instance.GridIndex;
        WorkSceneServices.SendStoreSellable(gridIndex, value);
    }


    [HarmonyPatch(nameof(CookController.StartCookCountDown))]
    [HarmonyReversePatch]
    public static void StartCookCountDown_ReversePatch(CookController __instance, float qteScore, bool allowInterrupt = false)
    { }

    [HarmonyPatch(nameof(CookController.StartCookCountDown))]
    [HarmonyPrefix]
    public static void StartCookCountDown_Prefix(CookController __instance, float qteScore)
    {
        var gridIndex = __instance.GridIndex;
        WorkSceneServices.SendQTE(gridIndex, qteScore);
    }

}
