using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    static readonly string[] Names = { "饭团", "炙猪肉饭团", "毛玉熔岩豆腐", "炒肉丝", "炸猪肉排", "蜜汁叉烧", "一击☆必杀", "华光玉煎包" };

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
