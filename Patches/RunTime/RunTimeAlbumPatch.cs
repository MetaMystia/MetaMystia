using HarmonyLib;

using GameData.RunTime.Common;
using MetaMystia.Network;
using MetaMystia.Network.Services;


namespace MetaMystia.Patch;

[HarmonyPatch(typeof(GameData.RunTime.Common.RunTimeAlbum))]
[AutoLog]
public partial class RunTimeAlbumPatch
{
    [HarmonyPatch(nameof(RunTimeAlbum.ChangePlayerSkin))]
    [HarmonyPostfix]
    public static void ChangePlayerSkin_Postfix(int skinSelectionInfo)
    {
        Log.Info($"Player skin changed to {skinSelectionInfo}");
        PlayerManager.Local.IsCustomSkinOverride = false;
        PlayerManager.InitLocalSkin();
        CommonServices.SendPlayerChangeSkin(PlayerManager.Local.Skin);
    }
}
