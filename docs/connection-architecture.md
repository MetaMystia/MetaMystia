# 连接架构

## 职责

命名空间与程序集职责对应：核心库使用 `MetaMystia.Network`，模组接入层使用 `MetaMystia.Multiplayer`，玩法消息使用 `MetaMystia.Multiplayer.Actions`。模组内相应目录为 `Multiplayer/` 与 `Multiplayer/Actions/`；公共游戏枚举仍位于 `MetaMystia`。

服务器拥有连接、世界成员、房间、人数限制和入房许可。房主也是普通客户端，由当前房间的 `Host` 标识；没有固定房主 UID。

| 代码 | 职责 |
|---|---|
| `MetaMystia.Network/Connection`、`Protocol` | TCP 分帧、有序收发、容量限制和超时 |
| `MetaMystia.Network/Server` | 单一处理循环维护成员和房间，校验身份并路由 |
| `MetaMystia.Network/Client` | 请求与确认、状态快照、调用线程上的有序派发 |
| `MetaMystia.Network/LanSession` | 本地开服、房主预留、客人自动加入默认房间 |
| `MetaMystia.Mod/Multiplayer/GameSession` | 游戏连接入口、主线程派发、异步操作提示与清理 |
| `GameFlow`、`DayDestinationManager`、`BusinessStart` | 游戏阶段、场景转换规则、入口确认与营业开场等待 |
| `PlayerProfile`、`PlayerManager.Network` | 玩家资料和运动转换、世界及房间角色显示 |
| `GameActions`、`GameMessageRules` | 显式玩法类型和路由，真实来源与延迟回调的房间校验 |
| `RoomClock` | 房主时钟校准；与网络存活检测分开 |

`MpManager`、`MpWire`、`MpSession`、`DirectTcp` 和旧握手、运动、资料 Action 已删除。连接状态、房间成员和游戏推进分别判断，不以“有客人”替代“已连接”或“房主”。

## 行为

- 局域网房主断开时关闭本地服务器；客人退房即结束该次连接。自由白天与主菜单往返不触发断开。
- 独立服务器允许停留在世界、创建和加入房间。房主离开后解散房间，其他人保留世界连接。
- 主菜单与自由白天允许建房、入房；房间内仍有人加载、收尾或已提交共同入口意向时，暂停接纳新成员。服务器也检查入房者的阶段。
- 各自的日期、存档与剧情进度独立。自由白天读档、主菜单往返保留连接、房间与入房身份；只撤回本人旧入口意向，加载中的成员不能参加共同推进。
- 白天运动向世界共享，即使玩家已经组房。夜间运动只在同一房间的夜间玩家之间共享；实时转发、缓存快照和角色显示都遵守这一范围。聊天按草案保持世界广播。
- 世界资料包含名字、场景、玩法阶段和皮肤的稳定资源包标识；完整资源表只在同房间共享，连接期间固定。缺失自定义皮肤时沿用本地回退显示。
- 房间人数含房主，范围为 1–256，且独立服务器房间不能超过服务器上限。降低上限保留现有成员；局域网房间与服务器上限一起更新。
- 管理请求无法确认、收发队列满时结束连接并提示原因。入房超时先撤销，撤销确认后独立服务器可保留世界连接；无法确认撤销则断开。远端直接中断 TCP 时，本端只能报告连接丢失。
- 每次入房有独立代号。退房或重连后，旧玩法消息、延迟回调和角色生成不能继续操作新会话。

## 同步与断开

[场景流转图](scene-flow-audit.md) 中蓝色标记表示同步点，红色路径表示先断开再执行原操作。

| 位置 | 处理 |
|---|---|
| 白天进入营业／最终试炼 | 收集兼容意向 → 等待服务器关闭入房 → 全员接受入口 → 房主放行；继续各自原入口 |
| 确认前读档或返回菜单 | 撤回本人意向；若与房主提议交错，回复拒绝并取消该次提议，不执行营业或试炼副作用 |
| 已接受共同入口 | 结束白天事件、选店也属于共同流程；不能因场景仍是 `DayScene` 就允许直接读档 |
| 选店、准备 | 沿用共同选店、备菜表与就绪确认；处理较快成员在本地加载期间发来的准备数据 |
| 常规营业开场 | 各端完成原开场事件、装饰与伙伴初始化后报告就绪；房主确认全员夜间就绪，先广播放行，再启动原刷客与计时 |
| 最终试炼 | 保留首次、重修及混合入口，以及已有备菜、客人绑定、阶段推进和胜负同步 |
| 正常打烊／试炼返回 | 保留原房主裁定与游戏收尾。先回白天者可自由活动；下一次共同推进等待其他成员回到可参加阶段或退出 |
| 共同流程读档、回菜单、回退 | 在 `SaveManagement` 重置游戏状态前主动断开，取消等待中的续接 |
| 未接入同步的夜间入口、共同流程结局改道 | 在 `UniversalGameManager.LoadScene` 执行前断开；最终试炼的 `TryLeaveSession` 正常返回单独放行 |

阶段由玩家资料发布，不包含日期或统一玩法轮次。入口协调沿用 `DayDestinationManager.Round`，试炼备菜沿用原有准备编号；不为全部玩法消息新增轮次。TCP 保证同一连接的消息顺序，入口接受确认负责处理“本人已撤回、房主尚未收到”的交错。

旧入口在离开白天时清理；正常回到白天时保留较快成员已提交的下一次意向。备菜、营业开场等待等状态按自身生命周期重置。`GuestFSM` 队列存放已收到的顾客操作，不是发包队列；本次不更改其 RuntimeId 或引入全局防旧包机制。

## 命令

默认 TCP 端口仍为 `40815`，可由原有配置修改。

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

游戏版本、模组版本、协议版本统一取自根目录 `Versions.props`。当前协议为 `3`，新增玩法阶段、入口回复与营业放行消息，与旧连接协议不兼容，参与者需要一起更新。

项目使用 `MetaMystia.local.props` 中的 Interop 引用路径。独立入口与游戏端复用同一网络程序集及真实枚举，无头入口不初始化游戏。

```powershell
dotnet build MetaMystia.sln -c Release -p:DeployToGame=false
dotnet run --project src/MetaMystia.Network.Tests -c Release --no-build
dotnet run --project src/MetaMystia.Flow.Tests -c Release --no-build
dotnet run --project src/MetaMystia.Server -c Release --no-build -- 40815 16
```

`DeployToGame=false` 将模组输出到项目的 `bin/Release`，不复制到游戏目录；网络程序集由 Costura 嵌入模组。服务器入口按回车或 Ctrl+C 退出。

网络测试覆盖真实 TCP 的顺序、阶段更新、自由切场景保留房间、入房限制、容量、退出、权限、昼夜隔离和旧房间上下文。流程测试直接编译模组的入口协调、营业等待与场景转换规则，替代游戏和传输入口，覆盖确认交错、成员离开、混合试炼及正常／中断路径。

游戏部分完成相关逆向源码核对与编译，未开展游戏运行时验证；双机切场景、角色显示和完整营业／试炼仍不能据此声称实测通过。
