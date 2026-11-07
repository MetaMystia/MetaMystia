using UnityEngine;
using UnityEngine.EventSystems;

using ResultScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<ResultSceneStatusPannel>();
        if (panel == null || panel.ClosePanelBtn == null || !panel.ClosePanelBtn.isActiveAndEnabled) return "No result status panel";
        panel.ClosePanelBtn.OnSubmit(new BaseEventData(EventSystem.current));
        return "Submitted result continue; verify save panel";
    }
}
