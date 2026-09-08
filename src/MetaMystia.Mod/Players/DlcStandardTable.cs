using System;

using GameData.RunTime.Common;

namespace MetaMystia;

/// <summary>
/// 本地 DLC 标识位，用于判断当前游戏资源
/// </summary>
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

// 仅用于本地 DLC 检测和资源包加载，不参与网络资源清单。
public static class DlcStandardTable
{
    /// <summary>
    /// 根据 DLC Key（如 "DLC1"）判断所属 DLC
    /// </summary>
    public static DlcPack KeyToDlc(string key) => key switch
    {
        _ when key == PlayerSaveFile.CORE_DATA_DLC_KEY => DlcPack.Core,
        _ when key == PlayerSaveFile.DLC1_DATA_DLC_KEY => DlcPack.Dlc1,
        _ when key == PlayerSaveFile.DLC2_DATA_DLC_KEY => DlcPack.Dlc2,
        _ when key == PlayerSaveFile.DLCMUSIC_DATA_DLC_KEY => DlcPack.DlcMusic,
        _ when key == PlayerSaveFile.DLC3_DATA_DLC_KEY => DlcPack.Dlc3,
        _ when key == PlayerSaveFile.DLC4_DATA_DLC_KEY => DlcPack.Dlc4,
        _ when key == PlayerSaveFile.DLC5_DATA_DLC_KEY => DlcPack.Dlc5,
        _ => DlcPack.None,
    };

}
