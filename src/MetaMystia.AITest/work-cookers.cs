using System.Collections.Generic;

using NightScene.CookingUtility;
using UnityEngine;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var c in Object.FindObjectsOfType<CookController>(true))
        {
            string res = c.Result == null ? "none" : c.Result.Type + " " + c.Result.Id + " " + c.Result.Text.Name;
            rows.Add("cooker pos=" + c.GridPosition + " id=" + c.Cooker.Id + " type=" + c.Cooker.Type + " phase=" + c.Phase + " result=" + res);
        }
        return rows.Count == 0 ? "no cookers" : string.Join("\n", rows);
    }
}
