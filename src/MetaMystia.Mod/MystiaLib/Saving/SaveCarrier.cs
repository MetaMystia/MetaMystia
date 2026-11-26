#nullable enable

using System.Diagnostics.CodeAnalysis;

using GameData.RunTime.Common;

using Carrier = GameData.RunTime.Common.PlayerSaveFile.DLCSchedulerSaveData;
using CarrierDictionary = Il2CppSystem.Collections.Generic.Dictionary<string, GameData.RunTime.Common.PlayerSaveFile.DLCSchedulerSaveData>;

namespace MetaMystia.MystiaLib.Saving;

/// <summary>
/// 记录存在存档 <c>schedulerPartialDLC</c> 的 <see cref="Bootstrap.Id"/> 键下。该键永不启用，
/// 原版当作未加载的 DLC 数据带进下一次存档，没有本模组时同样保留。
/// </summary>
internal static class SaveCarrier
{
    // 持有引用，防止地址被回收后由新字典复用。
    private static CarrierDictionary? _seen;

    /// <summary>取出当前存档的记录。原版每次读档都换成新字典，与上次是同一个时返回 false。</summary>
    public static bool TryTake([NotNullWhen(true)] out string[]? records)
    {
        records = null;
        var current = RunTimePlayerData.NotLoadedDLCSchedulerSaveData;
        if (current == null || current.Pointer == _seen?.Pointer) return false;
        _seen = current;
        if (current.ContainsKey(Bootstrap.Id))
            records = current[Bootstrap.Id].finishedEvents;
        records ??= [];
        return true;
    }

    /// <summary>替换全部记录。原版在线程池中生成存档，因此换成新字典，不改动旧的。</summary>
    public static void Write(string[] records)
    {
        var carrier = new Carrier
        {
            dlcSaveDate = 0,
            scheduledEvents = new(),
            scheduledNews = new(),
            scheduledNewsReplaceContents = new(),
            allTrackingMissions = new(),
            finishedEvents = records,
            finishedMissions = new string[0],
        };
        var replaced = new CarrierDictionary(_seen!.Cast<Il2CppSystem.Collections.Generic.IDictionary<string, Carrier>>());
        // 值是装箱的原生结构体，经非泛型接口由原生代码拆箱写入，见 docs/reference/il2cppinterop-defects.md。
        replaced.Cast<Il2CppSystem.Collections.IDictionary>()[Bootstrap.Id] = carrier;
        RunTimePlayerData.NotLoadedDLCSchedulerSaveData = _seen = replaced;
    }
}
