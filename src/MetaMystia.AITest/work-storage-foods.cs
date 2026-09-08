using System.Collections.Generic;

using GameData.RunTime.Common;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var pair in RunTimeStorage.GetAllFoods())
            if (pair.Value != 0 && pair.Key.Id == 35) rows.Add("FOOD id=" + pair.Key.Id + " " + pair.Key.Text.Name + " count=" + pair.Value);
        return rows.Count == 0 ? "no stored food id=35" : string.Join("\n", rows);
    }
}
