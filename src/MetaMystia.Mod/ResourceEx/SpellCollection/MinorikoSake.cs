using System.Linq;

using GameData.Core.Collections;
using GameData.RunTime.Common;
using NightScene.EventUtility;

using MetaMystia.Patch;

namespace MetaMystia.ResourceEx.SpellCollection;

/// <summary>丰穣结界期间按清酒 tag 提供无限酒水，不记录单瓶来源。</summary>
internal static class MinorikoSake
{
    private const int SakeTag = 6;
    private static EventManager _manager;
    internal static bool IsActive => _manager != null && _manager.CheckTimedBuffExists(Spell_Minoriko.HarvestBuff);

    internal static bool IsUnlimited(Sellable beverage) => IsActive
        && beverage != null && beverage.Type == Sellable.SellableType.Beverage
        && beverage.Id != RunTimeStorage.GREEN_TEA_ID && beverage.Tags.Contains(SakeTag);

    internal static void SetActive(bool active)
    {
        _manager = active ? EventManager.Instance : null;
        var panel = WorkSceneStoragePannelPatch.instanceRef;
        if (panel == null || panel.openType != Sellable.SellableType.Beverage)
            return;
        panel.UpdateBevField();
        panel.ActiveInStorageGroup.UpdateElementsAndReselect();
    }

}
