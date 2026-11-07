using UnityEngine;
using UnityEngine.EventSystems;

using DayScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<DaySceneFastTravelSubPannel>();
        if (panel == null || panel.ConfirmTeleportBtn == null || !panel.ConfirmTeleportBtn.isActiveAndEnabled || !panel.ConfirmTeleportBtn.IsInteractable())
            return "No confirm teleport button";
        panel.ConfirmTeleportBtn.OnSubmit(new BaseEventData(EventSystem.current));
        return "Submitted Yukari teleport; verify map and fund";
    }
}
