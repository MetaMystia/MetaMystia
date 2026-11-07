using System.Collections.Generic;

using TMPro;
using UnityEngine;

public static class Payload
{
    const string Target = "关闭";

    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var t in Object.FindObjectsOfType<TMP_Text>(true))
        {
            if (t.text != Target || !t.isActiveAndEnabled || t.canvasRenderer.cull) continue;
            rows.Add("TMP " + t.GetInstanceID() + " " + t.transform.name);
            var parent = t.transform.parent;
            while (parent != null)
            {
                var comps = new List<string>();
                foreach (var c in parent.GetComponents<Component>())
                    if (c != null) comps.Add(c.GetType().Name);
                rows.Add("  PARENT " + parent.name + " comps=" + string.Join(",", comps));
                parent = parent.parent;
            }
        }
        return rows.Count == 0 ? "No active TMP '" + Target + "'" : string.Join("\n", rows);
    }
}
