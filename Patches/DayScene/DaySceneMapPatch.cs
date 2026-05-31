using HarmonyLib;
using UnityEngine;

using DayScene;
using DayScene.Interactables.Collections.ConditionComponents;
using GameData.RunTime.DaySceneUtility.Collection;

using static MetaMystia.Patch.HarmonyPrefixFlow;

// using System.Linq;
// using DayScene.Interactables;
// using GameData.Core.Collections.DaySceneUtility.Collections;
// using DEYU.Utils;
// using MetaMystia.ResourceEx;
// using SgrYuki.Utils;


namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DayScene.DaySceneMap))]
[AutoLog]
public partial class DaySceneMapPatch
{
    [HarmonyPatch(nameof(DaySceneMap.SolveAndUpdateCharacterPositionInternal))]
    [HarmonyPrefix]
    public static bool SolveAndUpdateCharacterPositionInternal_Prefix(DaySceneMap __instance, Il2CppSystem.Collections.Generic.Dictionary<string, TrackedNPC> npcs, TrackedNPC npc, CharacterConditionComponent character, ref bool isNPCOnMap, bool changeRotation)
    {

        if (npc.key.IsResourceExSpecialGuest())
        {
            var spawnMarkerConfig = npc.key.GetSpawnMarkerConfig();
            character.Character.rb2d.transform.position = new Vector3(spawnMarkerConfig.x, spawnMarkerConfig.y, 0);
            character.Character.SetRotation((int)spawnMarkerConfig.rotation);
            isNPCOnMap = string.Equals(__instance.mapLabel, spawnMarkerConfig.mapLabel);
            return SkipOriginal;
        }
        return RunOriginal;


        // DO NOT DELETE
        // 下面的代码能跑，但是有一个严重问题，在旧版本 mod 加载过新稀客的存档里，不受 SpecialGuest 的 Destination 影响。需要考虑找到安全的方式「修改存档」，包括 override position
        // var templateMarker = __instance.AllSpawnMarkers.ToList().FirstOrDefault().Value;
        // if (templateMarker == null)
        // {
        //     Log.Warning($"Template marker not found! Skipping...");
        //     return;
        // }
        // if (npc.key.IsResourceExSpecialGuest())
        // {
        //     var spawnMarkerConfig = npc.key.GetSpawnMarkerConfig();
        //     if (__instance.AllSpawnMarkers.ContainsKey(npc.key))
        //     {
        //         return;
        //     }

        //     var newMarker = UnityEngine.Object.Instantiate(templateMarker, __instance.transform);
        //     newMarker.transform.position = new Vector3(spawnMarkerConfig.x, spawnMarkerConfig.y, 0);
        //     newMarker.name = npc.key;
        //     newMarker.spawnMarkerName = npc.key;
        //     newMarker.targetRotation = spawnMarkerConfig.rotation;

        //     __instance.AllSpawnMarkers[npc.key] = newMarker;
        //     Log.Warning($"Spawned marker at {spawnMarkerConfig.x}, {spawnMarkerConfig.y} for NPC: {npc.key}");
        //     Log.Warning($"Added marker to map: {__instance.name} {__instance.AllSpawnMarkers[npc.key].spawnMarkerName}");
        // }
    }
}
