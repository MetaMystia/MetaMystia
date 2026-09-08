using UnityEngine;

using NightScene.UI.GuestManagementUtility;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<WorkSceneServePannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No serve panel open";
        panel.ClosePanel();
        return "Called ClosePanel on serve panel";
    }
}
