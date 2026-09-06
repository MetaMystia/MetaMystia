using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using DayScene.Input;
using DayScene.Interactables;
using DayScene.Interactables.Collections.ConditionComponents;
using GameData.Core.Collections.DaySceneUtility;
using GameData.RunTime.DaySceneUtility;

public static class Payload
{
    public static object Execute()
    {
        var p = Object.FindObjectOfType<DayScenePlayerInputGenerator>();
        if (p == null) return "No day player";
        var rows = new List<string> { $"MAP={DayScene.SceneManager.Instance.CurrentActiveMapLabel} position={p.transform.position} AP={RunTimeDayScene.RemainActions} clock={DayScene.UI.UIManager.Instance.GetTimeCode(RunTimeDayScene.RemainActions)} moving={p.Moving} swapping={DayScene.SceneManager.Instance.IsMapSwapping}" };
        foreach (var c in Object.FindObjectsOfType<CollectableConditionComponent>().OrderBy(c => Vector3.Distance(p.transform.position, c.transform.position)).Take(12))
        {
            var tracked = RunTimeDayScene.GetTrackedCollectable(c.collectableKey);
            var data = DataBaseDay.RefCollectable(c.collectableKey);
            var area = c.GetComponent<InteractableArea>();
            var collider = c.GetComponent<Collider2D>();
            rows.Add($"GATHER | {c.collectableKey} | origin={c.transform.position} bounds={collider.bounds} | reach={area != null && p.allInteractables.Contains(area)} available={RunTimeDayScene.RefTrackedCollectableAvailability(c.collectableKey)} cooldown={tracked.currentCoolDown} regen={data.GetRegenerateActions()}");
        }
        foreach (var t in Object.FindObjectsOfType<MapTransitionData>())
            rows.Add($"EXIT | {t.name} | origin={t.transform.position} bounds={t.GetComponent<Collider2D>().bounds} | to={t.targetSceneLabel}/{t.targetSceneSpawnMarker} cost={(t.shouldCostAction ? 1 : 0)} unlocked={t.Unlocked}");
        return string.Join("\n", rows);
    }
}
