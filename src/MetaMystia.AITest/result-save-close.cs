using UnityEngine;

using ResultScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<ResultSceneSavePannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No result save panel";
        panel.ClosePanel();
        return "Closed save panel without writing; verify Day scene";
    }
}
