using UnityEngine;

using DayScene;
using DayScene.Interactables;
using GameData.Core.Collections.DaySceneUtility;

namespace MetaMystia.ResourceEx.Registries;

public static class SpawnMarkerRegistry
{
    public static void Register(DaySceneMap map, Il2CppSystem.Collections.Generic.Dictionary<string, SpawnMarker> markers)
    {
        foreach (var character in SpecialGuestRegistry.GetAllCharacterConfigs())
        {
            var config = character.spawnMarker;
            if (character.type != "Special" || config == null || config.mapLabel != map.mapLabel)
                continue;

            var name = character.label;
            if (markers.ContainsKey(name))
                continue;

            var go = new GameObject(name);
            go.transform.SetParent(map.spawnMarkerField, false);
            go.transform.position = new Vector3(config.x, config.y, 0f);
            var marker = go.AddComponent<SpawnMarker>();
            marker.spawnMarkerName = name;
            marker.targetRotation = config.rotation;
            markers.Add(name, marker);
            DataBaseDay.allSpawnMarkerLabels[map.mapLabel].Add(name);
        }
    }
}
