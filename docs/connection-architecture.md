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
| `GameFlow`、`DayDestinationManager` | 游戏阶段、入房许可、入口确认后继续原游戏流程 |
| `PlayerProfile`、`PlayerManager.Network` | 玩家资料和运动转换、世界及房间角色显示 |
| `GameActions`、`GameMessageRules` | 显式玩法类型和路由，真实来源与延迟回调的房间校验 |
| `RoomClock` | 房主时钟校准；与网络存活检测分开 |

`MpManager`、`MpWire`、`MpSession`、`DirectTcp` 和旧握手、运动、资料 Action 已删除。连接状态、房间成员和游戏推进分别判断，不以“有客人”替代“已连接”或“房主”。

## 行为

- 局域网房主退出、断开或回主菜单时关闭本地服务器；客人退房即结束该次连接。
- 独立服务器允许停留在世界、创建和加入房间。房主离开后解散房间，其他人保留世界连接。
- 纯白天且角色准备完成时开放入房。准备进入下一阶段前等待关闭入房的服务器确认，再按已安装的成员名单检查就绪。失败时不继续使用未确认状态。
- 白天运动向世界共享，即使玩家已经组房。夜间运动只在同一房间的夜间玩家之间共享；实时转发、缓存快照和角色显示都遵守这一范围。聊天按草案保持世界广播。
- 世界资料包含名字、场景和皮肤的稳定资源包标识；完整资源表只在同房间共享，连接期间固定。缺失自定义皮肤时沿用本地回退显示。
- 房间人数含房主，范围为 1–256，且独立服务器房间不能超过服务器上限。降低上限保留现有成员；局域网房间与服务器上限一起更新。
- 管理请求无法确认、收发队列满时结束连接并提示原因。入房超时先撤销，撤销确认后独立服务器可保留世界连接；无法确认撤销则断开。远端直接中断 TCP 时，本端只能报告连接丢失。
- 每次入房有独立代号。退房或重连后，旧玩法消息、延迟回调和角色生成不能继续操作新会话。

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

游戏版本、模组版本、协议版本统一取自根目录 `Versions.props`。当前协议为 `2`，与旧连接协议不兼容，参与者需要一起更新。

项目使用 `MetaMystia.local.props` 中的 Interop 引用路径。独立入口与游戏端复用同一网络程序集及真实枚举，无头入口不初始化游戏。

```powershell
dotnet build MetaMystia.sln -c Release -p:DeployToGame=false
dotnet run --project src/MetaMystia.Network.Tests -c Release --no-build
dotnet run --project src/MetaMystia.Server -c Release --no-build -- 40815 16
```

`DeployToGame=false` 将模组输出到项目的 `bin/Release`，不复制到游戏目录；网络程序集由 Costura 嵌入模组。服务器入口按回车或 Ctrl+C 退出。

网络验证覆盖真实 TCP 的顺序、容量、取消、退出、权限、昼夜隔离和旧房间上下文。游戏部分完成相关逆向源码核对与编译，未开展游戏运行时验证；双机切场景、角色显示和营业同步仍不能据此声称实测通过。
