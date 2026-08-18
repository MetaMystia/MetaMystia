# 网络 Action 与 Behavior 规范

网络功能分为共享协议层和 Mod 行为层。Action 只描述线协议数据，发送、接收和游戏逻辑写在对应 Behavior 中。整体架构见 [`network-architecture.md`](network-architecture.md)。

## 文件结构

每个 Action 通常对应两个文件：

- `src/MetaMystia.Protocol/Actions/<场景>/ExampleAction.cs`：共享协议类型。
- `src/MetaMystia.Mod/Network/Behaviors/Actions/<场景>/ExampleBehavior.cs`：Mod 端发送和接收行为。

协议 Action 示例：

```csharp
using MemoryPack;

namespace MetaMystia.Network;

[MemoryPackable]
[NetAction.RoomRelay]
public partial class ExampleAction : NetAction
{
    public int Value { get; set; }
}
```

对应 Behavior 示例：

```csharp
namespace MetaMystia.Network;

[NetActionBehavior]
internal static class ExampleBehavior
{
    public static void Send(int value) =>
        new ExampleAction { Value = value }.Enqueue();

    public static void Register(NetActionDispatcher dispatcher)
    {
        dispatcher.Register<ExampleAction>(Handle,
            scene: Common.UI.Scene.WorkScene,
            discardOnStory: true,
            requireHostSender: true,
            receiveScope: NetReceiveScope.ClientOnly);
    }

    private static void Handle(ExampleAction action)
    {
        // 在主线程应用收到的数据。
    }
}
```

简单 Action 也必须保持协议与行为分离，不得把 Mod、Unity 或游戏逻辑写回协议类型。

## 协议层约束

`src/MetaMystia.Protocol/` 由 `MetaMystia.Protocol` 独立编译，必须保持纯托管：

- 不得引用 BepInEx、Unity、Interop DLL、游戏命名空间或 Mod 行为类型。
- Action 和 DTO 使用 `[MemoryPackable]`，并声明为 `partial`。
- 只保存可稳定序列化的标量、数组、DTO、协议枚举或稳定标识。
- 不得保存 Unity 对象、游戏对象、托管回调或仅在当前进程有效的引用。
- 游戏枚举使用 `Wire*` 镜像，由行为层的 `WireEnumMaps` 在边界转换。
- 只在线层生效的成员使用 `[MemoryPackIgnore]`；已有的 `WireTargetUid` 和 `WireExceptUid` 位于 `NetAction` 基类。
- 允许不依赖 Mod 或游戏运行时的纯数据辅助方法，但不得在协议类型中发送消息或修改游戏状态。

协议层不使用 `[AutoLog]`。日志由 Mod 行为层统一处理。

## 类型注册

新增 Action 时必须同时完成：

1. 在 `src/MetaMystia.Protocol/ActionType.cs` 末尾追加枚举值。
2. 在 `src/MetaMystia.Protocol/NetAction.cs` 增加对应的 `[MemoryPackUnion]`。
3. 在 `src/MetaMystia.Protocol/Actions/` 增加具体 Action。
4. 在 `src/MetaMystia.Mod/Network/Behaviors/Actions/` 增加对应 Behavior。
5. 确认 Action、`ActionType`、Union 标签和 Behavior 一一对应。

Behavior 使用 `[NetActionBehavior]`，并声明准确签名的：

```csharp
public static void Register(NetActionDispatcher dispatcher)
```

`BehaviorRegistryGenerator` 自动生成注册调用，不得手工维护 Behavior 清单。缺少合法 `Register()` 会触发 `MMNET001` 编译错误。

当前协议尚未发布，`ActionType` 和序列化成员可直接删除、重排或修改；必须同步所有协议消费者、Union 注册、Behavior 和测试，不保留兼容适配层。

## 发送

- 对外发送入口放在 Behavior，命名为 `Send()`，负责构造完整 Action 并调用 `Enqueue()`。
- 使用 `Enqueue()` 进入统一发送队列，不得直接操作 `MpWire` 的内部队列或 `DirectTcp`。
- 仅高频且允许拥塞时丢弃的数据使用 `Enqueue(lowPriority: true)`。
- `Send()` 在调用方线程执行，不会自动切换到主线程。后台调用只能读取纯托管且线程安全的数据。
- `Enqueue()` 会在调用方线程完成 Action 序列化；构造 Action 时不得延迟读取游戏对象。
- `MpWire.CanSend` 和 `discardOnStory` 会在统一入口拦截不应发送的数据，不要在每个 `Send()` 重复相同检查。

## 路由

- 需要房间转发的 Action 使用 `[NetAction.RoomRelay]`。
- 需要公域转发的 Action 使用 `[NetAction.PublicRelay]`。
- 控制面请求、确认和端点通知不添加 Relay 标记。
- `WireTargetUid` 表示端点仅向指定 UID 下发。
- `WireExceptUid` 表示端点广播时排除指定 UID。
- `WireTargetUid` 和 `WireExceptUid` 不参与序列化，普通 Mod 客户端不能用它们绕过上行端点直接寻址其他客户端。
- 控制面 Action 不使用 Relay 标记。`RoomKickAction` 由房主发往端点，端点校验后向 `TargetUid` 转发同一个 Action；`ServerKickAction` 和 `ServerShutdownAction` 由端点定向或广播。

是否转发必须依据明确的消息方向和作用域决定。不得因为其他客户端可能需要而默认广播，也不得把 Relay 标记当作接收权限。

## 控制面 Action

Action 类型决定控制流，`Reason` 只描述原因。

- `HandshakeRejectAction` 仅用于握手阶段。字段为 `HandshakeRejectReason Reason`；原因可描述服务器满、版本不匹配或 ID 非法，客户端收到后必须断开连接。
- `RoomRequestRejectAction` 仅用于 `CreateRoomRequestAction` 和 `JoinRoomRequestAction` 失败。字段为 `RoomRequestRejectReason Reason`；客户端保持当前公域状态。
- `LeaveRoomAction` 和 `LeaveServerAction` 无字段、无拒绝路径。前者仅在 Relay 中回到 Public，Direct 使用后者主动关闭连接。
- `RoomKickAction` 携带 `TargetUid`、`RoomId` 和 `RoomKickReason Reason`。Relay 房主发送后，端点校验房主权限、目标和房间归属，再向目标转发同一个 Action；DirectHost 直接发送。目标不发送 ACK。
- `ServerKickAction` 携带 `TargetUid` 和 `ServerKickReason Reason`，收到后关闭目标连接；`ServerShutdownAction` 无字段，收到后关闭所有连接。
- `RoomMemberLeaveAction` 携带 `Uid`、`RoomId` 和 `RoomLeaveReason Reason`，供房间其他成员更新投影；`PeerLeaveAction` 携带 `PeerUid` 和 `RoomLeaveReason Reason`，仅用于 Direct。

异常断线使用 `RoomLeaveReason.Disconnected` 更新成员投影，不伪造 Leave Action。

同一连接最多一个未完成的 Create/Join 请求。客户端在收到 `RoomAssignAction`、`RoomRequestRejectAction` 或断线前禁止再次请求；超时关闭当前连接，后续重连再握手，不使用 `RequestId`。

## 发送者身份

`SenderUid` 构造时由 `NetAction.LocalUidProvider` 填充，但远端声明不构成可信身份：

- DirectHost 根据 TCP 连接覆盖入站 `SenderUid`。
- 转发端点在下行包体中写入真实发送者 UID。
- 端点生成控制面 Action 时可显式使用保留的 `MpConstants.HostUid`。
- Behavior 必须根据消息语义校验端点、房主、同房成员或目标对象，不能只相信载荷内容。

## 接收注册

接收约束通过 `dispatcher.Register<TAction>()` 声明：

- `scene`：仅在指定游戏场景处理。
- `discardOnStory`：剧情期间丢弃接收，并阻止本地发送。
- `requireHostSender`：要求 `SenderUid` 等于当前 `MpSession.HostUid`。
- `receiveScope: Any`：不限制本地角色。
- `receiveScope: ClientOnly`：直连服务端端点不处理；Relay 房主仍属于客户端进程。
- `receiveScope: HostOnly`：仅当前房主处理。
- `receiveScope: EndpointOnly`：仅当前服务端端点处理。

通用约束由 Dispatcher 执行。以下语义仍需在 `Handle()` 中显式校验：

- 是否来自保留的服务端端点 UID。
- 玩家是否属于当前房间或公域。
- 目标 UID、房间 ID、资源 ID 和运行时对象是否存在且匹配。
- 状态迁移是否允许、消息是否重复、回声是否需要忽略。

请求、拒绝、踢出和成员增量是不同语义，应使用不同 Action 表达；不得用 `Reason` 在同一 Action 内隐式切换控制流。

## 线程与时序

`PacketBuffer` 在 IO 线程组帧和反序列化，`MpWire` 将 Action 放入 `_inbox`。`PluginManager.Update()` 在主线程调用 Dispatcher，因此普通 `Handle()` 可以访问游戏对象。

- `Handle()` 不得阻塞、轮询等待或使用 `Thread.Sleep`。
- 需要延迟或等待游戏状态时，优先使用协程；维护现有 FSM 队列时遵循其既有时序。
- 自行创建的 `Task`、线程和延迟回调仍需通过 `RunOnMainThread()` 返回主线程。
- 从 `yield`、异步或队列恢复后，重新确认游戏对象和场景状态。

## 日志

Action 收发日志由 `NetActionRuntime` 统一记录。Behavior 不得在每个 `Send()` 和 `Handle()` 重复记录同一条收发日志。

新增高频或大载荷 Action 时，应同步检查 `ReceiveLogLevel()`、`SendLogLevel()`、`ReceiveLogOnlyAction()`、`SendLogOnlyAction()` 和 `ToLogString()`。日志不得输出密钥、Token、大块二进制或完整资源内容。

## 新增检查表

- Action 是否位于协议目录且没有游戏依赖。
- Behavior 是否位于行为目录并带 `[NetActionBehavior]`。
- `ActionType` 是否只追加且未重复。
- MemoryPack Union 是否同步注册。
- Action、枚举、Union 和 Behavior 是否一一对应。
- 字段和 DTO 是否可稳定序列化。
- `Wire*` 枚举映射是否经过核对。
- 消息方向、权威方、作用域和端点是否明确。
- 接收范围与业务权限是否分别处理。
- 是否正确选择 Relay、定向、排除或低优先级策略。
- 握手拒绝、入房拒绝、离房、离服、房间踢出和服务器踢出是否使用正确的 Action。
- 房间请求是否保持单飞、无 `RequestId`，并在超时后关闭连接。
- 是否处理伪造发送者、回声、重复消息和对象不存在。
- `Send()` 的调用线程是否安全，`Handle()` 是否保持主线程非阻塞。
