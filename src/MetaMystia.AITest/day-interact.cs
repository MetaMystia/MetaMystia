using UnityEngine;

using DayScene.Input;

public static class Payload
{
    public static object Execute()
    {
        var p = Object.FindObjectOfType<DayScenePlayerInputGenerator>();
        if (p == null || !Common.UI.UniversalGameManager.IsInputEnabled || !p.internalAvailability || p.currentInteractAction == null)
            return "No available interaction";
        p.TryInteract();
        return "Interaction submitted; observe next frame";
    }
}
