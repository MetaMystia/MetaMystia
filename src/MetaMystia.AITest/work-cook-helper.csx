using System.Collections.Generic;
using System.Text;

using GameData.Core.Collections;
using GameData.RunTime.Common;
using GameData.RunTime.NightSceneUtility;
using Il2CppInterop.Runtime;
using NightScene.CookingUtility;
using UnityEngine;

public static class AITestCook
{
    static CookController FindIdleByType(Recipe recipe)
    {
        foreach (var c in Object.FindObjectsOfType<CookController>(true))
            if (c.Phase == CookController.CookPhase.Idle && c.Cooker.Type == recipe.CookerType) return c;
        return null;
    }

    public static string Start(int recipeId)
    {
        var recipe = recipeId.RefRecipe();
        var cook = FindIdleByType(recipe);
        if (cook == null) return "No idle cooker for type " + recipe.CookerType;
        foreach (int id in recipe.Ingredients) RunTimeStorage.IngredientOut(id, false);
        var output = DataBaseCore.AsNewFood(recipe.FoodID);
        cook.SetCook(output, recipe, true);
        cook.StartCookCountDown(-1f);
        return "Started " + recipeId + " " + recipe.Food.Text.Name + " at " + cook.GridPosition;
    }

    public static string Status()
    {
        var rows = new List<string>();
        foreach (var c in Object.FindObjectsOfType<CookController>(true))
            rows.Add(c.GridPosition + " id=" + c.Cooker.Id + " type=" + c.Cooker.Type + " phase=" + c.Phase
                + (c.Result == null ? "" : " result=" + c.Result.Id + " " + c.Result.Text.Name));
        return string.Join("\n", rows);
    }

    public static string Extract()
    {
        CookController cook = null;
        foreach (var c in Object.FindObjectsOfType<CookController>(true))
            if (c.Phase == CookController.CookPhase.Finished) { cook = c; break; }
        if (cook == null) return "No finished cooker";
        var tray = IzakayaTray.Instance;
        if (tray.IsTrayFull) return "Tray full";
        var result = cook.Result;
        System.Action<Sellable> receive = value => tray.Receive(value);
        cook.Extract(DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sellable>>(receive));
        cook.Cooker.OnPlayerFinishExtract(cook);
        return "Extracted " + result.Id + " " + result.Text.Name + " trayFull=" + tray.IsTrayFull;
    }

    public static string Bev(int id)
    {
        if (RunTimeStorage.GetBeverageCountById(id) <= 0) return "No beverage " + id;
        var tray = IzakayaTray.Instance;
        if (tray.IsTrayFull) return "Tray full";
        var sell = id.AsNewBeverage();
        RunTimeStorage.BeverageOut(id, false);
        tray.Receive(sell);
        return "Beverage " + id + " " + sell.Text.Name + " trayFull=" + tray.IsTrayFull;
    }

    public static string Tray()
    {
        var tray = IzakayaTray.Instance;
        var rows = new List<string> { "trayMax=" + tray.TrayMaxNum + " full=" + tray.IsTrayFull };
        for (int i = 0; i < tray.TrayMaxNum; i++)
        {
            var s = tray.Tray[i];
            rows.Add(i + "=" + (s == null ? "empty" : s.Type + " " + s.Id + " " + s.Text.Name));
        }
        return string.Join("\n", rows);
    }
}

AITestCook.Tray()
