using System.Collections.Generic;

using TMPro;
using UnityEngine;

using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var b in Object.FindObjectsOfType<UIButtonBase>())
        {
            if (!b.isActiveAndEnabled || !b.IsInteractable()) continue;
            string label = "";
            foreach (var t in b.GetComponentsInChildren<TMP_Text>())
                if (t.text.Length > 0) { label += t.text + "|"; }
            rows.Add(b.name + " :: " + label);
        }
        return rows.Count == 0 ? "no UIButtonBase" : string.Join("\n", rows);
    }
}
