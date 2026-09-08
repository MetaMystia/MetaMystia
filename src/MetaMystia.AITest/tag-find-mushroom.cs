using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    static string Tags(IEnumerable<int> tags)
    {
        var parts = new List<string>();
        foreach (int t in tags)
        {
            var n = t.GetFoodTag();
            if (n != null) parts.Add(n);
        }
        return string.Join(",", parts);
    }

    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var r in RunTimeStorage.GetAllRecipes())
            foreach (int t in r.Food.Tags)
                if (t.GetFoodTag() == "菌类")
                {
                    rows.Add("FOOD id=" + r.Id + " " + r.Food.Text.Name + " lv=" + r.Food.Level
                        + " val=" + r.Food.TrueValue + " count=" + r.CookCount + " cooker=" + r.CookerType
                        + " tags=[" + Tags(r.Food.Tags) + "]");
                    break;
                }
        return rows.Count == 0 ? "no owned mushroom food" : string.Join("\n", rows);
    }
}
