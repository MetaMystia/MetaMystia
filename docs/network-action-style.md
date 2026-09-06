# 网络 Action 规范

## 基本结构

每个网络 Action 应放在 `Network/Actions/` 对应场景目录中，并保持以下结构：

```csharp
[MemoryPackable]
[AutoLog]
public partial class ExampleAction : Action
{
    public int Value { get; set; }

    public override void OnReceivedDerived()
    {
        // 应用收到的数据
    }

    public static void Send(int value) =>
        new ExampleAction { Value = value }.Enqueue();
}
```

- Action 必须使用 `[MemoryPackable]`、`[AutoLog]` 和 `partial`。
- 序列化数据使用属性表达；只在线层生效的状态使用 `[MemoryPackIgnore]`。
- 接收逻辑写在 `OnReceivedDerived()` 中，不得绕过 `Action.OnReceived()` 直接处理入站 Action。
- 对外发送入口命名为 `Send()`，负责构造完整 Action 并调用 `Enqueue()`。
- 不得在 Action 中保存 Unity 对象引用。网络数据必须由可序列化的标量、DTO 或稳定标识组成。

## 类型注册与兼容性

新增 Action 时必须同时完成：

1. 在 `ActionType` 末尾增加枚举值。
2. 在 `Action` 上增加对应的 `[MemoryPackUnion]`。
3. 新增具体 Action 类型。
4. 确认 `ActionType`、`MemoryPackUnion` 和具体类型一一对应。

不得插入、重排或复用已有 `ActionType` 数值。修改已有序列化字段的类型、顺序或含义前，必须明确评估协议兼容性。

本次版本预检按需求例外调整：`ConnectionInfo = 0`，原 `Ping`、`Pong` 及后续编号均顺延一位。此后仍只允许追加新编号。

## 连接预检

直连顺序：`ConnectionInfo` 请求 → `ConnectionInfo` 回复 → `Hello` → `HelloAck`。

- `ConnectionInfo` 固定为 ID 0，交换游戏版本、模组版本、协议版本、房间上限和人数。人数包含本机，不计尚未完成握手的连接。
- 协议版本暂取模组版本。客机要求三项版本全部一致，否则显示双方版本并断开，不发送 `Hello`。
- 客机发现预检人数已达上限时提示满房并断开，不发送 `Hello`。预检人数只表示当时状态，主机仍在接收 `Hello` 时检查最终人数限制和其他入房条件。
- 握手完成前不发送 `Ping`，也不发送、处理或转发业务同步。主机给待加入连接发送的首包为定向预检回复。
- 旧版本没有预检协议，无法提供完整版本信息，只能显示握手失败或超时。跨版本识别依赖双方支持这一固定编号和字段布局。

## 发送与路由

- 使用 `Enqueue()` 进入统一发送队列，不得直接操作 `DirectTcp`。
- 仅高频且允许拥塞时丢弃的数据使用 `Enqueue(lowPriority: true)`。
- `WireTargetUid` 表示仅发送给指定 UID。
- `WireExceptUid` 表示广播时排除指定 UID。
- `WireTargetUid` 和 `WireExceptUid` 只属于线层，不参与序列化。
- `SenderUid` 由线层根据实际连接写入或校正，不得信任远端自行声明的发送者身份。

拒绝连接使用 `RejectAction.SendAndDisconnect()`：IO 线程写出拒绝包后再关闭连接，不得在消息入队后立即断开。入站消息和断开事件按同一队列处理，确保最后收到的拒绝原因先于断开提示。

需要房间转发的 Action 使用 `[RoomRelay]`；需要公域转发的 Action 使用 `[PublicRelay]`。是否转发必须依据实际消息流决定，不得因“其他客户端可能需要”而默认广播。

## 接收约束

接收约束应使用 `Action` 提供的属性声明：

- `[CheckScene(...)]`：仅在指定场景处理。
- `[DiscardOnStory]`：剧情期间丢弃。
- `[RequireHostSender]`：仅接受主机发送的权威结果。
- `[ClientOnlyReceive]`：仅客机处理。
- `[HostOnlyReceive]`：仅主机处理。

这些属性写在 `OnReceivedDerived()` 上。不得在每个 Action 内重复实现已有的通用接收检查。

使用约束前必须先明确消息方向、权威方、转发路径和回声处理。请求、主机裁定和权威广播是不同语义，不应合并为含义模糊的 Action。

## 线程与时序

`MpWire` 在 IO 线程收包后将 Action 放入 `_inbox`，再由主线程调用 `OnReceived()`。因此，普通 `OnReceivedDerived()` 可以访问游戏对象，但延迟回调和自行创建的后台任务仍必须遵循主线程规则。

需要等待游戏状态时，使用项目已有的队列或 FSM 调度方式。不得在接收函数中阻塞线程、轮询等待或使用 `Thread.Sleep`。

## 日志

Action 基类统一记录发送和接收日志。仅在必要时覆盖：

- `OnSendLogLevel`、`OnReceiveLogLevel`；
- `OnSendLogOnlyAction`、`OnReceiveLogOnlyAction`；
- `ToLogString()`。

高频 Action 应降低日志级别或只记录 Action 名称。不得在日志中输出密钥、Token 或大块二进制数据。

## 新增检查表

- Action 编号是否只追加且未重复。
- MemoryPack Union 是否同步注册。
- 字段是否均可稳定序列化。
- 消息方向和权威方是否明确。
- 场景、剧情和接收端约束是否完整。
- 是否需要转发、定向发送、排除发送或低优先级。
- 是否正确处理本地回声、重复消息和对象不存在的情况。
- 接收逻辑是否在主线程执行且没有阻塞。
