using UnityEngine;

using GameData.RunTime.Common;
using PrepNightScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<IzakayaConfigPannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No preparation panel";
        var config = panel.m_IzakayaConfigure;
        int[] recipes = { 35, 7, 68, 10, 34, 61, 2016, 11000 };
        int[] beverages = { 22, 19, 13, 1001, 20, 17, 14, 27 };
        foreach (int id in recipes)
            if (!RunTimeStorage.HaveRecipe(id)) return $"Missing recipe {id}";
        foreach (int id in beverages)
            if (RunTimeStorage.GetBeverageCountById(id) == 0) return $"Missing beverage {id}";
        for (int i = config.DailyRecipes.Count - 1; i >= 0; i--)
            config.LogoffFromDailyRecipes(config.DailyRecipes[i].Id);
        for (int i = config.DailyBeverages.Count - 1; i >= 0; i--)
            config.LogoffFromDailyBeverages(config.DailyBeverages[i].Id);
        foreach (int id in recipes) config.RegisterToDailyRecipes(id, true);
        foreach (int id in beverages) config.RegisterToDailyBeverages(id, true);
        panel.GoToSpecific(IzakayaConfigPannel.CurrentConfigType.Recipe);
        panel.SolveDailyCompletion();
        return "Meirin+Daiyousei menu applied; verify via prep-state before opening";
    }
}
