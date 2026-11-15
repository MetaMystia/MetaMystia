using System.Threading.Channels;

namespace MetaMystia.Network;

public sealed partial class Server
{
    private ServerNotice? retirement;
    private readonly TaskCompletionSource<bool> drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task Drained => drained.Task;

    public async Task<NetworkErrorCode> DrainAsync(string noticeId, string targetModVersion)
    {
        if (options.LanKey != null) throw new InvalidOperationException("Standalone server only");
        if (string.IsNullOrWhiteSpace(noticeId) || noticeId.Length > 128 ||
            string.IsNullOrWhiteSpace(targetModVersion) || targetModVersion.Length > 64)
            throw new ArgumentException("Invalid retirement notice");
        if (Volatile.Read(ref stopping) != 0 || Volatile.Read(ref started) == 0) return NetworkErrorCode.ServerStopped;
        var result = new TaskCompletionSource<NetworkErrorCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await events.Writer.WriteAsync(() =>
            {
                if (Volatile.Read(ref stopping) != 0) { result.TrySetResult(NetworkErrorCode.ServerStopped); return; }
                if (retirement?.NoticeId != noticeId)
                {
                    retirement = new() { Code = ServerNoticeCode.Retiring, NoticeId = noticeId, TargetModVersion = targetModVersion };
                    var frame = new Frame(Kind.ServerNotice, Protocol.Pack(retirement));
                    foreach (var peer in peers.Where(p => p.Player != null && !p.Rejected)) peer.Wire.Send(frame);
                    Log(ServerLogLevel.Info, $"[系统] 服务端已进入退役状态，目标模组版本为 {ServerLogEntry.Quote(targetModVersion)}。");
                }
                CheckDrained();
                result.TrySetResult(NetworkErrorCode.None);
            }).ConfigureAwait(false);
        }
        catch (ChannelClosedException) { return NetworkErrorCode.ServerStopped; }
        return await result.Task.ConfigureAwait(false);
    }

    private void CheckDrained()
    {
        if (retirement != null && !peers.Any(p => p.Player != null && !p.Rejected)) drained.TrySetResult(true);
    }
}
