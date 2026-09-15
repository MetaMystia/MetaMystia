namespace MetaMystia;

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

[Flags]
public enum DlcPack : byte
{
    None = 0,
    Core = 1 << 0,
    Dlc1 = 1 << 1,
    Dlc2 = 1 << 2,
    DlcMusic = 1 << 3,
    Dlc3 = 1 << 4,
    Dlc4 = 1 << 5,
    Dlc5 = 1 << 6,

    All = Core | Dlc1 | Dlc2 | DlcMusic | Dlc3 | Dlc4 | Dlc5,
}

public enum ResourceCategory
{
    Foods, Recipes, Beverages, Ingredients, Cookers, Items, Izakayas, SpecialGuests, NormalGuests,
    _Count
}
