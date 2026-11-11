using System.Threading.Channels;

namespace MetaMystia.Network;

public enum ServerCommand { Kick, Leave, MaxPlayers }
public sealed record ServerPlayer(int Uid, string Name, ushort Room);
public sealed record ServerStatus(int MaxPlayers, ServerPlayer[] Players, RoomSummary[] Rooms)
{
    public ServerNotice? Retirement { get; init; }
}

public sealed partial class Server
{
    public async Task<ServerStatus> GetStatusAsync()
    {
        if (Volatile.Read(ref stopping) != 0 || Volatile.Read(ref started) == 0) throw new InvalidOperationException("Not running");
        var result = new TaskCompletionSource<ServerStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        await events.Writer.WriteAsync(() => result.TrySetResult(new(maxPlayers,
            peers.Where(p => p.Player != null).Select(p => new ServerPlayer(p.Player!.Uid, p.Player.Name, p.Room)).ToArray(),
            Capture(null).Rooms) { Retirement = retirement })).ConfigureAwait(false);
        return await result.Task.ConfigureAwait(false);
    }

    /// <summary>独立服务端的本地管理入口，与网络消息在同一队列执行。</summary>
    public async Task<NetworkErrorCode> ManageAsync(ServerCommand command, int value)
    {
        if (options.LanKey != null) throw new InvalidOperationException("Standalone server only");
        if (Volatile.Read(ref stopping) != 0 || Volatile.Read(ref started) == 0) return NetworkErrorCode.ServerStopped;
        var result = new TaskCompletionSource<NetworkErrorCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await events.Writer.WriteAsync(() =>
            {
                var error = Volatile.Read(ref stopping) != 0 ? NetworkErrorCode.ServerStopped : Manage(command, value);
                if (error is not (NetworkErrorCode.None or NetworkErrorCode.ServerStopped))
                {
                    string action = command == ServerCommand.MaxPlayers ? $"将服务器人数上限调整为 {value} 人"
                        : command == ServerCommand.Kick ? $"将 UID={value} 的玩家踢出服务器" : $"将 UID={value} 的玩家移出房间";
                    Log(ServerLogLevel.Warning, $"[管理] 管理员无法{action}，因为{ServerLogEntry.Reason(error)}。");
                }
                result.TrySetResult(error);
            }).ConfigureAwait(false);
        }
        catch (ChannelClosedException) { return NetworkErrorCode.ServerStopped; }
        return await result.Task.ConfigureAwait(false);
    }

    public async Task<NetworkErrorCode> ReloadTextSettingsAsync(ServerTextSettings settings)
    {
        settings.Validate();
        var copy = settings with
        {
            ChatFilter = settings.ChatFilter with { Words = settings.ChatFilter.Words.ToArray() },
            WelcomeMessages = settings.WelcomeMessages.ToArray()
        };
        if (Volatile.Read(ref stopping) != 0 || Volatile.Read(ref started) == 0) return NetworkErrorCode.ServerStopped;
        var result = new TaskCompletionSource<NetworkErrorCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await events.Writer.WriteAsync(() =>
            {
                if (Volatile.Read(ref stopping) != 0) { result.TrySetResult(NetworkErrorCode.ServerStopped); return; }
                ApplyTextSettings(copy);
                Log(ServerLogLevel.Info, $"[管理] 管理员重新加载了配置，当前有 {welcomeMessages.Length} 条欢迎公告和 {chatWords.Length} 个有效过滤词条，聊天正文记录已{(logChat ? "开启" : "关闭")}。");
                result.TrySetResult(NetworkErrorCode.None);
            }).ConfigureAwait(false);
        }
        catch (ChannelClosedException) { return NetworkErrorCode.ServerStopped; }
        return await result.Task.ConfigureAwait(false);
    }

    private void ApplyTextSettings(ServerTextSettings settings)
    {
        chatWords = settings.ChatFilter.Enabled
            ? settings.ChatFilter.Words.Where(w => !string.IsNullOrWhiteSpace(w)).Distinct().ToArray() : [];
        chatComparison = settings.ChatFilter.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        logChat = settings.LogChat;
        welcomeMessages = settings.WelcomeMessages.Select(message => Protocol.Pack(new ChatPayload { Message = message })).ToArray();
    }

    public async Task<NetworkErrorCode> SayAsync(string message)
    {
        if (Volatile.Read(ref stopping) != 0 || Volatile.Read(ref started) == 0) return NetworkErrorCode.ServerStopped;
        if (string.IsNullOrWhiteSpace(message) || message.Length > ChatPayload.MaxLength) return NetworkErrorCode.InvalidChat;
        var body = Protocol.Pack(new ChatPayload { Message = message });
        var result = new TaskCompletionSource<NetworkErrorCode>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await events.Writer.WriteAsync(() =>
            {
                var error = Volatile.Read(ref stopping) != 0 ? NetworkErrorCode.ServerStopped : CheckChat(body, out _);
                if (error == NetworkErrorCode.None)
                {
                    // UID 0 保留给服务端；客户端发送者身份始终由连接覆盖。
                    var frame = new Frame(Kind.Data, body, Type: (ushort)GameMessageType.Chat, Route: Route.World);
                    foreach (var peer in peers.Where(p => p.Player != null && !p.Rejected)) peer.Wire.Send(frame);
                    if (logChat) Log(ServerLogLevel.Chat, $"[聊天] 服务器说：{ServerLogEntry.Quote(message)}");
                }
                if (error == NetworkErrorCode.None) Log(ServerLogLevel.Info, "[管理] 管理员提交了一条服务器广播。");
                else if (error != NetworkErrorCode.ServerStopped)
                    Log(ServerLogLevel.Warning, $"[管理] 服务端未发送广播，因为{ServerLogEntry.Reason(error)}。");
                result.TrySetResult(error);
            }).ConfigureAwait(false);
        }
        catch (ChannelClosedException) { return NetworkErrorCode.ServerStopped; }
        return await result.Task.ConfigureAwait(false);
    }

    private NetworkErrorCode Manage(ServerCommand command, int value)
    {
        if (command == ServerCommand.MaxPlayers)
        {
            if (value < 1 || value > 256) return NetworkErrorCode.InvalidLimit;
            int previous = maxPlayers;
            maxPlayers = value;
            Log(ServerLogLevel.Info, $"[管理] 管理员将服务器人数上限从 {previous} 人调整为 {value} 人，现有连接不受影响。");
            Publish();
            return NetworkErrorCode.None;
        }
        if (command is not (ServerCommand.Kick or ServerCommand.Leave)) return NetworkErrorCode.InvalidMessage;
        var peer = peers.FirstOrDefault(p => p.Player?.Uid == value && !p.Rejected);
        if (peer == null) return NetworkErrorCode.PlayerMissing;
        if (command == ServerCommand.Leave)
        {
            if (peer.Room == 0) return NetworkErrorCode.NotInRoom;
            Log(ServerLogLevel.Info, $"[管理] 管理员将{ServerLogEntry.Player(peer.Player!.Uid, peer.Player.Name)}移出了房间 {RoomCode.Format(peer.Room)}。");
            Leave(peer);
            Publish();
        }
        else
        {
            Log(ServerLogLevel.Info, $"[管理] 管理员将{ServerLogEntry.Player(peer.Player!.Uid, peer.Player.Name)}踢出了服务器。");
            Reject(peer, NetworkErrorCode.KickedByServer);
            Remove(peer, NetworkErrorCode.KickedByServer);
        }
        return NetworkErrorCode.None;
    }

    private void Log(ServerLogLevel level, string message)
    {
        try { Logged?.Invoke(new(level, message)); }
        catch (Exception e) { try { CallbackError?.Invoke(e); } catch { } }
    }
}
