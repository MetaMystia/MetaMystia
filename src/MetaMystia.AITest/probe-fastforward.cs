using UnityEngine;

using Common.UI;
using DayScene.UI;
using DEYU.AdpUISystem.LogicalCollection;

public static class Payload
{
    public static object Execute()
    {
        var rows = new System.Collections.Generic.List<string>();
        foreach (var h in Object.FindObjectsOfType<UIButtonHold>())
            rows.Add("HOLD name=" + h.name + " active=" + h.isActiveAndEnabled + " interact=" + h.IsInteractable() + " id=" + h.GetInstanceID());
        var panel = Object.FindObjectOfType<DaySceneSustainedPannel>();
        if (panel != null)
        {
            rows.Add("PANEL=" + (panel == null ? "null" : "ok"));
            var f = panel.FastForwardBtn;
            rows.Add("FIELD name=" + (f != null ? f.name : "null") + " active=" + (f != null ? f.isActiveAndEnabled.ToString() : "-") + " interact=" + (f != null ? f.IsInteractable().ToString() : "-"));
        }
        return string.Join("\n", rows);
    }
}
