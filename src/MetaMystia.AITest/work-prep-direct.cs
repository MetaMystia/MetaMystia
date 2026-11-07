using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using NightScene.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        const int desk = 2;
        const int recipeId = 11000;
        const int bevId = 27;
        var gm = GuestsManager.Instance;
        var g = gm.GetInDeskGuest(desk);
        if (g == null || g.AllOrdersCount <= 0) return "no guest/order at desk " + desk;
        var order = g.PeekOrders();
        var so = order as GuestsManager.SpecialOrder;
        var rows = new System.Collections.Generic.List<string>();
        rows.Add("desk=" + desk + " orderType=" + order.Type + " cast=" + (so != null));
        var recipe = recipeId.RefRecipe();
        foreach (int id in recipe.Ingredients) RunTimeStorage.IngredientOut(id, false);
        var tray = IzakayaTray.Instance;
        tray.Receive(DataBaseCore.AsNewFood(recipe.FoodID));
        if (RunTimeStorage.GetBeverageCountById(bevId) <= 0) return "no beverage " + bevId;
        RunTimeStorage.BeverageOut(bevId, false);
        tray.Receive(bevId.AsNewBeverage());
        rows.Add("prepared food=" + recipe.Food.Text.Name + " bev=" + DataBaseCore.RefBeverage(bevId).Text.Name);
        gm.ExcuteEventAtCorodinate(desk);
        rows.Add("opened desk " + desk);
        return string.Join("\n", rows);
    }
}
