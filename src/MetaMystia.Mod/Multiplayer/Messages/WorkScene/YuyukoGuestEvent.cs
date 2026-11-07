namespace MetaMystia.Multiplayer.Messages;

/// <summary>幽幽子本体同步消息的事件类型；数值属于网络协议，已有成员不得重排。</summary>
public enum YuyukoGuestEvent
{
    Bind,
    Order,
    Evaluate,
    Complete,
    Clear,
    Phase,
    Swallow,
}
