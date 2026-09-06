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
        int[] recipes = { 2008, 19, 11000 };
        int[] beverages = { 20, 12 };
        foreach (int id in recipes)
            if (!RunTimeStorage.HaveRecipe(id)) return $"Missing recipe {id}";
        foreach (int id in beverages)
            if (RunTimeStorage.GetBeverageCountById(id) == 0) return $"Missing beverage {id}";
        if (RunTimeStorage.GetCookerCountById(5000) == 0 || RunTimeStorage.GetCookerCountById(15) == 0) return "Missing cooker";
        if (config.CookerConfigure.Length != 3 || config.CookerConfigure[0] != 19 || config.CookerConfigure[1] != 17) return "Cooker layout changed; inspect first";
        for (int i = config.DailyRecipes.Count - 1; i >= 0; i--)
            config.LogoffFromDailyRecipes(config.DailyRecipes[i].Id);
        for (int i = config.DailyBeverages.Count - 1; i >= 0; i--)
            config.LogoffFromDailyBeverages(config.DailyBeverages[i].Id);
        foreach (int id in recipes) config.RegisterToDailyRecipes(id, true);
        foreach (int id in beverages) config.RegisterToDailyBeverages(id, true);
        config.RegisterToCookers(5000, 0, true);
        config.RegisterToCookers(15, 1, true);
        panel.GoToSpecific(IzakayaConfigPannel.CurrentConfigType.Recipe);
        panel.SolveDailyCompletion();
        return "Menu applied through preparation callbacks; verify before opening";
    }
}
