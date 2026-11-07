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
        rows.Add("foodTag34=" + 34.GetFoodTag() + " bevTag13=" + 13.GetBeverageTag());
        foreach (var r in RunTimeStorage.GetAllRecipes())
            foreach (int t in r.Food.Tags)
                if (t == 34)
                {
                    rows.Add("FOOD id=" + r.Id + " " + r.Food.Text.Name + " lv=" + r.Food.Level
                        + " val=" + r.Food.TrueValue + " count=" + r.CookCount + " cooker=" + r.CookerType
                        + " tags=[" + FoodTags(r.Food.Tags) + "]");
                    break;
                }
        foreach (var pair in RunTimeStorage.GetAllBeverages())
            foreach (int t in pair.Key.Tags)
                if (t == 13)
                {
                    rows.Add("BEV id=" + pair.Key.Id + " " + pair.Key.Text.Name + " val=" + pair.Key.TrueValue
                        + " count=" + pair.Value + " tags=[" + BevTags(pair.Key.Tags) + "]");
                    break;
                }
        return string.Join("\n", rows);
    }
}
