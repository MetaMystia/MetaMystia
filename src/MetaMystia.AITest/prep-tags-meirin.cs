using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    static bool FoodHit(int t)
    {
        var n = t.GetFoodTag();
        return n == "力量涌现" || n == "饱腹" || n == "中华" || n == "肉";
    }

    static bool BevHit(int t)
    {
        var n = t.GetBeverageTag();
        return n == "提神" || n == "可加热" || n == "古典";
    }

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
        foreach (var r in RunTimeStorage.GetAllRecipes())
        {
            bool hit = false;
            foreach (int t in r.Food.Tags)
                if (FoodHit(t)) { hit = true; break; }
            if (!hit) continue;
            rows.Add($"RECIPE id={r.Id} {r.Food.Text.Name} lv={r.Food.Level} val={r.Food.TrueValue} count={r.CookCount} cooker={r.CookerType} tags=[{FoodTags(r.Food.Tags)}]");
        }
        foreach (var pair in RunTimeStorage.GetAllBeverages())
        {
            bool hit = false;
            foreach (int t in pair.Key.Tags)
                if (BevHit(t)) { hit = true; break; }
            if (!hit) continue;
            rows.Add($"BEVERAGE id={pair.Key.Id} {pair.Key.Text.Name} lv={pair.Key.Level} val={pair.Key.TrueValue} count={pair.Value} tags=[{BevTags(pair.Key.Tags)}]");
        }
        return rows.Count == 0 ? "no candidates" : string.Join("\n", rows);
    }
}
