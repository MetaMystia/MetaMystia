using System;
using HarmonyLib;

using GameData.Core.Collections;
using NightScene.CookingUtility;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
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
        if (YuyukoGuestSync.IsSwallowedCooker(__instance.GridIndex)) return SkipOriginal;
        // Log.Debug($"SetCook_Prefix called");
        if (GameSession.HasPeers && (!PlayerManager.RecipeAvailable(recipe.Id) || !PlayerManager.FoodAvailable(thisResult.id)))
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
        if (YuyukoGuestSync.IsSwallowedCooker(__instance.GridIndex)) return;
        if (GameFlow.ShouldSkipAction) return;
        var gridIndex = __instance.GridIndex;
        var recipeId = recipe.Id;
        SellableFood food = SellableFood.FromSellable(thisResult);
        NightCookAction.Send(gridIndex, food, recipeId);
    }

    [HarmonyPatch(nameof(CookController.Extract))]
    [HarmonyReversePatch]
    public static void Extract_ReversePatch(CookController __instance, Il2CppSystem.Action<Sellable> targetAssignmentCallBack)
    { }

    [HarmonyPatch(nameof(CookController.Extract))]
    [HarmonyPrefix]
    public static void Extract_Prefix(CookController __instance)
    {
        // 吞食消息已让两端各执行一次原版中断，不能再把其内部 Extract 当作玩家取菜广播。
        if (YuyukoGuestSync.IsInterruptingCooker) return;
        if (GameFlow.ShouldSkipAction) return;
        var gridIndex = __instance.GridIndex;
        ExtractFromCookerAction.Send(gridIndex);
    }

    [HarmonyPatch(nameof(CookController.Store))]
    [HarmonyReversePatch]
    public static void Store_ReversePatch(CookController __instance, Sellable value)
    { }

    [HarmonyPatch(nameof(CookController.Store))]
    [HarmonyPrefix]
    public static void Store_Prefix(CookController __instance, Sellable value)
    {
        if (GameFlow.ShouldSkipAction) return;
        var gridIndex = __instance.GridIndex;
        StoreSellableAction.Send(gridIndex, value);
    }


    [HarmonyPatch(nameof(CookController.StartCookCountDown))]
    [HarmonyReversePatch]
    public static void StartCookCountDown_ReversePatch(CookController __instance, float qteScore, bool allowInterrupt = false)
    { }

    [HarmonyPatch(nameof(CookController.StartCookCountDown))]
    [HarmonyPrefix]
    public static bool StartCookCountDown_Prefix(CookController __instance, float qteScore)
    {
        // 联机下 QTE 期间时间继续推进，厨具可能已被吞食，需阻止 QTE 结束后再启动烹饪倒计时。
        if (YuyukoGuestSync.IsSwallowedCooker(__instance.GridIndex)) return SkipOriginal;
        var gridIndex = __instance.GridIndex;
        QTEAction.Send(gridIndex, qteScore);
        return RunOriginal;
    }

}
