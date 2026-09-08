using System.Collections.Generic;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class Payload
{
    const string Target = "确认移动";

    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var t in Object.FindObjectsOfType<TMP_Text>(true))
        {
            if (t.text != Target) continue;
            rows.Add("TMP " + t.GetInstanceID() + " " + t.transform.name);
            var parent = t.transform.parent;
            while (parent != null)
            {
                var s = parent.GetComponent<Selectable>();
                rows.Add("  PARENT " + parent.name + " selectable=" + (s != null) + (s != null ? " active=" + s.isActiveAndEnabled + " interact=" + s.IsInteractable() + " type=" + s.GetType().Name : ""));
                parent = parent.parent;
            }
        }
        return rows.Count == 0 ? "No selectable contains " + Target : string.Join("\n", rows);
    }
}
