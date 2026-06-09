using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

using DayScene.Interactables.Collections.BehaviourComponents;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(NitoriTelephoneComponent))]
[AutoLog]
public partial class NitoriTelephoneComponentPatch
{
    private static bool _merchantContactFlowActive;
    private static readonly HashSet<string> _soldOutVisibilityLogged = [];

    [HarmonyPatch(nameof(NitoriTelephoneComponent.OnInteract))]
    [HarmonyPrefix]
    public static void OnInteract_Prefix(NitoriTelephoneComponent __instance)
    {
        _merchantContactFlowActive = false;
        _soldOutVisibilityLogged.Clear();

        if (__instance == null)
            return;

        InjectResourceExMerchants(__instance);
    }

    [HarmonyPatch(nameof(NitoriTelephoneComponent.OpenFirstMenu))]
    [HarmonyPrefix]
    public static void OpenFirstMenu_Prefix()
    {
        _merchantContactFlowActive = false;
        _soldOutVisibilityLogged.Clear();
    }

    [HarmonyPatch(nameof(NitoriTelephoneComponent.GetMultiBuyMerchantBtnConfig))]
    [HarmonyPrefix]
    public static void GetMultiBuyMerchantBtnConfig_Prefix()
    {
        _merchantContactFlowActive = false;
        _soldOutVisibilityLogged.Clear();
    }

    [HarmonyPatch(nameof(NitoriTelephoneComponent.OpenSpecialNPCMapSelectionMenu))]
    [HarmonyPrefix]
    public static void OpenSpecialNPCMapSelectionMenu_Prefix()
    {
        _merchantContactFlowActive = false;
        _soldOutVisibilityLogged.Clear();
    }

    [HarmonyPatch(nameof(NitoriTelephoneComponent.OpenMerchantNPCMapSelectionMenu))]
    [HarmonyPrefix]
    public static void OpenMerchantNPCMapSelectionMenu_Prefix()
    {
        _merchantContactFlowActive = true;
    }

    [HarmonyPatch(nameof(NitoriTelephoneComponent.OpenMerchantNPCSelectionMenu))]
    [HarmonyPrefix]
    public static void OpenMerchantNPCSelectionMenu_Prefix()
    {
        _merchantContactFlowActive = true;
    }

    [HarmonyPatch(nameof(NitoriTelephoneComponent.CheckExtraMerchantDoSell))]
    [HarmonyPostfix]
    public static void KeepSoldOutResourceExMerchantContactVisible_Postfix(string characterLabel, ref bool __result)
    {
        if (!_merchantContactFlowActive || __result)
            return;

        if (ResourceExManager.IsTelephoneMerchant(characterLabel))
        {
            __result = true;
            if (_soldOutVisibilityLogged.Add(characterLabel))
                Log.Info($"Keep sold-out ResourceEx telephone merchant visible in contact menu: {characterLabel}");
        }
    }

    [HarmonyPatch(nameof(NitoriTelephoneComponent._OpenMerchantNPCMapSelectionMenu_b__19_0))]
    [HarmonyPostfix]
    public static void KeepResourceExMerchantMapVisible_Postfix(string x, ref bool __result)
    {
        if (!__result && ResourceExManager.HasTelephoneMerchantOnMap(x))
            __result = true;
    }

    [HarmonyPatch(nameof(NitoriTelephoneComponent._OpenMerchantNPCSelectionMenu_b__22_2))]
    [HarmonyPostfix]
    public static void KeepResourceExMerchantVisible_Postfix(
        NitoriTelephoneComponent __instance,
        ExtraMerchantData x,
        ref bool __result)
    {
        if (__result)
            return;

        if (x == null)
            return;

        if (ResourceExManager.IsTelephoneMerchantOnMap(x.merchantKey, __instance?.currentMapLabel))
        {
            __result = true;
            Log.Info($"Keep ResourceEx telephone merchant in contact selection: {x.merchantKey} map={x.merchantMapLabel} current={__instance?.currentMapLabel}");
        }
    }

    private static void InjectResourceExMerchants(NitoriTelephoneComponent telephone)
    {
        var exEntries = ResourceExManager.GetAllTelephoneMerchantEntries()
            .Where(entry => !string.IsNullOrWhiteSpace(entry.key))
            .ToList();

        if (exEntries.Count == 0)
            return;

        PatchMapLabels(telephone, exEntries.Select(entry => entry.mapLabel));
        PatchExtraMerchantData(telephone, exEntries);
    }

    private static void PatchMapLabels(NitoriTelephoneComponent telephone, IEnumerable<string> extraMapLabels)
    {
        var mapLabels = (telephone.mapLabel ?? new Il2CppStringArray(0)).ToArray().ToList();
        var existingMapLabels = mapLabels.ToHashSet();
        var changed = false;

        foreach (var mapLabel in extraMapLabels)
        {
            if (string.IsNullOrWhiteSpace(mapLabel))
                continue;

            if (!existingMapLabels.Add(mapLabel))
                continue;

            mapLabels.Add(mapLabel);
            changed = true;
        }

        if (!changed)
            return;

        telephone.mapLabel = new Il2CppStringArray(mapLabels.ToArray());
        Log.Info($"Injected ResourceEx telephone map labels: {string.Join(", ", mapLabels)}");
    }

    private static void PatchExtraMerchantData(
        NitoriTelephoneComponent telephone,
        IReadOnlyCollection<(string key, string mapLabel)> exEntries)
    {
        var existing = (telephone.extraMerchantData ?? new Il2CppReferenceArray<ExtraMerchantData>(0)).ToArray().ToList();
        var existingKeys = existing
            .Where(data => data != null)
            .Select(data => data.merchantKey)
            .ToHashSet();

        var addedCount = 0;
        foreach (var (key, mapLabel) in exEntries)
        {
            if (existingKeys.Contains(key))
                continue;

            existing.Add(ResourceExManager.ToTelephoneExtraMerchantData(key, mapLabel));
            existingKeys.Add(key);
            addedCount++;
        }

        if (addedCount == 0)
            return;

        telephone.extraMerchantData = new Il2CppReferenceArray<ExtraMerchantData>(existing.ToArray());
        Log.Info($"Injected {addedCount} ResourceEx telephone merchant entries.");
    }
}
