using System.Collections.Generic;
using UnityEngine;

using DayScene.Interactables.Collections.ConditionComponents;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string>();
        foreach (var c in Object.FindObjectsOfType<CharacterConditionComponent>(true))
        {
            if (!c.isActiveAndEnabled) continue;
            rows.Add("NPC label=" + c.CharacterLabel + " name=" + c.name + " pos=" + c.transform.position
                + " active=" + c.gameObject.activeInHierarchy);
        }
        return rows.Count == 0 ? "no npc components" : string.Join("\n", rows);
    }
}
