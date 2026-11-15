# 使用统计事件

客户端使用私有统计服务 `https://track.izakaya.cc/api.php`，站点 ID 为 `13`。请求采用 Matomo 兼容的 GET 事件参数：

- `e_c`：事件类别，本模组统一使用 `Client`
- `e_a`：操作及结果，使用点号分层，如 `Multiplayer.Connect.Failed`
- `e_n`：简短的事件详情，使用 `key=value;key=value` 格式

客户端沿用助手与管理器的 Matomo 事件参数，不额外发送任意字段字典。详情应放在 `e_n`，并使用短键、ASCII错误码和短版本号。接入时整个 `e_n` 按编码后 1024 字节截断，规则与管理器上报一致。不要发送 JSON、完整堆栈或本地化错误正文。

## 事件表

| 状态 | `e_a` | `e_n` 示例 | 触发时机 |
|---|---|---|---|
| 已接入 | `Run` | `0.31.2` | 首次进入主场景，每次进程一次 |
| 待接入 | `Run.Options` | `game=4.4.0e;target=4.4.0e;proto=0;lang=zh-CN;dlc=CORE,DLC1;debug=0;sig=1;ignoreDlc=0;ipv6=0;maxPlayers=2` | 首次进入主场景，启动环境只发送一次 |
| 待接入 | `Game.VersionMismatch` | `game=4.4.0e;target=4.4.0f` | 检测到游戏版本与目标版本不一致 |
| 待接入 | `Multiplayer.Connect.Start` | `mode=official;port=40815;ipv6=0` | 开始连接官方入口、指定服务器或本地开服 |
| 待接入 | `Multiplayer.Connect.Success` | `mode=official;uid=1;durationMs=438;players=1;maxPlayers=16` | 连接或本地开服成功 |
| 待接入 | `Multiplayer.Connect.Failed` | `mode=official;durationMs=3007;code=HandshakeTimeout;count=0;limit=16;protocol=0;remoteGame=4.4.0e;remoteMod=0.31.2` | 连接或本地开服失败 |
| 待接入 | `Multiplayer.Room.Create.Success` | `uid=1;room=AB20;membership=1;members=1;maxPlayers=4` | 独立服务器建房成功 |
| 待接入 | `Multiplayer.Room.Create.Failed` | `uid=1;code=InvalidLimit;requestedMax=512;maxPlayers=16` | 独立服务器建房失败 |
| 待接入 | `Multiplayer.Room.Join.Success` | `uid=2;room=AB20;membership=2;members=2;maxPlayers=4` | 通过房间码加入成功 |
| 待接入 | `Multiplayer.Room.Join.Failed` | `uid=2;room=AB20;code=RoomFull;count=4;limit=4` | 通过房间码加入失败 |
| 待接入 | `Multiplayer.Disconnect` | `roomRole=guest;uid=2;room=AB20;membership=2;code=ConnectionLost;durationSec=1240;players=2;roomPlayers=2` | 已建立的联机连接结束 |
| 待接入 | `ResourceEx.Load.Finished` | `loaded=3;rejected=1;dependencyRejected=0;signature=1` | 每次资源包扫描结束 |
| 待接入 | `ResourceEx.Load.Package` | `package=ExamplePack;version=1.2.0;label=example;zipKB=512` | 每个资源包加载成功时 |
| 待接入 | `ResourceEx.Load.Rejected` | `package=ExamplePack;version=1.2.0;reason=id_range` | 每个资源包被拒绝时 |
| 待接入 | `Initialization.Failed` | `stage=resourceex;type=IOException;message=...` | Patch、宿主、Addressables 或 ResourceEx 最终初始化失败 |

`mode`取值为`host`、`official`或`direct`。连接成功和失败事件中的`durationMs`是本次操作耗时；断开事件中的`durationSec`是连接持续时间。

`uid`是联机服务器分配的会话内玩家 UID，用于与服务器日志关联。它不是统计服务的设备 ID，不可跨服务器会话、重启或服务器实例识别玩家。连接尚未建立时没有该值，不记录。

`room`是由服务器分配的四位大写十六进制房间码。客户端不能指定或覆盖创建时的房间码。本地开服会在握手时自动创建默认房间，因此该模式的`Connect.Success`可能已经包含`room`和`membership`；普通官方或独立服务器连接在真正建房或入房前没有房间上下文。

`membership`是服务器在每次成功入房时分配的成员代号，用于区分同一UID的多次入房。它只用于房间创建、加入和断线事件。

`room`、`membership`和`roomRole`只在事件发生时已经入房的情况下记录。`roomRole`取值为`host`或`guest`。

网络错误码使用`NetworkErrorCode`名称。`count`、`limit`、`protocol`、`remoteGame`和`remoteMod`只在协议错误提供对应值时记录。`remoteGame`和`remoteMod`表示拒绝连接的一方提供或要求的版本，不表示本地版本。

## 接入前置条件

以下数据目前不能全部从现有调用点直接取得，接入对应事件前必须先补齐数据源：

| 事件或字段 | 当前限制 | 接入要求 |
|---|---|---|
| `Multiplayer.Connect.*`的`mode` | `ConnectOfficial`直接调用通用`Connect`，没有保留官方、直连或本地开服的来源 | 增加内部连接模式参数，由调用方显式传入 |
| 连接和房间失败的`code/count/limit/...` | `Run`的失败回调是无参数`Action`，异常虽然已由`Observe`取得，但没有传给业务调用方 | 将失败回调改为携带`Exception`或`NetworkError`，再统一生成事件详情 |
| `durationMs`、`durationSec` | 当前没有连接开始时间和成功连接时间 | 在连接开始时记录单调时钟，在成功、失败或断开时计算耗时 |
| `Run.Options`的`dlc` | `ActivePackTags`会在DLC标签后追加ResourceEx包标签，不能代表纯DLC状态 | 单独保存并暴露纯DLC标签，不能直接使用`ActivePackTags` |
| `ResourceEx.Load.Finished`的`rejected` | 缺少`ResourceEx.json`的ZIP和版本冲突落选包目前不会进入拒绝列表 | 让加载结果记录所有跳过和拒绝的包，确保汇总数量完整 |
| `ResourceEx.Load.Rejected`的`version/reason` | 拒绝结果目前只有包名和字符串原因，且无法可靠区分ZIP失败与JSON失败 | 让加载器返回结构化原因及可用的包版本、标签 |
| `ResourceEx.Load.Package`的`zipKB` | `LoadedResourcePackage`未保存ZIP大小，只能根据`ZipPath`再次读取 | 加载时保存ZIP大小，或用`FileInfo.Length`读取现有路径 |

资源包没有`packInfo`时，`version`和`label`可能不存在，此时省略字段，不伪造默认版本。`RuntimeAddressables`的首次初始化失败会重试，只有最终无法恢复时才发送`Initialization.Failed`。

资源包拒绝原因使用短分类：

| 分类 | 含义 |
|---|---|
| `zip` | ZIP读取失败 |
| `json` | `ResourceEx.json`缺失或解析失败 |
| `id_range` | ID范围或签名校验失败 |
| `dependency` | DLC或资源包依赖不满足 |
| `finalize` | 完成资源包初始化失败 |

`ResourceEx.Load.Rejected`的`package`和`version`来自包本身，可能较长；与其他动态内容一样受整体字节上限约束。

## 接入位置

| 事件 | 主要接入点 |
|---|---|
| `Run`、`Run.Options`、`Game.VersionMismatch` | `IzakayaReporter`与`MainSceneManagerPatch` |
| `Multiplayer.Connect.*`、`Multiplayer.Disconnect` | `GameSession.StartHost`、`Connect`、`ConnectionEnded`及`Observe` |
| `Multiplayer.Room.*` | `GameSession.CreateRoom`、`JoinRoom`及异步操作结果 |
| `ResourceEx.Load.*` | `ResourcePackageLoader.LoadAllPackages`与`ResourceExManager.LoadAllResourcePackages` |
| `Initialization.Failed` | `Plugin.Load`、Patch应用、`PluginHost`创建及ResourceEx初始化 |

`ReportOtherEventAsync`当前受`isOtherEventEnabled`控制。除`Run`外的事件接入完成后，才能开启该开关。

## 心跳

心跳不使用事件参数，只发送`ping=1`。首次进入主场景立即发送，之后每2分钟发送一次，用于会话持续时间和活动统计。

## 不记录

- 逐帧状态、运动数据、逐条玩法消息和聊天正文
- 玩家名称和完整连接地址
- 资源包全部文件列表或资源正文
- 未截断的异常消息、文件路径和堆栈
