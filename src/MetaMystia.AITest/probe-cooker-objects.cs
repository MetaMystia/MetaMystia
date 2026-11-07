using System.Collections.Generic;

using NightScene.CookingUtility;
using UnityEngine;

public static class Payload
{
    static void Walk(Transform t, List<string> rows)
    {
        if (t.name.Contains("Cook") || t.name.Contains("Desk") || t.name.Contains("Table"))
        {
            var c = t.GetComponent<CookController>();
            rows.Add("OBJ " + t.name + " active=" + t.gameObject.activeSelf + " controller=" + (c != null) + (c != null ? " phase=" + c.Phase : ""));
        }
        for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i), rows);
    }

    public static object Execute()
    {
        var rows = new List<string>();
        rows.Add("scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++) Walk(roots[i].transform, rows);
        return rows.Count == 0 ? "no objects with Cook/Desk/Table names" : string.Join("\n", rows);
    }
}
