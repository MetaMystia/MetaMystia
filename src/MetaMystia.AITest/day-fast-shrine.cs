using UnityEngine;

using DayScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<DaySceneSustainedPannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No day sustained panel";
        panel.OpenFastTravelPanelParameterless();
        return "Opened fast travel panel; verify map choices";
    }
}
