using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    static string FoodTags(Sellable s)
    {
        var parts = new List<string>();
        foreach (int t in s.Tags) parts.Add(t + "=" + t.GetFoodTag());
        return string.Join(",", parts);
    }

    static string BevTags(Sellable s)
    {
        var parts = new List<string>();
        foreach (int t in s.Tags) parts.Add(t + "=" + t.GetBeverageTag());
        return string.Join(",", parts);
    }

    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var r in RunTimeStorage.GetAllRecipes())
        {
            if (r.Id != 5012 && r.Id != 70 && r.Id != 35 && r.Id != 5 && r.Id != 56) continue;
            rows.Add("FOOD id=" + r.Id + " " + r.Food.Text.Name + " tags=[" + FoodTags(r.Food) + "]");
        }
        var bev = 22.AsNewBeverage();
        rows.Add("BEV id=22 " + bev.Text.Name + " tags=[" + BevTags(bev) + "]");
        return string.Join("\n", rows);
    }
}
