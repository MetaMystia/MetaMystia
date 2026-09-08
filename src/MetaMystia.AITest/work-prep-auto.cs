using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using Il2CppInterop.Runtime;
using NightScene.CookingUtility;
using NightScene.GuestManagementUtility;
using UnityEngine;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        GuestsManager.SpecialOrder order = null;
        foreach (int code in new[] { 0, 1, 2, 3, 4 })
        {
            var g = GuestsManager.Instance.GetInDeskGuest(code);
            if (g == null || !(g is SpecialGuestsController sp) || sp.SpecialGuest.Id != 7) continue;
            order = g.PeekOrders() as GuestsManager.SpecialOrder;
            if (order != null) { rows.Add("desk=" + code); break; }
        }
        if (order == null) return "Reimu order not found";
        string foodTag = DataBaseLanguage.GetFoodTag(order.RequestFoodTag);
        string bevTag = DataBaseLanguage.GetBeverageTag(order.RequestBeverageTag);
        rows.Add("order=" + foodTag + " + " + bevTag);

        int recipeId = foodTag == "高级" || foodTag == "甜" || foodTag == "不可思议" ? 5012 : foodTag == "实惠" || foodTag == "饱腹" ? 35 : 0;
        int bevId = bevTag == "无酒精" ? 22 : bevTag == "低酒精" || bevTag == "可加热" ? 19 : 0;
        if (recipeId == 0 || bevId == 0) return "Unsupported tags: " + foodTag + " + " + bevTag;
        var recipe = recipeId.RefRecipe();
        CookController cook = null;
        var preferredPos = new Vector3Int(1, 3, 0);
        foreach (var c in Object.FindObjectsOfType<CookController>(true))
            if (c.Phase == CookController.CookPhase.Idle && c.Cooker.Type == recipe.CookerType && c.GridPosition == preferredPos)
            { cook = c; break; }
        if (cook == null)
            foreach (var c in Object.FindObjectsOfType<CookController>(true))
                if (c.Phase == CookController.CookPhase.Idle && c.Cooker.Type == recipe.CookerType)
                { cook = c; break; }
        if (cook == null) return "No idle cooker for " + recipe.CookerType;
        foreach (int id in recipe.Ingredients) RunTimeStorage.IngredientOut(id, false);
        var output = DataBaseCore.AsNewFood(recipe.FoodID);
        cook.SetCook(output, recipe, true);
        cook.StartCookCountDown(-1f);
        if (cook.Phase != CookController.CookPhase.Finished)
            return "Cook did not finish immediately; phase=" + cook.Phase;
        var tray = IzakayaTray.Instance;
        var result = cook.Result;
        System.Action<Sellable> receive = value => tray.Receive(value);
        cook.Extract(DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sellable>>(receive));
        cook.Cooker.OnPlayerFinishExtract(cook);
        rows.Add("cooked id=" + result.Id + " " + result.Text.Name);
        if (RunTimeStorage.GetBeverageCountById(bevId) <= 0) return "No beverage id " + bevId;
        var sell = bevId.AsNewBeverage();
        RunTimeStorage.BeverageOut(bevId, false);
        tray.Receive(sell);
        rows.Add("bev id=" + bevId + " " + sell.Text.Name);
        return string.Join("\n", rows);
    }
}
