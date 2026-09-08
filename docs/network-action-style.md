# 网络 Action 规范

## 边界

控制协议放在 `MetaMystia.Protocol`，只包含连接、房间、身份、路由和字节载荷。游戏 Action 留在 `MetaMystia.Mod/Network/Actions/`，可以直接使用游戏枚举，不能保存 Unity 对象。

服务器不解析游戏载荷。新增游戏 Action 不需要修改服务器、控制协议 Union 或建立 Wire 枚举镜像。

## 注册和发送

```csharp
[MemoryPackable]
[AutoLog]
public partial class ExampleAction : Action
{
    public int Value { get; set; }

    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        // 应用业务操作
    }

    public static void Send(int value) =>
        new ExampleAction { Value = value }.Enqueue();
}
```

在 `GameMessages` 中登记一次类型、稳定编号、Route 和玩法阶段。默认阶段是 Night，公共消息及初始数据明确标记为阶段无关。

编号只追加，不重用已有编号。修改序列化字段或语义时更新 `GameMessages.Version`；控制消息格式变化更新 `ProtocolVersion.Current`。游戏载荷版本和核心协议版本不是同一个版本。

使用 `Enqueue()` 进入统一入口。不要直接操作套接字，也不要在 Action 内判断直连或专服。`WireTargetUid` 只用于需要定向发送的事件；状态块必须广播，才能一致缓存与重放。

## 选择消息类别

| Route | 来源与接收方 | 缓存 |
| --- | --- | --- |
| PublicState | 玩家更新自身公共数据 | 最新完整块 |
| PublicEvent | 玩家向公共域发送事件 | 不缓存 |
| MemberState | 成员更新自身房间数据 | 当前绑定最新块 |
| MemberEvent | 成员向同房成员发送事件 | 不缓存 |
| HostRequest | 成员向当前房主提交请求 | 不缓存 |
| HostEvent | 房主向成员发布结果 | 不缓存 |
| HostState | 房主发布协调状态 | 当前房间最新块 |

`Endpoint` 校验真实连接、发送者加入实例、房主权限，并填写实际 SenderUid 与接收者加入实例。`ClientSession` 再检查成员、实例和序号。`GameMessages` 检查类型与 Route 是否匹配，以及玩法阶段。

不要手动填写 SenderUid 代替另一个玩家。结果如果需要保留原请求者，另设 ActorUid；例如上菜请求由房主核对真实来源，房主结果携带 ActorUid 供回声处理。

`requestAndResult` 只用于载荷相同而方向明确的既有请求/结果。不要把所有操作默认设为成员广播；需要裁定的操作先交房主。

## 数据与游戏效果

- 外观、场景、位置、资源先更新 `ModPlayerStore`；Handler 不生成或销毁角色。
- 同类状态用完整块替换，未发送的块不变，空表明确表示清空。
- 公共与成员状态发送时也更新本机 Store；房主阶段结果在 `RoomGameplay` 中应用。
- `CheckScene` 表示等待指定场景；`WaitUntilStoryEnds` 表示等待剧情结束，不再丢弃剧情中的重要操作。
- 修改游戏世界的消息由 `RoomGameplay` 在当前绑定内按序处理；等待队列有容量和期限。
- 需要等待的协程使用 `RoomGameplay.Run()`，阶段或绑定结束时统一停止。其他延后回调必须检查捕获的绑定与阶段。
- 不要另建无界队列，也不要只检查“现在仍在联机”后执行旧回调。

准备、选择请求由房主修改阶段快照。不要在 Action、LocalPlayer、PeerPlayer 各保存一份准备标志。

## 握手、线程与诊断

握手为 Hello → Welcome。握手前不能发业务消息，Online 只表示公共连接就绪；入房与玩法同步分别确认。服务器收到控制请求后先提交一致状态，再发送快照。

网络 IO 只读写帧；Mod 主线程通过 `MpWire.FlushInbox()` 处理控制确认和 Action。拒绝连接先写出 Rejected 再关闭。队列溢出明确断连，不静默丢弃玩法事件。

基类记录消息类型、来源和阶段，高频消息可降低 `OnSendLogLevel / OnReceiveLogLevel`。日志保留内部原因；面向玩家的提示通过 TextId 和中英文语言文件提供。

## 新增消息时核对

1. 数据属于哪个拥有者，何时失效。
2. 是完整状态还是一次性事件，是否需要房主裁定。
3. 注册编号、Route、阶段是否正确。
4. 对象尚未生成、转场、退房及旧消息到达时如何处理。
5. 完成相关游戏源码审计、完整构建和必要回归。
