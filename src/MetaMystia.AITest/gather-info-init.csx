using GameData.Core.Collections.DaySceneUtility;
using GameData.RunTime.Common;
using GameData.RunTime.DaySceneUtility;

public static class AITestGather
{
    public static string Read(string key)
    {
        var data = DataBaseDay.RefCollectable(key);
        var tracked = RunTimeDayScene.GetTrackedCollectable(key);
        var rows = new List<string> { $"{key} AP={RunTimeDayScene.RemainActions} available={RunTimeDayScene.RefTrackedCollectableAvailability(key)} cooldown={tracked.currentCoolDown} regen={data.GetRegenerateActions()} hours={data.showTime}" };
        foreach (var p in data.primaryProduct)
            rows.Add($"PRIMARY {p.GetText().Name} configured={p.productAmount} storage={RunTimeStorage.GetAmountInStorage(p)}");
        foreach (var p in data.secondaryProduct)
            rows.Add($"SECONDARY {p.product.GetText().Name} probability={p.probability} configured={p.product.productAmount} storage={RunTimeStorage.GetAmountInStorage(p.product)}");
        return string.Join("\n", rows);
    }
}

"Gather reader ready"
