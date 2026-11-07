using System.Collections.Generic;

using TMPro;
using UnityEngine;

using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    const string Target = "Lv1";

    public static object Execute()
    {
        var matches = new List<UIButtonToggle>();
        foreach (var t in Object.FindObjectsOfType<UIButtonToggle>())
        {
            if (!t.isActiveAndEnabled || !t.IsInteractable()) continue;
            foreach (var text in t.GetComponentsInChildren<TMP_Text>())
                if (text.text == Target && text.isActiveAndEnabled && !text.canvasRenderer.cull)
                { matches.Add(t); break; }
        }
        if (matches.Count != 1) return $"Expected one toggle '{Target}', found {matches.Count}";
        var btn = matches[0];
        var wasOn = btn.IsToggleOn;
        btn.SubmitExtern();
        return $"Submitted '{Target}' id={btn.GetInstanceID()} wasOn={wasOn}; verify snapshot";
    }
}
