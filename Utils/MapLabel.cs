using System;
using System.Collections.Generic;
using MetaMystia.UI;

namespace MetaMystia;

/// <summary>游戏内白天/选店地图标识；MapKey 与 <c>SceneDirector</c> / <c>PrimaryName</c> 一致。</summary>
public enum MapLabel : ushort
{
    Unknown = 0,

    Home = 1,
    Basement = 2,
    BeastForest = 3,
    HumanVillage = 4,
    HakureiShrine = 5,
    ScarletMansion = 6,
    BambooForest = 7,
    PartyStage = 8,
    Hakugyokurou = 9,

    DLC1_MagicForest = 10,
    DLC1_YoukaiMountain = 11,
    DLC2_FormerHell = 12,
    DLC2_EarthSpiritsPalace = 13,
    DLC3_MyourenTemple = 14,
    DLC3_DivineSpiritMausoleum = 15,
    DLC3_HakureiFestival = 16,
    DLC4_GardenOfTheSun = 17,
    DLC4_ShiningNeedleCastle = 18,
    DLC4_ScarletMansionBasement = 19,
    DLC5_Makai = 20,
    DLC5_LunarCapital = 21,
}

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
