using System.Collections.Generic;

using GameData.Core.Collections;
using GameData.Core.Collections.CharacterUtility;
using GameData.CoreLanguage.Collections;
using GameData.RunTime.Common;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        rows.Add("foodTag-1=" + (-1).GetFoodTag());
        var s = 40.RefSGuest();
        rows.Add("guest40=" + s.Text.Name + " id=" + s.Id);
        var fp = new List<string>();
        foreach (int t in s.LikeFoodTagUnfolded) fp.Add(t + "=" + t.GetFoodTag());
        rows.Add("  FOOD " + string.Join(",", fp));
        var bp = new List<string>();
        foreach (int t in s.LikeBevTagUnfolded) bp.Add(t + "=" + t.GetBeverageTag());
        rows.Add("  BEV  " + string.Join(",", bp));
        rows.Add("--owned food with tagId -1--");
        foreach (var r in RunTimeStorage.GetAllRecipes())
            foreach (int t in r.Food.Tags)
                if (t == -1) { rows.Add("FOOD id=" + r.Id + " " + r.Food.Text.Name + " count=" + r.CookCount + " cooker=" + r.CookerType); break; }
        rows.Add("--owned beverage with tagId 3--");
        foreach (var pair in RunTimeStorage.GetAllBeverages())
            foreach (int t in pair.Key.Tags)
                if (t == 3) { rows.Add("BEV id=" + pair.Key.Id + " " + pair.Key.Text.Name + " count=" + pair.Value); break; }
        return string.Join("\n", rows);
    }
}
