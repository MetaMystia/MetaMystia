using System.Collections.Generic;

using UnityEngine;

using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    const string Target = "GotoWork";

    public static object Execute()
    {
        var matches = new List<UIButtonHold>();
        foreach (var h in Object.FindObjectsOfType<UIButtonHold>())
            if (h.name == Target && h.isActiveAndEnabled && h.IsInteractable()) matches.Add(h);
        if (matches.Count != 1) return $"Expected one hold button '{Target}', found {matches.Count}";
        var btn = matches[0];
        btn.ExternalStartHold();
        return $"Hold started '{Target}' id={btn.GetInstanceID()} holdTime={btn.HoldTime}; wait then verify scene";
    }
}
