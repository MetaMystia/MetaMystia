namespace MetaMystia;

/// <summary>游戏内白天/选店地图标识；MapKey 与 <c>SceneDirector</c> / <c>PrimaryName</c> 一致。</summary>
/// <remarks>mod 自有枚举（非游戏类型），作为线协议的一部分驻留协议层。数值即线值，只增不改。</remarks>
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
