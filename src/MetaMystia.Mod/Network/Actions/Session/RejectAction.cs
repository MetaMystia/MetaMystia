using MemoryPack;

using MetaMystia.UI;

namespace MetaMystia.Network;

/// <summary>
/// 主机 → 客机：连接被拒绝，携带拒绝原因。客机收到后显示通知并断开。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class RejectAction : Action
{
    public TextId ReasonId { get; set; }
    public string[] ReasonArgs { get; set; } = [];

    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Warning;

    [ClientOnlyReceive]
    public override void OnReceivedDerived()
    {
        var reason = ReasonId.Get(ReasonArgs);
        Log.LogWarning($"Connection rejected: {reason}");
        InGameConsole.ShowPassiveFromAnyThread(reason);
        MpWire.DisconnectPeer();
    }

    /// <summary>
    /// 主机向指定客机发送拒绝消息，然后断开连接
    /// </summary>
    public static void SendAndDisconnect(int uid, TextId reasonId, params string[] args)
    {
        // 先断开再发 Reject 会导致 DirectTcp 找不到 targetUid；Reject 应在断开前入队，
        // 且 DirectTcp 对找不到的 targetUid 不得广播（否则会误伤所有在线客机）。
        new RejectAction { ReasonId = reasonId, ReasonArgs = args, WireTargetUid = uid }.Enqueue();
        MpWire.DisconnectClient(uid);
    }
}
