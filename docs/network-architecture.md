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
6. Direct 模式继续返回 `RoomEnterAction`，客户端进入直连房间。
7. Relay 模式先进入公域，之后通过 `CreateRoomRequestAction` 或 `JoinRoomRequestAction` 请求进入房间。

房间和公域成员变化使用增量 Action：

- `PublicPlayerUpsertAction` 更新公域轻量玩家记录。
- `RoomEnterAction` 向进房者下发自身身份和现有成员全量表。
- `RoomMemberJoinAction`、`RoomMemberLeaveAction` 更新房间成员。
- `RoomKickAction` 使 Relay 客户端退回公域；Direct 踢出使用断开连接。
- `PeerLeaveAction` 用于直连端点通告连接离开。

请求、端点确认、房间成员增量和公域增量具有不同语义，不得合并为一个含义不明确的 Action。

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

## 协议兼容性

当前没有独立协议版本。握手要求游戏版本和 Mod 版本匹配，但这不能代替序列化兼容性审查。

修改共享协议时必须同时检查所有消费者，并遵循以下规则：

- `ActionType` 只能追加，不能插入、重排或复用已有数值。
- `MemoryPackUnion` 标签必须与 `ActionType` 一一对应。
- 修改已有字段的类型、顺序或含义前，必须评估旧客户端、服务端和测试客户端的行为。
- 协议变更必须同步更新发送方、接收方、DTO 转换和对应 Behavior。
- 需要不兼容变更时，应同步发布所有消费者并更新 Mod 版本。

具体新增流程见 [`network-action-style.md`](network-action-style.md)。
