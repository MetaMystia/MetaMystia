# 联机架构

支持常规营业与最终试炼联机。玩家各自保留日期、存档和剧情进度，在共同玩法的关键节点同步推进。

## 职责

核心库使用 `MetaMystia.Network`，模组接入层使用 `MetaMystia.Multiplayer`，玩法消息位于 `MetaMystia.Multiplayer.Actions`。

服务器管理连接、世界成员、房间和入房许可。房主是由房间 `Host` 标识的客户端，UID 不固定。

| 代码 | 职责 |
|---|---|
| `MetaMystia.Network/Connection`、`Protocol` | TCP 分帧、有序收发、容量限制和超时 |
| `MetaMystia.Network/Server` | 单一处理循环维护成员和房间，校验身份并路由 |
| `MetaMystia.Network/Client` | 请求与确认、状态快照、调用线程上的有序派发 |
| `MetaMystia.Network/LanSession` | 本地开服、房主预留、客人自动加入默认房间 |
| `MetaMystia.Mod/Multiplayer/GameSession` | 游戏连接入口、主线程派发、异步操作提示与清理 |
| `GameFlow`、`DayDestinationManager`、`BusinessStart` | 游戏阶段、场景转换规则、入口确认与营业开场等待 |
| `PlayerProfile`、`PlayerManager.Network` | 玩家资料和运动转换、世界及房间角色显示 |
| `GameActions`、`GameMessageRules` | 玩法消息类型、路由、发送者身份与房间校验 |
| `RoomClock` | 房主时钟校准；与网络存活检测分开 |

连接状态、房间身份和玩法阶段分别判断。

## 连接与共享范围

- 局域网房主断开时关闭本地服务器；客人退房即结束该次连接。
- 独立服务器允许停留在世界、创建和加入房间。房主离开后解散房间，其他人保留世界连接。
- 主菜单与自由白天允许建房、入房；房间内仍有人加载、收尾或已提交共同入口意向时，暂停接纳新成员。服务器也检查入房者的阶段。
- 自由白天读档、主菜单往返保留连接、房间与入房身份，并撤回本人旧入口意向；加载中的成员不能参加共同推进。
- 聊天向世界广播。白天运动向世界共享，夜间运动仅在同一房间的夜间玩家之间共享；运动转发、缓存和角色显示使用相同范围。
- 世界资料包含名字、场景、玩法阶段和皮肤的稳定资源包标识；完整资源表只在同房间共享，连接期间固定。缺失自定义皮肤时沿用本地回退显示。
- 房间人数含房主，范围为 1–256，且独立服务器房间不能超过服务器上限。降低上限保留现有成员；局域网房间与服务器上限一起更新。
- 管理请求无法确认或收发队列满时断开并提示原因。入房超时先撤销；独立服务器确认撤销后保留世界连接，无法确认则断开。
- 每次入房有独立代号。退房或重连后，旧玩法消息、延迟回调和角色生成不能继续操作新会话。

## 同步与断开

[场景流转图](scene-flow-audit.md) 中蓝色标记表示同步点，红色路径表示先断开再执行原操作。

| 位置 | 处理 |
|---|---|
| 白天进入营业／最终试炼 | 收集兼容意向 → 等待服务器关闭入房 → 房主锁定目标并广播执行；各端继续原入口，无二次表决或取消 |
| 确认前读档或返回菜单 | 撤回本人意向；房主依据收到的意向决定是否推进 |
| 已确认共同入口 | 迟到改选不改变本轮目标；结束白天事件、选店均属于共同流程，读档按中断处理 |
| 选店、准备 | 同步店铺、备菜表与就绪状态；缓存本地加载期间收到的准备数据 |
| 常规营业开场 | 各端完成原开场事件、装饰与伙伴初始化后报告就绪；房主确认全员夜间就绪，先广播放行，再启动原刷客与计时 |
| 最终试炼 | 支持首次、重修及混合入口，同步备菜、客人绑定、阶段推进和胜负 |
| 正常打烊／试炼返回 | 按房主裁定执行原游戏收尾；先回白天者可自由活动，下一次共同推进等待其他成员就绪或退出 |
| 共同流程读档、回菜单、回退 | 在 `SaveManagement` 重置游戏状态前主动断开，取消等待中的续接 |
| 未接入同步的夜间入口、共同流程结局改道 | 在 `UniversalGameManager.LoadScene` 执行前断开；最终试炼的 `TryLeaveSession` 正常返回单独放行 |

玩家资料发布玩法阶段，不比较日期。玩家只提交意向，房主决定共同推进，各端执行命令时不再比较最新选择。入口协调使用 `DayDestinationManager.Round`，忽略旧轮次的意向与重复执行命令；试炼备菜使用准备编号，没有统一的玩法轮次。

离开白天时清理旧入口；正常回白天时保留其他成员已提交的下一次意向。备菜、开场等待等状态各自重置。`GuestFSM` 队列保存已收到、等待执行的顾客操作。

开发原则见[营业联机开发方法](multiplayer-business-sync-reference.md)，具体审计见[顾客同步](guest-fsm-audit.md)和[幽幽子挑战同步](yuyuko-challenge-sync.md)。

## 命令

默认 TCP 端口为 `40815`，可通过配置修改。

| 命令 | 用途 |
|---|---|
| `/mp start [port]` | 本地开服并建房；保留 `/mp start server` 别名 |
| `/mp connect <address> [port]` | 连接并自动加入局域网默认房间 |
| `/mp connect <address> [port] --world` | 连接独立服务器，停留在世界 |
| `/mp rooms`、`/mp create [count]`、`/mp join <room>` | 查看、创建、加入房间 |
| `/mp leave` | 退房；局域网模式结束该次联机 |
| `/mp disconnect`、`/mp stop` | 结束连接；本地开服时同时关闭服务器 |
| `/mp maxplayers [count]` | 查看或调整房间上限；未入房时保存本地开服配置 |
| `/mp kick uid <uid>`、`/mp kick id <name>` | 房主移除成员 |
| `/mp status`、`/mp id <name>` | 查看状态、修改名字 |

## 构建与验证

游戏、模组和协议版本统一配置在根目录 `Versions.props`。当前协议为 `4`，与旧协议不兼容，参与者需一起更新。

Interop 引用路径配置在 `MetaMystia.local.props`。独立服务器与模组共用网络程序集，服务器不初始化游戏。

```powershell
dotnet build MetaMystia.sln -c Release -p:DeployToGame=false
dotnet run --project src/MetaMystia.Network.Tests -c Release --no-build
dotnet run --project src/MetaMystia.Flow.Tests -c Release --no-build
dotnet run --project src/MetaMystia.Server -c Release --no-build -- 40815 16
```

`DeployToGame=false` 仅生成本地构建产物，模组输出到项目的 `bin/Release`。独立服务器按回车或 Ctrl+C 退出。

普通构建同时部署主模组到 `BepInEx/plugins`、预加载组件到 `BepInEx/patchers/MetaMystia`。网络程序集由 Costura 嵌入主模组；预加载组件在插件发现前执行主模组的模块初始化，注册内嵌依赖解析。

网络测试覆盖 TCP 顺序、阶段、房间管理、权限和消息隔离；流程测试覆盖入口确认、营业等待与场景转换，使用替代的游戏和传输入口。

已完成源码审计、编译和离线检查，预加载启动验证通过。双机验证按具体场景记录，启动与离线检查不代表完整营业／试炼已实测通过。
