using System;

using MemoryPack;

namespace MetaMystia.Multiplayer.Actions;

/// <summary>客机向主机确认本体绑定；空会话和零编号表示请求补发绑定消息。</summary>
[MemoryPackable]
[AutoLog]
public partial class YuyukoGuestBoundAction : Action
{
    public Guid Session { get; set; }
    public int RuntimeId { get; set; }

    [HostOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived() => YuyukoGuestSync.ReceiveBound(SenderUid, Session, RuntimeId);

    public static void Send(Guid session, int runtimeId) =>
        new YuyukoGuestBoundAction { Session = session, RuntimeId = runtimeId }.Enqueue();
}
