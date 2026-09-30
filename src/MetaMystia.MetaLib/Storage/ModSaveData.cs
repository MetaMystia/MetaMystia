using System;
using System.Linq;
using System.Text.Json;

using Il2CppInterop.Runtime.InteropTypes.Arrays;

using Common;
using GameData.RunTime.Common;

using Carrier = GameData.RunTime.Common.PlayerSaveFile.DLCSchedulerSaveData;
using CarrierDictionary = Il2CppSystem.Collections.Generic.Dictionary<string, GameData.RunTime.Common.PlayerSaveFile.DLCSchedulerSaveData>;
using CarrierDictionaryInterface = Il2CppSystem.Collections.Generic.IDictionary<string, GameData.RunTime.Common.PlayerSaveFile.DLCSchedulerSaveData>;
using NativeDictionary = Il2CppSystem.Collections.IDictionary;

namespace MetaMystia.MetaLib.Storage;

[AutoLog]
public static partial class ModSaveData
{
    public const string StorageKey = "MetaLib";
    private static int mainThreadId;
    private static bool hasSession;
    private static readonly ModuleHandlers handlers = new();

    internal static void SetMainThread() => mainThreadId = Environment.CurrentManagedThreadId;
    internal static void BeginLoad() => Close();
    internal static void Close()
    {
        hasSession = false;
        handlers.Reset();
    }

    internal static void CompleteLoad()
    {
        hasSession = true;
        if (!TryOpen(out var document, out var error))
        {
            Log.Warning(error);
            return;
        }
        foreach (var failure in handlers.Load(document))
            Log.Warning(failure);
    }

    public static bool TryRegister(string moduleId, int version, Func<ModuleData?, bool> onLoad, Func<JsonElement?> onSave, out string error)
    {
        if (Environment.CurrentManagedThreadId != mainThreadId || hasSession)
        {
            error = "请在插件初始化时、首次读档前的主线程注册回调。";
            return false;
        }
        return handlers.TryRegister(moduleId, version, onLoad, onSave, out error);
    }

    internal static bool TryPrepareSave(out string error)
    {
        if (!TryGetPayload(out var before, out error))
            return false;
        var document = new SaveDocument(before);
        if (!handlers.TryCapture(document, out error))
            return false;
        var after = document.ToArray();
        if (!before.SequenceEqual(after))
            Publish(after);
        return true;
    }

    public static bool TryRead(string moduleId, out ModuleData? value, out string error)
    {
        value = null;
        return TryOpen(out var document, out error) && document.TryRead(moduleId, out value, out error);
    }

    public static bool TryWrite(string moduleId, int version, JsonElement data, out string error)
    {
        if (handlers.IsDispatching)
        {
            error = "回调应返回数据，不能直接写入存储。";
            return false;
        }
        if (!TryOpen(out var document, out error) || !document.TryWrite(moduleId, version, data, out error))
            return false;
        Publish(document.ToArray());
        return true;
    }

    public static bool TryRemove(string moduleId, int supportedVersion, out string error)
    {
        if (handlers.IsDispatching)
        {
            error = "不能在回调中删除存储。";
            return false;
        }
        if (!TryOpen(out var document, out error) || !document.TryRemove(moduleId, supportedVersion, out error))
            return false;
        Publish(document.ToArray());
        return true;
    }

    public static bool TryGetModuleIds(out string[] moduleIds, out string error)
    {
        moduleIds = Array.Empty<string>();
        if (!TryOpen(out var document, out error))
            return false;
        moduleIds = document.ModuleIds;
        return true;
    }

    internal static bool TryGetPayload(out string[] payload, out string error)
    {
        payload = Array.Empty<string>();
        error = "";
        if (Environment.CurrentManagedThreadId != mainThreadId)
        {
            error = "存储 API 必须在 Unity 主线程调用。";
            return false;
        }
        if (!hasSession)
        {
            error = "当前没有已加载的存档。";
            return false;
        }
        if (LoadingSceneManager.LoadedGameDataProfile?.ActiveDLCLabel?.Contains(StorageKey) == true)
        {
            error = "存储键被注册为启用的 DLC，已拒绝操作。";
            return false;
        }
        return TryGetPayload(RunTimePlayerData.NotLoadedDLCSchedulerSaveData, out payload, out error);
    }

    internal static bool TryGetPayload(CarrierDictionary? dictionary, out string[] payload, out string error)
    {
        payload = Array.Empty<string>();
        error = "";
        if (dictionary == null || !dictionary.ContainsKey(StorageKey))
            return true;
        var carrier = dictionary.Cast<NativeDictionary>()[(Il2CppSystem.String)StorageKey].Cast<Carrier>();
        if (carrier.finishedEvents == null ||
            carrier.scheduledEvents == null || carrier.scheduledEvents.Count != 0 ||
            carrier.scheduledNews == null || carrier.scheduledNews.Count != 0 ||
            carrier.scheduledNewsReplaceContents == null || carrier.scheduledNewsReplaceContents.Count != 0 ||
            carrier.allTrackingMissions == null || carrier.allTrackingMissions.Count != 0 ||
            carrier.finishedMissions == null || carrier.finishedMissions.Length != 0)
        {
            error = "存储键下的载体结构不匹配，原数据已保留。";
            return false;
        }
        payload = new string[carrier.finishedEvents.Length];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = carrier.finishedEvents[i];
        return true;
    }

    private static bool TryOpen(out SaveDocument document, out string error)
    {
        document = null!;
        if (!TryGetPayload(out var payload, out error))
            return false;
        document = new(payload);
        return true;
    }

    private static void Publish(string[] payload)
    {
        var carrier = new Carrier
        {
            dlcSaveDate = RunTimePlayerData.GetDay().CorrectedDay,
            scheduledEvents = new(),
            scheduledNews = new(),
            scheduledNewsReplaceContents = new(),
            allTrackingMissions = new(),
            finishedEvents = new Il2CppStringArray(payload),
            finishedMissions = new Il2CppStringArray(0)
        };
        var current = RunTimePlayerData.NotLoadedDLCSchedulerSaveData;
        var replacement = current == null ? new CarrierDictionary() : new CarrierDictionary(current.Cast<CarrierDictionaryInterface>());
        // IDictionary 接收装箱对象，由原生实现拆箱；不经过泛型 set_Item 的值类型封送。
        replacement.Cast<NativeDictionary>()[(Il2CppSystem.String)StorageKey] = carrier;
        // 原字典及载体不变，正在进行的保存继续读取原快照。
        RunTimePlayerData.NotLoadedDLCSchedulerSaveData = replacement;
    }
}
