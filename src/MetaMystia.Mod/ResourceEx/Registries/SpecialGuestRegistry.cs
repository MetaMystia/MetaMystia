using System.Collections.Generic;
using System.Linq;

using GameData.Core.Collections.DaySceneUtility;
using GameData.RunTime.DaySceneUtility;
using GameData.RunTime.Common;

using Il2CppInterop.Runtime.InteropTypes.Arrays;

using MetaMystia.ResourceEx.Models;

namespace MetaMystia.ResourceEx.Registries;

/*
ResourceEx 特殊客人的写入路径（迁移后）：

由框架按 ModDatabaseExtension.OnInjectSpecialGuests 写入：
    SpecialGuest（含刷客池 SpecialGuestPool） -> DataBaseCharacter.SpecialGuest / DataBaseCore.Izakayas
    GuestProfilePair（立绘／像素集）            -> DataBaseCharacter.SpecialGuestVisual
    角色文本与点单请求行                        -> DataBaseLanguage.SpecialGuest / SpecialGuestRequest*
    评价与对话                                  -> NightSceneLanguage.SpecialEvaluation / SpecialConversation

仍由本模组负责：
    Before/After DayScene Awake: 运行时 NPC 归图（RefreshAllDayNpcs）；点位由桥接按注入的刷新点数据生成
    存档内的对话重置（ResetTrackedNpcDialog）
    角色配置与查询（供地图、商人、对话、立绘等其它链路取用）

已删除：直接写游戏表的 RegisterAllSpecialGuests／RegisterSpecialPortraits／RegisterAllEvaluations／
RegisterAllConversations／RegisterAllFoodRequests／RegisterAllBevRequests／RegisterAllSpecialGuestPairs／
RegisterAllSpawnConfigs／RegisterNPCs（后者迁移前即为空实现）。
*/

/// <summary>
/// 特典角色（SpecialGuest）领域注册器：持有角色配置，并提供点位与运行时 NPC 能力。
/// </summary>
[AutoLog]
public static partial class SpecialGuestRegistry
{
    private static readonly Dictionary<(int id, string type), CharacterConfig> _characterConfigs = new();

    internal static void Merge(ResourceConfig config, string packageName)
    {
        if (config?.characters == null) return;

        foreach (var charConfig in config.characters)
        {
            _characterConfigs[(charConfig.id, charConfig.type)] = charConfig;
            Log.LogInfo($"[{packageName}] Loaded config for character {charConfig.name} ({charConfig.id}, {charConfig.type})");
        }
    }

    public static IEnumerable<CharacterConfig> GetAllCharacterConfigs()
    {
        return _characterConfigs.Values;
    }
    public static bool IsResourceExSpecialGuest(this int id, string type = "Special") => _characterConfigs.ContainsKey((id, type));
    public static bool IsResourceExSpecialGuest(this string stringId, string type = "Special") => _characterConfigs.Values.Any(c => c.label == stringId && c.type == type);

    public static CharacterConfig GetCharacterConfig(int id, string type = "Special")
    {
        if (_characterConfigs.TryGetValue((id, type), out var config))
        {
            return config;
        }
        return null;
    }

    public static CharacterConfig GetCharacterConfig(string stringId, string type = "Special")
    {
        return _characterConfigs.Values.FirstOrDefault(c => c.label == stringId && c.type == type);
    }

    private static void RegisterAllSpawnMarkers()
    {
        Log.Info($"Registering Spawn Markers from ResourceEx...");
        GetAllCharacterConfigs()
            .Select(c => c.spawnMarker ?? new SpawnMarkerConfig() { mapLabel = "BeastForest", x = 0f, y = 0f, rotation = DayScene.Input.DayScenePlayerInputGenerator.CharacterRotation.Down })
            .ToList()
            .ForEach(RegisterSpawnMarker);
    }

    private static void RegisterSpawnMarker(SpawnMarkerConfig config)
    {
        var mapLabel = DataBaseDay.allSpawnMarkerLabels.ContainsKey(config.mapLabel) ? config.mapLabel : "BeastForest";
        DataBaseDay.allSpawnMarkerLabels[mapLabel].Add(config.mapLabel);
        Log.Info($"Registered Spawn Marker for map: {config.mapLabel}");
    }

    public static SpawnMarkerConfig GetSpawnMarkerConfig(this string stringId) =>
        GetAllCharacterConfigs()
            .FirstOrDefault(c => c.label == stringId)
            ?.spawnMarker
            ?? new SpawnMarkerConfig()
            {
                mapLabel = "BeastForest",
                x = 0f,
                y = 0f,
                rotation = DayScene.Input.DayScenePlayerInputGenerator.CharacterRotation.Down
            };

    public static void RefreshAllDayNpcs()
    {
        GetAllCharacterConfigs()
            .Where(c => c.spawnMarker != null)
            .ToList()
            .ForEach(RefreshDayNpc);
    }

    private static void RefreshDayNpc(CharacterConfig config)
    {
        if (config.spawnMarker == null) return;
        var mapLabel = config.spawnMarker.mapLabel;
        if (string.IsNullOrEmpty(mapLabel) || !DataBaseDay.mapReference.ContainsKey(mapLabel))
        {
            Log.Warning($"Cannot place ResourceEx NPC {config.label}: map '{mapLabel}' is not registered.");
            return;
        }

        var npc = RunTimeDayScene.GetTrackedNPC(config.label);
        if (npc == null) return;

        // 电话按 trackedNPCs 分组；保留原目的地和对话池，点位由 SpawnMarker 定位。
        RunTimeDayScene.RemoveNPC(config.label);
        npc.overridePosition = null;
        RunTimeDayScene.GetMapNPCs(mapLabel).Add(config.label, npc);
        RunTimeDayScene.OnRequireCurrentMapRefreshCallback?.Invoke();
        Log.Info($"Initialized Day Scene Spawn Config for Special Guest: {config.name} ({config.id})");
    }

    // 如果使用过旧版 mod，存档内的 NPC 对话可能仍是 Wriggle 未更新，导致对话缺失或错误
    // 这里手动重置所有已追踪的扩展的 NPC 的对话内容，以确保对话正确
    // 不过当游戏触发羁绊升级时，也会正确更新对话内容
    // 未来也许可以考虑直接删除此逻辑（？）
    internal static void ResetTrackedNpcDialog()
    {
        foreach (var trackedNPCsDict in RunTimeDayScene.trackedNPCs.Values)
        {
            foreach (var kvp in trackedNPCsDict)
            {
                var stringId = kvp.Key;
                if (stringId.IsResourceExSpecialGuest())
                {
                    var config = GetCharacterConfig(stringId);

                    var runTimeData = RunTimeAlbum.RefOrGenerateSpecialRunTimeData(config.id);
                    if (runTimeData == null) continue;

                    int level = runTimeData.CurrentBondLevel;
                    var chatData = level switch
                    {
                        1 => config?.kizuna?.lv1ChatData,
                        2 => config?.kizuna?.lv2ChatData,
                        3 => config?.kizuna?.lv3ChatData,
                        4 => config?.kizuna?.lv4ChatData,
                        5 => config?.kizuna?.lv5ChatData,
                        _ => null
                    };

                    if (chatData != null && chatData.Count > 0)
                    {
                        var dialogs = new Il2CppStringArray(chatData.Where(x => !string.IsNullOrEmpty(x)).ToArray());

                        RunTimeDayScene.SetNPCDialog(stringId, "Wriggle", dialogs);
                        Log.Info($"Reset dialog for tracked NPC: {config.name} ({stringId}) at Lv {level}");
                    }
                }
            }
        }
    }
}
