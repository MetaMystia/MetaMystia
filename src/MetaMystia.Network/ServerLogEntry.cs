using System.Text.Encodings.Web;
using System.Text.Json;

namespace MetaMystia.Network;

public enum ServerLogLevel { Info, Warning, Error, Chat }
public sealed record ServerLogEntry(ServerLogLevel Level, string Message)
{
    private static readonly JsonSerializerOptions json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    // 保留中文，转义换行和终端控制字符，避免玩家文本伪造日志行。
    public static string Quote(string text) => JsonSerializer.Serialize(text, json);

    public static string Player(int uid, string name) => $"玩家{Quote(name)}(UID={uid})";

    public static string Reason(NetworkErrorCode code) => $"{code switch
    {
        NetworkErrorCode.None => "操作成功",
        NetworkErrorCode.ProtocolMismatch => "协议版本不一致",
        NetworkErrorCode.GameMismatch => "游戏版本不一致",
        NetworkErrorCode.ModMismatch => "模组版本不一致",
        NetworkErrorCode.ServerFull => "服务器人数已满",
        NetworkErrorCode.RoomFull => "房间人数已满",
        NetworkErrorCode.JoinClosed => "房间已关闭加入",
        NetworkErrorCode.PlayerNotAvailable => "玩家当前状态不允许此操作",
        NetworkErrorCode.HostPreparing => "房主正在准备",
        NetworkErrorCode.RoomMissing => "房间不存在",
        NetworkErrorCode.RoomEnded => "房间已结束",
        NetworkErrorCode.StaleRoom => "房间状态已过期",
        NetworkErrorCode.HostOnly => "仅房主可以执行此操作",
        NetworkErrorCode.InvalidLimit => "人数上限无效",
        NetworkErrorCode.DuplicateName => "玩家名字重复",
        NetworkErrorCode.WorldDataBudgetExceeded => "玩家资料总量超出限制",
        NetworkErrorCode.ResourcesNotReadyOrInvalid => "资源未就绪或无效",
        NetworkErrorCode.InvalidHello => "握手信息无效",
        NetworkErrorCode.ReceiveTimeout => "接收消息超时",
        NetworkErrorCode.HandshakeTimeout => "握手超时",
        NetworkErrorCode.JoinTimeout => "加入房间超时",
        NetworkErrorCode.ManagementUnconfirmed => "管理操作未确认",
        NetworkErrorCode.CancelUnconfirmed => "取消操作未确认",
        NetworkErrorCode.LeaveUnconfirmed => "离房操作未确认",
        NetworkErrorCode.SendQueueFull => "发送队列已满",
        NetworkErrorCode.ReceiveQueueFull => "接收队列已满",
        NetworkErrorCode.ServerQueueFull => "服务端处理队列已满",
        NetworkErrorCode.TooManyRequests => "请求过于频繁",
        NetworkErrorCode.ConnectionLost => "连接丢失",
        NetworkErrorCode.ConnectFailed => "连接失败",
        NetworkErrorCode.AddressUnavailable => "地址不可用",
        NetworkErrorCode.Disconnected => "连接已断开",
        NetworkErrorCode.ServerStopped => "服务端已停止",
        NetworkErrorCode.LeftDefaultRoom => "玩家已离开默认房间",
        NetworkErrorCode.Finished => "连接已结束",
        NetworkErrorCode.InvalidMessage => "消息无效",
        NetworkErrorCode.AlreadyInRoom => "玩家已在房间中",
        NetworkErrorCode.DefaultRoomOnly => "仅允许使用默认房间",
        NetworkErrorCode.PlayerMissing => "目标玩家不存在",
        NetworkErrorCode.UidExhausted => "玩家 UID 已耗尽",
        NetworkErrorCode.SendFailed => "发送消息失败",
        NetworkErrorCode.LeftDuringJoin => "玩家在加入过程中离开",
        NetworkErrorCode.InvalidServerMessage => "服务端消息无效",
        NetworkErrorCode.ChatFiltered => "消息包含敏感词",
        NetworkErrorCode.InvalidChat => "聊天内容无效",
        NetworkErrorCode.KickedByServer => "管理员踢出",
        NetworkErrorCode.NotInRoom => "玩家当前不在房间中",
        NetworkErrorCode.ServerRetiring => "服务端正在退役",
        NetworkErrorCode.VersionUnavailable => "目标版本不可用",
        _ => "未知原因"
    }}（{code}）";
}
