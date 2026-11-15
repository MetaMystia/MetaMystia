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
                write(ServerLogLevel.Info, "[命令] 可用命令：\n  players              查看在线玩家\n  rooms                查看房间\n  kick <UID>           踢出服务器\n  leave <UID>          移出房间\n  maxplayers [人数]    查看或修改人数上限（1–256）\n  say <内容>           广播服务器消息\n  reload               重载文本配置\n  stop                 关闭服务端");
                break;
            case "reload" when args.Length == 1:
                try
                {
                    var settings = ServerConfiguration.Load(configDirectory ?? AppContext.BaseDirectory);
                    var reloadError = await server.ReloadTextSettingsAsync(settings);
                    if (reloadError != NetworkErrorCode.None)
                        write(ServerLogLevel.Warning, "[管理] 服务端正在关闭，未应用新配置。");
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
                {
                    write(ServerLogLevel.Warning, $"[管理] 服务端无法重新加载配置，将继续使用原配置。原因：{ServerLogEntry.Quote(e.Message)}");
                }
                break;
            case "stop" when args.Length == 1:
                write(ServerLogLevel.Info, "[管理] 管理员通过 stop 命令请求关闭服务端。");
                return false;
            case "say":
                string message = line.TrimStart()[args[0].Length..].TrimStart();
                var chatError = await server.SayAsync(message);
                if (chatError is NetworkErrorCode.ServerStopped or NetworkErrorCode.InvalidChat) write(ServerLogLevel.Warning, chatError switch
                {
                    NetworkErrorCode.ServerStopped => "[管理] 服务端正在关闭，未执行此操作。",
                    _ => "[命令] say 的内容不能为空且不能超过 1024 个 UTF-16 单元。用法：say <内容>。"
                });
                break;
            case "players" when args.Length == 1:
                var players = await server.GetStatusAsync();
                string playerList = string.Join("\n", players.Players.OrderBy(p => p.Uid).Select(player =>
                    $"  {ServerLogEntry.Player(player.Uid, player.Name)}当前位于{(player.Room == 0 ? "大厅" : "房间 " + RoomCode.Format(player.Room))}。"));
                write(ServerLogLevel.Info, players.Players.Length == 0 ? "[查询] 当前没有在线玩家。"
                    : $"[查询] 服务器当前在线 {players.Players.Length}/{players.MaxPlayers} 人。\n{playerList}");
                break;
            case "rooms" when args.Length == 1:
                var rooms = (await server.GetStatusAsync()).Rooms;
                string roomList = string.Join("\n", rooms.OrderBy(r => r.Id).Select(room =>
                    $"  房间 {RoomCode.Format(room.Id)}的房主为 UID={room.Host}，现有 {room.Count}/{room.MaxPlayers} 人，已{(room.Joinable ? "开放" : "关闭")}加入。"));
                write(ServerLogLevel.Info, rooms.Length == 0 ? "[查询] 当前没有房间。" : $"[查询] 服务器当前共有 {rooms.Length} 个房间。\n{roomList}");
                break;
            case "maxplayers" when args.Length == 1:
                write(ServerLogLevel.Info, $"[查询] 服务器最多允许 {(await server.GetStatusAsync()).MaxPlayers} 名玩家连接。");
                break;
            case "kick":
            case "leave":
            case "maxplayers":
                if (args.Length != 2 || !int.TryParse(args[1], out int value) || value < 1)
                {
                    write(ServerLogLevel.Warning, command == "maxplayers" ? "[命令] maxplayers 参数无效。用法：maxplayers <1–256>。" : $"[命令] {command} 参数无效。用法：{command} <正整数 UID>。");
                    break;
                }
                var operation = command == "kick" ? ServerCommand.Kick : command == "leave" ? ServerCommand.Leave : ServerCommand.MaxPlayers;
                var error = await server.ManageAsync(operation, value);
                if (error == NetworkErrorCode.ServerStopped)
                    write(ServerLogLevel.Warning, "[管理] 服务端正在关闭，未执行此操作。");
                break;
            default:
                write(ServerLogLevel.Warning, $"[命令] 服务端无法执行命令 {ServerLogEntry.Quote(command)}，因为命令不存在或参数数量错误。输入 help 查看帮助。");
                break;
        }
        return true;
    }
}
