using GameData.Core.Collections;
using MetaMystia.Patch;

namespace MetaMystia.Network;

[NetActionBehavior]
internal static class NightCookBehavior
{
    public static void Send(int gridIndex, SellableFoodData food, int recipeId) =>
        new NightCookAction { GridIndex = gridIndex, RecipeId = recipeId, Food = food }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<NightCookAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true);
    }

    private static void Handle(NightCookAction action)
    {
        Plugin.Instance?.Log.LogInfo(
            $"Received COOK: CookerIndex={action.GridIndex}, FoodId={action.Food.Id}, Modifiers=[{string.Join(",", action.Food.ModifierIds)}]");
        if (!PlayerManager.RecipeAvailable(action.RecipeId))
        {
            Plugin.Instance?.Log.LogError($"RecipeId {action.RecipeId} not available!");
            return;
        }

        var recipe = action.RecipeId.RefRecipe();
        if (recipe == null)
        {
            Plugin.Instance?.Log.LogWarning("Failed to create recipe");
            return;
        }

        var food = action.Food.ToSellable();
        var cookerController = CookManager.GetCookerControllerByIndex(action.GridIndex);
        if (cookerController == null)
        {
            Plugin.Instance?.Log.LogWarning($"Failed to find CookerController with GridIndex={action.GridIndex}");
            return;
        }

        CookControllerPatch.SetCook_ReversePatch(cookerController, food, recipe, false);
    }
}
