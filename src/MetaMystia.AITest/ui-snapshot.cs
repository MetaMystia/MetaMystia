using System.Collections.Generic;

using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class Payload
{
    static string Path(Transform t)
    {
        var path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    static bool Visible(Graphic g) => g.isActiveAndEnabled && g.canvas != null && g.canvas.isActiveAndEnabled
        && !g.canvasRenderer.cull && g.canvasRenderer.GetInheritedAlpha() * g.color.a > 0.01f;

    public static object Execute()
    {
        var rows = new List<string> { "Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name };
        foreach (var t in Object.FindObjectsOfType<TMP_Text>())
            if (Visible(t) && !string.IsNullOrWhiteSpace(t.text)) rows.Add("TEXT | " + t.GetInstanceID() + " | " + t.text.Replace("\n", " / "));
        foreach (var t in Object.FindObjectsOfType<Text>())
            if (Visible(t) && !string.IsNullOrWhiteSpace(t.text)) rows.Add("TEXT | " + t.GetInstanceID() + " | " + t.text.Replace("\n", " / "));
        foreach (var s in Object.FindObjectsOfType<Selectable>())
        {
            if (!s.isActiveAndEnabled || !s.IsInteractable()) continue;
            var labels = new List<string>();
            foreach (var t in s.GetComponentsInChildren<TMP_Text>())
                if (Visible(t) && !string.IsNullOrWhiteSpace(t.text)) labels.Add(t.text.Replace("\n", " / "));
            if (labels.Count > 0 || (s.targetGraphic != null && Visible(s.targetGraphic)))
                rows.Add("CONTROL | " + s.GetInstanceID() + " | " + s.name + " | " + string.Join("; ", labels));
        }
        var selected = EventSystem.current?.currentSelectedGameObject;
        var selectedControl = selected != null ? selected.GetComponent<Selectable>() : null;
        rows.Add("SELECTED | " + (selected != null ? (selectedControl != null ? selectedControl.GetInstanceID() : selected.GetInstanceID()) + " | " + Path(selected.transform) : "none"));
        return string.Join("\n", rows);
    }
}
