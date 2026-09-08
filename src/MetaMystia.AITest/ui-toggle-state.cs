using System.Collections.Generic;

using TMPro;
using UnityEngine;

using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var t in Object.FindObjectsOfType<UIButtonToggle>(true))
        {
            if (!t.isActiveAndEnabled) continue;
            var labels = new List<string>();
            foreach (var text in t.GetComponentsInChildren<TMP_Text>(true))
                if (text.isActiveAndEnabled && !string.IsNullOrWhiteSpace(text.text)) labels.Add(text.text.Replace("\n", "/"));
            rows.Add("TOGGLE | " + t.GetInstanceID() + " | " + t.name + " | on=" + t.IsToggleOn
                + " | interact=" + t.IsInteractable() + " | " + string.Join(";", labels));
        }
        return rows.Count == 0 ? "no active toggles" : string.Join("\n", rows);
    }
}
