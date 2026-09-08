using UnityEngine;

using ResultScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<ResultSceneSavePannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No result save panel";
        panel.OverrideSave(3);
        return "Triggered save to slot index 3; wait for write log";
    }
}
