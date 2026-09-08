using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    static string Tags(System.Collections.Generic.IEnumerable<int> tags, bool food)
    {
        var parts = new List<string>();
        foreach (int t in tags)
        {
            var name = food ? t.GetFoodTag() : t.GetBeverageTag();
            if (name != null) parts.Add(t + "=" + name);
        }
        return string.Join(",", parts);
    }

    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var r in RunTimeStorage.GetAllRecipes())
        {
            bool keep = false;
            foreach (int t in r.Food.Tags)
            {
                var n = t.GetFoodTag();
                if (n == "高级" || n == "甜" || n == "饱腹" || n == "实惠" || n == "不可思议") keep = true;
            }
            if (keep) rows.Add($"RECIPE id={r.Id} {r.Food.Text.Name} lv={r.Food.Level} val={r.Food.TrueValue} count={r.CookCount} cooker={r.CookerType} tags=[{Tags(r.Food.Tags, true)}]");
        }
        foreach (var pair in RunTimeStorage.GetAllBeverages())
        {
            bool keep = false;
            foreach (int t in pair.Key.Tags)
            {
                var n = t.GetBeverageTag();
                if (n == "无酒精" || n == "低酒精" || n == "可加热" || n == "酒精" || n == "加冰") keep = true;
            }
            if (keep) rows.Add($"BEVERAGE id={pair.Key.Id} {pair.Key.Text.Name} lv={pair.Key.Level} val={pair.Key.TrueValue} count={pair.Value} tags=[{Tags(pair.Key.Tags, false)}]");
        }
        return rows.Count == 0 ? "no candidates" : string.Join("\n", rows);
    }
}
