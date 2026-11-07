using UnityEngine;

using DayScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<DaySceneSustainedPannel>();
        if (panel == null || panel.FastForwardBtn == null || !panel.FastForwardBtn.isActiveAndEnabled || !panel.FastForwardBtn.IsInteractable())
            return "No fast forward button";
        panel.FastForwardBtn.ExternalStartHold();
        return "Started day fast-forward hold; wait for DayOver";
    }
}
