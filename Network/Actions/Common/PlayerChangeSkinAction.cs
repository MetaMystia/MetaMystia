using MemoryPack;

using GameData.Core.Collections.CharacterUtility;

using SgrYuki;

namespace MetaMystia.Network;

/// <summary>
/// 皮肤变更网络同步 Action。
/// 当玩家通过 /skin 命令更改皮肤时，广播给所有其他玩家。
/// </summary>
[MemoryPackable]
[AutoLog]
[PublicRelay]
public partial class PlayerChangeSkinAction : Action
{
    public PlayerSkin Skin { get; set; }

    public override void OnReceivedDerived()
    {
        if (!PlayerManager.TryGetVisiblePeer(SenderUid, out var peer))
        {
            return;
        }

        peer.Skin = Skin;
        peer.UpdateCharacterSprite();
        // 如果对端使用了网络皮肤，本地也应该从服务器拉取（完成后会自动刷新）
        if (!string.IsNullOrEmpty(Skin?.NetSkinName))
            NetSkinManager.RequestSkin(Skin.NetSkinName);
    }

    public static void Send(PlayerSkin skin) =>
        new PlayerChangeSkinAction { Skin = skin }.Enqueue();
}
