using MemoryPack;

namespace MetaMystia.Multiplayer.Messages;

/// <summary>
/// 玩家 → 所有玩家：播放表情
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class EmoteMessage : MultiplayerMessage
{
    public EmoteType Emote { get; set; }

    [DiscardOnStory]
    public override void OnReceivedDerived()
    {
        if (PlayerManager.TryGetVisiblePeer(SenderUid, out var peer) && peer.IsSameMapAsLocal)
            MetaMystia.Emote.Play(peer.GetCharacterUnit(), Emote);
    }

    public static void Send(EmoteType emote) => new EmoteMessage { Emote = emote }.Enqueue();
}
