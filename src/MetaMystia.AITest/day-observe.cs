using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using DayScene.Input;
using DayScene.Interactables;

public static class Payload
{
    public static object Execute()
    {
        var p = Object.FindObjectOfType<DayScenePlayerInputGenerator>();
        if (p == null) return "No day player";
        var rows = new List<string> { $"PLAYER | {p.transform.position} | input={Common.UI.UniversalGameManager.IsInputEnabled} | internal={p.internalAvailability} | moving={p.Moving}" };
        foreach (var a in Object.FindObjectsOfType<InteractableArea>().OrderBy(a => Vector3.Distance(p.transform.position, a.transform.position)).Take(16))
        {
            var collider = a.GetComponent<Collider2D>();
            rows.Add($"AREA | {a.GetInstanceID()} | {a.name} | {a.transform.position} | distance={Vector3.Distance(p.transform.position, a.transform.position):F2} | inReach={p.allInteractables.Contains(a)} | collider={collider != null && collider.enabled} | trigger={a.triggerMode}");
        }
        return string.Join("\n", rows);
    }
}
