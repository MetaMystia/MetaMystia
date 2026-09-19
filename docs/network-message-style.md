# 网络消息规范

连接、资料和运动使用 `Client` 专用接口；其他玩法使用 `Multiplayer/Messages/` 下的消息。职责与同步流程见 [联机架构](multiplayer-architecture.md)。

## 定义与注册

```csharp
[MemoryPackable]
[AutoLog]
public partial class ExampleMessage : MultiplayerMessage
{
    public int Value { get; set; }

    public override void OnReceivedDerived()
    {
        // 应用收到的数据
    }

    public static void Send(int value) =>
        new ExampleMessage { Value = value }.Enqueue();
}
```

- 使用 `[MemoryPackable]`、`[AutoLog]` 和 `partial`。正文只放标量、数据记录和稳定标识，不保存 Unity 对象。
- 新类型同时登记到核心库 `GameMessageType`、`GameMessageRules`，以及模组 `MultiplayerMessage` 的 `MemoryPackUnion` 和 `GameMessages.TypeOf`。
- 类型编号显式声明，不保留已删除消息的兼容占位。编号调整时更新协议版本。正文结构或协议含义变化时更新根目录 `Versions.props` 的协议版本。
- `GameMessages` 在主线程序列化一次并发送；接收时核对外层消息编号与实际消息类型，再调用 `MultiplayerMessage.OnReceived()`。

## 路由与身份

`GameMessageRules` 显式登记允许的路由、正文上限、是否属于房间、是否仅房主可发送。独立服务器与局域网使用同一份规则。

- `Enqueue()` 根据规则选择世界、房间或房主；`WireTargetUid` 只指定定向接收者，不进入正文。
- `SenderUid` 来自服务器确认的真实连接，不信任正文身份。房主生成结果后，发送者是房主。
- 上菜和确认上菜的 `ActorUid` 表示原执行者，用于处理本地已执行的回声。房主收到请求时用真实 `SenderUid` 覆盖该字段；客人不能广播裁定结果。
- 定向回复异步请求时使用 `Client.Reply(context, ...)`，保留原请求及双方入房代号。不要只保存 UID 后向后来重新入房的同名玩家回复。
- 房间广播排除发送者；世界广播不受房间边界影响。没有通用“排除任意玩家”、优先级插队或拥塞丢弃接口。
- 接收函数不能绕过 `MultiplayerMessage.OnReceived()`。服务器校验身份和范围，玩法层判断动作本身是否有效。

## 接收约束

按实际消息语义在 `OnReceivedDerived()` 上声明：

| 属性 | 约束 |
|---|---|
| `CheckScene` | 只在指定游戏场景处理 |
| `DiscardOnStory` | 剧情期间丢弃 |
| `RequireHostSender` | 只接受当前房主的权威结果 |
| `ClientOnlyReceive` | 只由客机处理 |
| `HostOnlyReceive` | 只由房主处理 |

明确区分请求、裁定和结果。只有现有玩法确实需要同一类型承担请求与结果时，才使用 `HostBroadcastOnly`，并分别检查两端行为。

## 时序与日志

`PluginHost.Update()` 调用 `GameSession.Tick()`，由 `Client.DispatchPending()` 顺序安装状态并派发 消息。普通接收函数运行在主线程；网络 IO 不访问游戏对象。

需要跨帧等待时使用协程或现有顾客 FSM。消息 的延迟回调必须检查 `IsCurrent`；顾客队列使用 `QueueForGuest`，使旧连接或旧入房身份的操作自动结束。其他管理器应捕获客户端及入房代号，或使用会话重置时递增的代号。不要阻塞主线程等待网络确认。

基类统一记录收发日志，按需覆盖日志级别或 `ToLogString()`。连接对象不参与日志序列化；不要输出 Token 或大块正文。

新增 消息 时核对：四处注册一致、身份与路由明确、场景及剧情约束完整、本地回声不重复执行、延迟操作能随退房失效。
