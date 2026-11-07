using HarmonyLib;

using GameData.Core.Collections.NightSceneUtility;

namespace MetaMystia.Patch;

[HarmonyPatch(typeof(DataBaseNight))]
public class DataBaseNightPatch
{
    /// <summary>
    /// DataBaseNight.Initialize 会用资源包数据整体重建符卡字典，
    /// 自定义符卡必须在它之后写入，否则会被覆盖掉。
    /// </summary>
    [HarmonyPatch(nameof(DataBaseNight.Initialize))]
    [HarmonyPostfix]
    public static void Initialize_Postfix()
    {
        ResourceExManager.OnDataBaseNightInitialized();
    }
}
