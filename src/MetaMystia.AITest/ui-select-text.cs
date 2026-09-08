using System.Collections.Generic;

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    const string Target = "没什么事了";

    public static object Execute()
    {
        var matches = new List<UIButtonBase>();
        foreach (var button in Object.FindObjectsOfType<UIButtonBase>())
        {
            if (!button.isActiveAndEnabled || !button.IsInteractable()) continue;
            foreach (var text in button.GetComponentsInChildren<TMP_Text>())
                if (text.text == Target && text.isActiveAndEnabled && !text.canvasRenderer.cull)
                { matches.Add(button); break; }
        }
        if (matches.Count != 1) return $"Expected one button '{Target}', found {matches.Count}";
        matches[0].Select();
        matches[0].OnSubmit(new BaseEventData(EventSystem.current));
        return "Submitted " + Target;
    }
}
