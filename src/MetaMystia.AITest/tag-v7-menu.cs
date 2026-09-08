using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    static readonly int[] Ids = { 35, 7, 68, 10, 34, 61, 2016, 11000, 2008, 1002, 3004 };

    static string Tags(IEnumerable<int> tags, bool food)
    {
        var parts = new List<string>();
        foreach (int t in tags)
        {
            var n = food ? t.GetFoodTag() : t.GetBeverageTag();
            if (n != null) parts.Add(n);
        }
        return string.Join(",", parts);
    }

    public static object Execute()
    {
        var rows = new List<string>();
        foreach (int id in Ids)
        {
            var r = id.RefRecipe();
            rows.Add("FOOD id=" + r.Id + " " + r.Food.Text.Name + " lv=" + r.Food.Level
                + " val=" + r.Food.TrueValue + " count=" + r.CookCount + " cooker=" + r.CookerType
                + " tags=[" + Tags(r.Food.Tags, true) + "]");
        }
        return string.Join("\n", rows);
    }
}
