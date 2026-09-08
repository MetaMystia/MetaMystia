using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    static string FoodTags(IEnumerable<int> tags)
    {
        var parts = new List<string>();
        foreach (int t in tags)
        {
            var n = t.GetFoodTag();
            if (n != null) parts.Add(n);
        }
        return string.Join(",", parts);
    }

    static string BevTags(IEnumerable<int> tags)
    {
        var parts = new List<string>();
        foreach (int t in tags)
        {
            var n = t.GetBeverageTag();
            if (n != null) parts.Add(n);
        }
        return string.Join(",", parts);
    }

    public static object Execute()
    {
        var rows = new List<string>();
        rows.Add("--food 清淡 candidates--");
        foreach (var r in RunTimeStorage.GetAllRecipes())
            foreach (int t in r.Food.Tags)
                if (t.GetFoodTag() == "清淡") { rows.Add("FOOD id=" + r.Id + " food=" + r.Food.Id + " " + r.Food.Text.Name + " tags=" + FoodTags(r.Food.Tags)); break; }
        rows.Add("--beverage 直饮 candidates--");
        foreach (var pair in RunTimeStorage.GetAllBeverages())
            foreach (int t in pair.Key.Tags)
                if (t.GetBeverageTag() == "直饮") { rows.Add("BEV id=" + pair.Key.Id + " " + pair.Key.Text.Name + " tags=" + BevTags(pair.Key.Tags)); break; }
        return rows.Count == 0 ? "none" : string.Join("\n", rows);
    }
}
