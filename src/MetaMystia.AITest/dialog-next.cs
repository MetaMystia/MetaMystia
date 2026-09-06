using UnityEngine;
using UnityEngine.InputSystem;

using Common.DialogUtility;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<DialogPannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No active dialogue";
        panel.Interact(new InputAction.CallbackContext());
        return "Dialogue continue submitted";
    }
}
