using UnityEngine;
using UnityEngine.EventSystems;

using MainScene.UI;

public static class Payload
{
    public static object Execute()
    {
        var menu = Object.FindObjectOfType<MainMenuPannel>();
        if (menu == null || !menu.ContinueBtn.IsInteractable()) return "Continue unavailable";
        menu.ContinueBtn.OnSubmit(new BaseEventData(EventSystem.current));
        return "Continue submitted";
    }
}
