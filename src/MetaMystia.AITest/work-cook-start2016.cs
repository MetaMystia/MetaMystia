using GameData.Core.Collections;
using GameData.RunTime.Common;
using NightScene.CookingUtility;
using UnityEngine;

public static class Payload
{
    public static object Execute()
    {
        const int recipeId = 2016;
        var pos = new Vector3Int(3, 3, 0);
        CookController cook = null;
        foreach (var c in Object.FindObjectsOfType<CookController>(true))
            if (c.GridPosition == pos) { cook = c; break; }
        if (cook == null || cook.Phase != CookController.CookPhase.Idle)
            return "Cooker not idle; phase=" + (cook == null ? "none" : cook.Phase.ToString());
        var recipe = recipeId.RefRecipe();
        if (cook.Cooker.Type != recipe.CookerType)
            return "Cooker type mismatch: " + cook.Cooker.Type + " vs " + recipe.CookerType;
        foreach (int id in recipe.Ingredients) RunTimeStorage.IngredientOut(id, false);
        var output = DataBaseCore.AsNewFood(recipe.FoodID);
        cook.SetCook(output, recipe, true);
        cook.StartCookCountDown(-1f);
        return "Started cook id=" + recipeId + " " + recipe.Food.Text.Name + " at " + pos + "; wait phase=Finished";
    }
}
