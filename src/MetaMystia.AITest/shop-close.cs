using UnityEngine;

using DayScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<DaySceneShopPannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No active shop panel";
        panel.ClosePanel();
        return "Closed DaySceneShopPannel; verify snapshot";
    }
}
