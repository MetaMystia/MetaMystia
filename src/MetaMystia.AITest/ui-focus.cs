using System.Collections.Generic;

using TMPro;
using UnityEngine;

using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    const string Target = "委托采集\n(耗时30分钟)";

    public static object Execute()
    {
        var matches = new List<UIButtonBase>();
        foreach (var button in Object.FindObjectsOfType<UIButtonBase>())
        {
            if (!button.isActiveAndEnabled || !button.IsInteractable()) continue;
            foreach (var text in button.GetComponentsInChildren<TMP_Text>())
                if (text.text == Target) { matches.Add(button); break; }
        }
        if (matches.Count != 1) return $"Expected one button, found {matches.Count}";
        matches[0].Select();
        return "Selected " + Target;
    }
}
