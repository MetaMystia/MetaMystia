using UnityEngine;

using Common.UI.NoteBookUtility;

public static class Payload
{
    public static object Execute()
    {
        var panel = Object.FindObjectOfType<NoteBookMainPannel>();
        if (panel == null || !panel.isActiveAndEnabled) return "No active NoteBookMainPannel";
        panel.CloseExternPanel();
        return "Closed NoteBookMainPannel; verify Day scene";
    }
}
