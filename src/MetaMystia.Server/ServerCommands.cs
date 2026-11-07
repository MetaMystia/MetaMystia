using System.Text.Json;

using MetaMystia.Network;

namespace MetaMystia.Hosting;

internal static class ServerCommands
{
    // 返回 false 表示停止服务端；空行不执行任何操作。
    public static async Task<bool> Execute(string line, Server server, Action<ServerLogLevel, string> write, string? configDirectory = null)
    {
        var args = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0) return true;
        string command = args[0].TrimStart('/').ToLowerInvariant();
        switch (command)
        {
            case "help" when args.Length == 1:
                write(ServerLogLevel.Info, "help | players | rooms | kick <uid> | leave <uid> | maxplayers [1–256] | say <内容> | reload | stop");
                break;
            case "reload" when args.Length == 1:
                try
                {
                    var settings = ServerConfiguration.Load(configDirectory ?? AppContext.BaseDirectory);
                    var reloadError = await server.ReloadTextSettingsAsync(settings);
                    write(reloadError == NetworkErrorCode.None ? ServerLogLevel.Info : ServerLogLevel.Warning,
                        reloadError == NetworkErrorCode.None ? "公告、聊天日志开关和敏感词配置已生效。" : "服务器正在关闭，配置未应用。");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
                {
                    write(ServerLogLevel.Warning, $"配置重载失败，保留原配置：{ServerLogEntry.Quote(e.Message)}");
                }
                break;
            case "stop" when args.Length == 1:
                write(ServerLogLevel.Info, "收到 stop 命令。");
                return false;
            case "say":
                string message = line.TrimStart()[args[0].Length..].TrimStart();
                var chatError = await server.SayAsync(message);
                write(chatError == NetworkErrorCode.None ? ServerLogLevel.Info : ServerLogLevel.Warning, chatError switch
                {
                    NetworkErrorCode.None => "服务器消息已提交广播。",
                    NetworkErrorCode.ChatFiltered => "消息包含敏感词，未发送。",
                    NetworkErrorCode.ServerStopped => "服务器正在关闭。",
                    _ => "用法：say <内容>，内容不能为空且不能超过 1024 个 UTF-16 单元。"
                });
                break;
            case "players" when args.Length == 1:
                var players = await server.GetStatusAsync();
                write(ServerLogLevel.Info, $"在线玩家 {players.Players.Length}/{players.MaxPlayers}");
                foreach (var player in players.Players.OrderBy(p => p.Uid))
                    write(ServerLogLevel.Info, $"uid={player.Uid} name={ServerLogEntry.Quote(player.Name)} room={(player.Room == 0 ? "大厅" : RoomCode.Format(player.Room))}");
                break;
            case "rooms" when args.Length == 1:
                var rooms = (await server.GetStatusAsync()).Rooms;
                write(ServerLogLevel.Info, $"房间数 {rooms.Length}");
                foreach (var room in rooms.OrderBy(r => r.Id))
                    write(ServerLogLevel.Info, $"room={RoomCode.Format(room.Id)} host={room.Host}，人数 {room.Count}/{room.MaxPlayers}，{(room.Joinable ? "开放加入" : "关闭加入")}");
                break;
            case "maxplayers" when args.Length == 1:
                write(ServerLogLevel.Info, $"服务器人数上限 {(await server.GetStatusAsync()).MaxPlayers}");
                break;
            case "kick":
            case "leave":
            case "maxplayers":
                if (args.Length != 2 || !int.TryParse(args[1], out int value) || value < 1)
                {
                    write(ServerLogLevel.Warning, command == "maxplayers" ? "用法：maxplayers <1–256>" : $"用法：{command} <正整数 UID>");
                    break;
                }
                var operation = command == "kick" ? ServerCommand.Kick : command == "leave" ? ServerCommand.Leave : ServerCommand.MaxPlayers;
                var error = await server.ManageAsync(operation, value);
                string result = error switch
                {
                    NetworkErrorCode.None => command == "maxplayers" ? $"服务器人数上限已设为 {value}，保留在线玩家。"
                        : command == "kick" ? $"已断开玩家 uid={value}。" : $"已将玩家 uid={value} 移出房间；房主离开时房间解散。",
                    NetworkErrorCode.PlayerMissing => $"玩家 uid={value} 不在线。",
                    NetworkErrorCode.NotInRoom => $"玩家 uid={value} 已在大厅。",
                    NetworkErrorCode.InvalidLimit => "人数上限必须为 1–256。",
                    NetworkErrorCode.ServerStopped => "服务器正在关闭。",
                    _ => $"操作失败：{error}"
                };
                write(error == NetworkErrorCode.None ? ServerLogLevel.Info : ServerLogLevel.Warning, result);
                break;
            default:
                write(ServerLogLevel.Warning, "未知命令或参数数量错误，输入 help 查看帮助。");
                break;
        }
        return true;
    }
}
