using System.Collections.Generic;

using UnityEngine;

using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    const string Target = "ScarletMansion";

    public static object Execute()
    {
        var matches = new List<UIButtonBase>();
        foreach (var button in Object.FindObjectsOfType<UIButtonBase>())
            if (button.name == Target && button.isActiveAndEnabled && button.IsInteractable()) matches.Add(button);
        if (matches.Count != 1) return $"Expected one UIButtonBase '{Target}', found {matches.Count}";
        matches[0].Select();
        return "Selected " + Target;
    }
}
