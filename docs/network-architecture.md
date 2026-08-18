# 网络架构

本文描述 Mod 端和共享协议层的当前边界。仓库中的 `src/MetaMystia.Server/` 目前只有空入口，尚未实现 Relay 服务端。

## 模块边界

- `src/MetaMystia.Protocol/`：独立的 `netstandard2.1` 项目，直接拥有线协议类型，包括 `NetPacket`、`NetAction`、具体 Action、DTO、协议枚举和 `PacketBuffer`。
- `src/MetaMystia.Mod/Network/Behaviors/`：Mod 端行为层，负责发送入口、接收约束、游戏状态读写和协议类型与游戏类型的转换。
- `src/MetaMystia.Mod/Network/MpWire.cs`：Mod 端线层，管理收发队列、IO 线程、组帧、直连转发和主线程分发入口。
- `src/MetaMystia.Mod/Network/DirectTcp.cs`：直连 TCP 实现，只由 `MpWire` IO 线程驱动。
- `src/MetaMystia.Mod/Network/MpSession.cs`：记录传输类型、连接阶段和当前房主 UID。
- `src/MetaMystia.Mod/Managers/MpManager.cs`：联机应用层，组合传输、作用域、房间角色、场景和玩法状态。
- `src/MetaMystia.Mod/Players/PlayerManager.cs`：维护本地玩家与远端玩家表，并提供公域和房间投影视图。
- `src/MetaMystia.Server/`：可独立构建和发布的服务端宿主；当前保持空实现，尚未引入业务依赖。

协议层不得引用 BepInEx、Unity、Interop DLL、游戏命名空间或 Mod 行为类型。游戏相关数据必须先转换为纯托管 DTO、稳定标识或 `Wire*` 枚举。

## 状态维度

联机状态由多个独立维度组成，不得只用 Host/Client 或单个 `IsConnected` 推断权限。

### 传输类型

`MpSession.TransportKind` 包含：

- `None`：没有传输会话。
- `DirectHost`：本地同时承担直连 TCP 端点和房主。
- `DirectClient`：连接直连端点的客机。
- `RelayClient`：连接独立 Relay 端点的 Mod 客户端。

### 作用域与角色

- 公域：仅 Relay 会话存在，本地 `RoomId` 为 `PublicRoomId`，`Role` 为 `None`。
- 房间：本地具有非公域 `RoomId`，`Role` 为 `Host` 或 `Client`。
- 房主：由 `IsRoomHost` 表达，是玩法权威角色。
- 服务端端点：由 `IsServerEndpoint` 表达，当前 Mod 端仅 `DirectHost` 满足。Relay 房主不是服务端端点。

常用判断应按语义选择：

- 处理端点请求：`IsServerEndpoint`。
- 执行房主裁定：`IsRoomHost`。
- 执行客机逻辑：`IsRoomClient`，或使用 Dispatcher 的接收范围。
- 判断公域连接：`IsInPublicScope`、`IsPublicConnected`。
- 判断玩法同步：`IsGameplaySyncActive`、`ShouldSkipAction`。

`MpConstants.HostUid` 是直连端点和 Relay 控制面使用的保留 UID。`MpSession.HostUid` 表示当前房间的实际房主 UID；在 Relay 房间中二者可能不同。

## 连接流程

客户端连接流程为：

1. `ConnectAsync()` 建立 TCP 上行链路。
2. 客户端发送 `ServerInfoRequestAction`。
3. 端点返回 `ServerInfoReplyAction`，声明游戏版本、Mod 版本和 `ServerMode`。
4. 客户端确认版本和模式后发送 `HelloAction`。
5. 端点返回 `HelloAckAction`，分配 UID 并下发全服轻量玩家表。
6. Direct 模式继续返回 `RoomAssignAction`，客户端进入直连房间。
7. Relay 模式先进入公域，之后通过 `CreateRoomRequestAction` 或 `JoinRoomRequestAction` 请求进入房间。

房间和公域成员变化使用增量 Action：

- `PublicPlayerUpsertAction` 更新公域轻量玩家记录。
- `RoomAssignAction` 向进房者下发自身身份和现有成员全量表。
- `RoomNewPlayerJoinedAction`、`RoomMemberLeaveAction` 更新房间成员。
- `RoomKickAction` 在 Relay 中使目标退回公域，在 Direct 中使目标断开连接。
- `PeerLeaveAction` 用于直连端点通告连接离开。

请求、结果和成员增量具有不同语义，不得合并为一个含义不明确的 Action。

## 控制面 Action

控制流由 Action 类型决定，`Reason` 只描述原因，不改变状态迁移。

| Action | 方向 | 字段 | 收到后的状态 |
| --- | --- | --- | --- |
| `HandshakeRejectAction` | 端点 -> 握手中的客户端 | `HandshakeRejectReason Reason` | 断开连接，进入 Offline |
| `RoomRequestRejectAction` | 端点 -> 入房请求者 | `RoomRequestRejectReason Reason` | 保持当前公域状态 |
| `LeaveRoomAction` | 客户端 -> 端点 | 无 | Relay：Room -> Public；Direct 不支持；不可拒绝 |
| `LeaveServerAction` | 客户端 -> 端点 | 无 | 主动关闭连接；不可拒绝 |
| `RoomKickAction` | 房主 -> 端点 -> 目标 | `TargetUid`、`RoomId`、`RoomKickReason Reason` | Relay：Room -> Public；Direct：断开连接 |
| `ServerKickAction` | 端点 -> 目标 | `TargetUid`、`ServerKickReason Reason` | 断开连接 |
| `ServerShutdownAction` | 端点 -> 全部客户端 | 无 | 全部断开连接 |
| `RoomMemberLeaveAction` | 端点 -> 房间其他成员 | `Uid`、`RoomId`、`RoomLeaveReason Reason` | 移除房间成员投影 |
| `PeerLeaveAction` | DirectHost -> 其他客机 | `PeerUid`、`RoomLeaveReason Reason` | 移除直连成员投影 |

`RoomKickAction` 在 Relay 中由房主发往端点，端点校验权限、目标和房间归属后，向目标转发同一个 Action；DirectHost 直接向目标发送同一个 Action。目标不发送 ACK，端点另向其他成员发送 `RoomMemberLeaveAction` 或 `PeerLeaveAction`。

`LeaveRoomAction`、`LeaveServerAction` 没有拒绝路径。异常断线由连接层处理，并按 `Disconnected` 原因更新成员投影。

握手拒绝原因只描述握手失败，例如服务器满、版本不匹配或 ID 非法；入房失败使用独立的 `RoomRequestRejectAction`。

同一连接最多有一个未完成的 `CreateRoomRequestAction` 或 `JoinRoomRequestAction`。客户端等待 `RoomAssignAction`、`RoomRequestRejectAction`、断线或超时；超时关闭当前连接，不携带 `RequestId`，也不得复用连接发送下一次入房请求。

## 收发链路

出站链路为：

```text
Behavior.Send
  -> NetActionRuntime.Enqueue
  -> NetPacket 序列化和加长度头
  -> MpWire._outbox
  -> MpWire IO 线程
  -> DirectTcp
```

入站链路为：

```text
DirectTcp
  -> PacketBuffer 组帧和反序列化
  -> MpWire.OnWirePacket
  -> 可选的 DirectHost 转发
  -> MpWire._inbox
  -> PluginManager.Update
  -> NetActionDispatcher
  -> Behavior.Handle
```

组帧、反序列化和直连转发发生在 IO 线程。Dispatcher 和 `Handle()` 由主线程执行，可以访问游戏对象，但不得阻塞主线程。详细规则见 [`threading-and-scheduling.md`](threading-and-scheduling.md)。

## 路由与信任

- `[NetAction.RoomRelay]` 表示需要在房间作用域转发。
- `[NetAction.PublicRelay]` 表示需要在公域作用域转发。
- `WireTargetUid` 和 `WireExceptUid` 是本地线层参数，不参与序列化。
- Mod 客户端的出站数据统一发往上行端点；定向和排除参数只在端点侧实际选择下行连接。
- `SenderUid` 不可信任远端包体声明。DirectHost 使用 TCP 连接 UID 覆盖入站值；转发端点负责在下行包体中写入真实发送者。
- 接收范围只决定本地是否处理，不能替代服务端路由、发送者校验或业务权限检查。

控制面 Action 不应添加 Relay 标记。修改端点专用 Action 时，还需检查 `MpWire.IsEndpointOnly()` 的防御性排除列表。

## 协议变更

当前协议尚未发布，不保留旧 Action、枚举值或兼容适配层。删除、重命名、重排或修改字段时，直接同步所有协议消费者、Behavior、Union 注册和测试。

具体新增流程见 [`network-action-style.md`](network-action-style.md)。
