using System.Collections.Generic;

using GameData.Core.Collections.CharacterUtility;
using GameData.CoreLanguage.Collections;

public static class Payload
{
    public static object Execute()
    {
        var s = 9000.RefSGuest();
        var rows = new List<string> { "guest=" + s.Text.Name + " id=" + s.Id };
        var food = new List<string>();
        foreach (int t in s.LikeFoodTagUnfolded)
            if (!food.Contains(t + "=" + t.GetFoodTag())) food.Add(t + "=" + t.GetFoodTag());
        rows.Add("FOOD " + string.Join(",", food));
        var bev = new List<string>();
        foreach (int t in s.LikeBevTagUnfolded)
            if (!bev.Contains(t + "=" + t.GetBeverageTag())) bev.Add(t + "=" + t.GetBeverageTag());
        rows.Add("BEV  " + string.Join(",", bev));
        return string.Join("\n", rows);
    }
}
