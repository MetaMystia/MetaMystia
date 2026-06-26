namespace MetaMystia.Network;

[NetActionBehavior]
internal static class PlayerChangeSkinBehavior
{
    public static void Send(PlayerSkinData skin) =>
        new PlayerChangeSkinAction { Skin = skin }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<PlayerChangeSkinAction>(Handle);
    }

    private static void Handle(PlayerChangeSkinAction action)
    {
        if (!PlayerManager.PlayerTable.TryGetValue(action.SenderUid, out var peer))
            return;

        peer.Skin = action.Skin;
        peer.UpdateCharacterSprite();

        if (!string.IsNullOrEmpty(action.Skin?.NetSkinName))
            NetSkinManager.RequestSkin(action.Skin.NetSkinName);
    }
}
