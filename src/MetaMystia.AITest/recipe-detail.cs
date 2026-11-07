using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    static readonly string[] Names = { "大江户船祭", "分子蛋", "无意识妖怪慕斯", "山泉双色果盘", "饭团", "能量串", "热松饼", "班尼迪克蛋" };

    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var r in RunTimeStorage.GetAllRecipes())
        {
            bool keep = false;
            foreach (string n in Names) if (r.Food.Text.Name == n) keep = true;
            if (!keep) continue;
            var ing = new List<string>();
            foreach (int id in r.Ingredients)
                ing.Add(id + "=" + id.RefIngredient().Text.Name + "(" + RunTimeStorage.GetIngredientCountById(id) + ")");
            rows.Add("RECIPE id=" + r.Id + " " + r.Food.Text.Name + " cooker=" + r.CookerType + " count=" + r.CookCount + " time=" + r.CookTime + " ing=[" + string.Join(",", ing) + "]");
        }
        return rows.Count == 0 ? "no recipe" : string.Join("\n", rows);
    }
}
