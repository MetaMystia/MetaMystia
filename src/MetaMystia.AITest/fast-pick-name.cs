using System.Collections.Generic;

using UnityEngine;

using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    const string Target = "HakureiShrine";

    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var button in Object.FindObjectsOfType<UIButtonBase>())
            if (button.name == Target) rows.Add(button.GetType().Name + " id=" + button.GetInstanceID() + " active=" + button.isActiveAndEnabled + " interact=" + button.IsInteractable());
        return rows.Count == 0 ? "No UIButtonBase named " + Target : string.Join("\n", rows);
    }
}
