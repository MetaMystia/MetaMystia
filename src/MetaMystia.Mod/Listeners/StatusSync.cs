using Mystia.Listeners;

namespace MetaMystia.Listeners;

/// <summary>
/// 玩家皮肤切换后的本地外观刷新（原 <c>RunTimeAlbumPatch.ChangePlayerSkin_Postfix</c>）。
/// 装潢皮肤解析（<c>Izakaya.GetMap</c>）没有旧补丁对应行为，保持接口默认空实现。
/// </summary>
[AutoLog]
public sealed partial class StatusSync : IStatusListener
{
    public void OnPlayerSkinChanged(int skinId)
    {
        Log.Info($"Player skin changed to {skinId}");
        PlayerManager.Local.IsCustomSkinOverride = false;
        PlayerManager.InitLocalSkin();
        PlayerProfile.SendProfile();
    }
}
