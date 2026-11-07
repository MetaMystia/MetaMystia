using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;

using GameData.Core.Collections;
using GameData.RunTime.Common;

using MetaMystia.ResourceEx.SpellCollection;

using static MetaMystia.Patch.HarmonyPrefixFlow;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeStorage))]
[AutoLog]
public static partial class RunTimeStoragePatch
{
    [HarmonyPatch(nameof(RunTimeStorage.GetAllBeverages))]
    [HarmonyPostfix]
    internal static void GetAllBeverages_Postfix(Il2CppReferenceArray<KeyValuePair<Sellable, int>> __result)
    {
        for (var i = 0; i < __result.Length; i++)
        {
            var item = __result[i];
            if (MinorikoSake.IsUnlimited(item.Key))
                __result[i] = new KeyValuePair<Sellable, int>(item.Key, -1);
        }
    }

    [HarmonyPatch(nameof(RunTimeStorage.BeverageOut))]
    [HarmonyPrefix]
    internal static bool BeverageOut_Prefix(int beverageId)
        // 绿茶在原版直接返回；绕过原生跳板对该短跳转的错误重定位。
        => beverageId == RunTimeStorage.GREEN_TEA_ID
            || (MinorikoSake.IsActive && MinorikoSake.IsUnlimited(DataBaseCore.RefBeverage(beverageId)))
            ? SkipOriginal : RunOriginal;

    [HarmonyPatch(nameof(RunTimeStorage.BeverageOutRange))]
    [HarmonyPrefix]
    internal static void BeverageOutRange_Prefix(ref IEnumerable<int> beverageIds) => FilterSake(ref beverageIds);

    [HarmonyPatch(nameof(RunTimeStorage.BeverageInRange))]
    [HarmonyPrefix]
    internal static void BeverageInRange_Prefix(ref IEnumerable<int> beverageIds) => FilterSake(ref beverageIds);

    private static void FilterSake(ref IEnumerable<int> beverageIds)
    {
        if (!MinorikoSake.IsActive)
            return;
        var result = new List<int>();
        foreach (var id in new List<int>(beverageIds))
            if (!MinorikoSake.IsUnlimited(DataBaseCore.RefBeverage(id)))
                result.Add(id);
        beverageIds = result.Cast<IEnumerable<int>>();
    }
}
