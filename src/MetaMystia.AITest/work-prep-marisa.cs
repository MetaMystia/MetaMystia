using GameData.Core.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        const int desk = 0;
        const int recipeId = 32;
        const int bevId = 27;
        var gm = GuestsManager.Instance;
        var g = gm.GetInDeskGuest(desk);
        if (g == null || g.AllOrdersCount <= 0) return "no guest/order at desk " + desk;
        var recipe = recipeId.RefRecipe();
        foreach (int id in recipe.Ingredients) RunTimeStorage.IngredientOut(id, false);
        var tray = IzakayaTray.Instance;
        tray.Receive(DataBaseCore.AsNewFood(recipe.FoodID));
        if (RunTimeStorage.GetBeverageCountById(bevId) <= 0) return "no beverage " + bevId;
        RunTimeStorage.BeverageOut(bevId, false);
        tray.Receive(bevId.AsNewBeverage());
        gm.ExcuteEventAtCorodinate(desk);
        return "prepared food=" + recipe.Food.Text.Name + " bev=27 at desk " + desk;
    }
}
