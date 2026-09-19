namespace MetaMystia;

public enum MapLabel : int
{
    Unknown = -1,

    Home = 0,
    Basement = 1,
    BeastForest = 2,
    HumanVillage = 3,
    HakureiShrine = 4,
    ScarletMansion = 5,
    BambooForest = 6,
    PartyStage = 7,
    Hakugyokurou = 8,

    DLC1_MagicForest = 1000,
    DLC1_YoukaiMountain = 1001,
    DLC2_FormerHell = 2000,
    DLC2_EarthSpiritsPalace = 2001,
    DLC3_MyourenTemple = 3000,
    DLC3_DivineSpiritMausoleum = 3001,
    DLC3_HakureiFestival = 3002,
    DLC4_GardenOfTheSun = 4000,
    DLC4_ShiningNeedleCastle = 4001,
    DLC4_ScarletMansionBasement = 4002,
    DLC5_Makai = 5000,
    DLC5_LunarCapital = 5001,
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
