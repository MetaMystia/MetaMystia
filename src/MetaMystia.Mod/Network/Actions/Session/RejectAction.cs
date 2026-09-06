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
        // IO 线程写出拒绝包后关闭连接，不能在此处入队后立即断开。
        new RejectAction { ReasonId = reasonId, ReasonArgs = args, WireTargetUid = uid }.Enqueue();
    }
}
