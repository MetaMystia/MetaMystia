using System;
using System.Collections.Generic;
using MetaMystia.UI;

namespace MetaMystia;

// MapLabel 枚举本体位于 src/MetaMystia.Protocol/Enums/MapLabel.cs（协议层）。
// 此处仅保留依赖游戏 TextId 的展示/解析扩展，属行为侧，留 mod。

public static class MapLabelExtensions
{
    private static readonly Dictionary<string, MapLabel> MapKeyToLabel = new(StringComparer.Ordinal)
    {
        ["Home"] = MapLabel.Home,
        ["Basement"] = MapLabel.Basement,
        ["BeastForest"] = MapLabel.BeastForest,
        ["HumanVillage"] = MapLabel.HumanVillage,
        ["HakureiShrine"] = MapLabel.HakureiShrine,
        ["ScarletMansion"] = MapLabel.ScarletMansion,
        ["BambooForest"] = MapLabel.BambooForest,
        ["PartyStage"] = MapLabel.PartyStage,
        ["Hakugyokurou"] = MapLabel.Hakugyokurou,
        ["DLC1_MagicForest"] = MapLabel.DLC1_MagicForest,
        ["DLC1_YoukaiMountain"] = MapLabel.DLC1_YoukaiMountain,
        ["DLC2_FormerHell"] = MapLabel.DLC2_FormerHell,
        ["DLC2_EarthSpiritsPalace"] = MapLabel.DLC2_EarthSpiritsPalace,
        ["DLC3_MyourenTemple"] = MapLabel.DLC3_MyourenTemple,
        ["DLC3_DivineSpiritMausoleum"] = MapLabel.DLC3_DivineSpiritMausoleum,
        ["DLC3_HakureiFestival"] = MapLabel.DLC3_HakureiFestival,
        ["DLC4_GardenOfTheSun"] = MapLabel.DLC4_GardenOfTheSun,
        ["DLC4_ShiningNeedleCastle"] = MapLabel.DLC4_ShiningNeedleCastle,
        ["DLC4_ScarletMansionBasement"] = MapLabel.DLC4_ScarletMansionBasement,
        ["DLC5_Makai"] = MapLabel.DLC5_Makai,
        ["DLC5_LunarCapital"] = MapLabel.DLC5_LunarCapital,
    };

    public static bool IsSelected(this MapLabel label) => label != MapLabel.Unknown;

    /// <summary>解析游戏 MapKey；空为 <see cref="MapLabel.Unknown"/> 且返回 false。</summary>
    public static bool TryFromMapKey(string mapKey, out MapLabel label)
    {
        if (string.IsNullOrEmpty(mapKey))
        {
            label = MapLabel.Unknown;
            return false;
        }

        if (MapKeyToLabel.TryGetValue(mapKey, out label))
            return true;

        label = MapLabel.Unknown;
        return false;
    }

    public static MapLabel FromMapKey(string mapKey)
    {
        TryFromMapKey(mapKey, out var label);
        return label;
    }

    public static string ToMapKey(this MapLabel label) =>
        label == MapLabel.Unknown ? "" : label.ToString();

    public static string GetDisplayName(this MapLabel label) => label switch
    {
        MapLabel.Home => TextId.MapLabel_Home.Get(),
        MapLabel.Basement => TextId.MapLabel_Basement.Get(),
        MapLabel.BeastForest => TextId.MapLabel_BeastForest.Get(),
        MapLabel.HumanVillage => TextId.MapLabel_HumanVillage.Get(),
        MapLabel.HakureiShrine => TextId.MapLabel_HakureiShrine.Get(),
        MapLabel.ScarletMansion => TextId.MapLabel_ScarletMansion.Get(),
        MapLabel.BambooForest => TextId.MapLabel_BambooForest.Get(),
        MapLabel.PartyStage => TextId.MapLabel_PartyStage.Get(),
        MapLabel.Hakugyokurou => TextId.MapLabel_Hakugyokurou.Get(),
        MapLabel.DLC1_MagicForest => TextId.MapLabel_DLC1_MagicForest.Get(),
        MapLabel.DLC1_YoukaiMountain => TextId.MapLabel_DLC1_YoukaiMountain.Get(),
        MapLabel.DLC2_FormerHell => TextId.MapLabel_DLC2_FormerHell.Get(),
        MapLabel.DLC2_EarthSpiritsPalace => TextId.MapLabel_DLC2_EarthSpiritsPalace.Get(),
        MapLabel.DLC3_MyourenTemple => TextId.MapLabel_DLC3_MyourenTemple.Get(),
        MapLabel.DLC3_DivineSpiritMausoleum => TextId.MapLabel_DLC3_DivineSpiritMausoleum.Get(),
        MapLabel.DLC3_HakureiFestival => TextId.MapLabel_DLC3_HakureiFestival.Get(),
        MapLabel.DLC4_GardenOfTheSun => TextId.MapLabel_DLC4_GardenOfTheSun.Get(),
        MapLabel.DLC4_ShiningNeedleCastle => TextId.MapLabel_DLC4_ShiningNeedleCastle.Get(),
        MapLabel.DLC4_ScarletMansionBasement => TextId.MapLabel_DLC4_ScarletMansionBasement.Get(),
        MapLabel.DLC5_Makai => TextId.MapLabel_DLC5_Makai.Get(),
        MapLabel.DLC5_LunarCapital => TextId.MapLabel_DLC5_LunarCapital.Get(),
        _ => TextId.MapLabel_Unknown.Get(),
    };

    public static string FormatIzakayaSelection(this MapLabel mapLabel, int level) =>
        $"{mapLabel.GetDisplayName()} {level.GetMapLevelDisplayName()}";
}

public static class MapLevelExtensions
{
    public static string GetMapLevelDisplayName(this int level) => level switch
    {
        1 => TextId.MapLevel_Cart.Get(),
        2 => TextId.MapLevel_Cabin.Get(),
        3 => TextId.MapLevel_Izakaya.Get(),
        _ => TextId.MapLevel_Unknown.Get(level),
    };
}
