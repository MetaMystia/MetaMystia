# 顾客同步重构调研

基线：`07c29c3`，目标游戏 4.4.0e；原版依据为 4.3.0c 逆向导出。仅源码审计，没有游戏实测。编号 1–9 沿用[顾客同步审计](guest-fsm-audit.md)，符卡相关问题见[符卡联机适配](spell-multiplayer.md)，本文不重复。

## 现状

主机运行原版顾客逻辑，在生命周期节点由 Hook 调用 `GuestFSM.On*` 广播；客机用 Prefix 跳过自主决定，收到消息后放入该顾客的等待队列，由 `Do*` 重放。上菜由双方发起、主机裁定。幽幽子本体另有手动轨道（`YuyukoGuestSync`），自带会话、订单序号和订单版本。

| 环节 | 主机探测 | 消息 | 客机落地 |
|---|---|---|---|
| 生成 | `PostInitializeGuestGroup` Prefix | `GuestSpawn` | 手写重放构造与 `PostInitializeGuestGroup`，`TrySendToSeat` 被短路 |
| 入座 | `MoveToDesk` Prefix；出队由劫持的 `CheckAndSendFromQueue` 补发 | `MoveToDesk`、`SendFromQueue` | 手写 `ReplayTrySendToSeat`，出队收尾另行执行 |
| 排队 | `MoveToQueue` Postfix | `MoveToQueue` | 手写入队，耐心回调为空 |
| 点单 | `GenerateOrderInternal` Postfix | `GenerateOrder`（含资金、剩余点单数） | `PendingOrder` 暂存后调用原版，内层 Prefix 回填 |
| 上菜 | 面板 `Send`、`Cancel`、`OnPanelClose` | `ServeSellable`、`ConfirmServe` | 主机按菜品内容裁定后广播，回声按 `ActorUid` 过滤 |
| 评价 | `TryOverrideEvaluateByBuff` Postfix | `EvaluateOrder` | 写入菜酒，以 `OverrideEvalResult` 重放 `EvaluateOrder` |
| 离场 | `LeaveFromDesk`、`PatientDepletedLeave`、队列耐心闭包、`RepellInternal` | `GuestLeave`、`PatientDepletedDesk`、`PatientDepletedQueue`、`GuestRepell` | `Bypass` 放行原版入口，或手写最小清理 |
| 异常 | 任一端状态不符或等待超时 | `GuestKill`（仅主机发） | 强制清理并移出映射 |

审计第 1 项已由 `2841bc5` 修复（客机放行完整 `PatientDepletedLeave`）。

## 问题归因

| 编号 | 根因 | 表现 | 依据 |
|---|---|---|---|
| R1 | 顾客映射没有生命周期 | `GuestsMap` 为静态表，只有 `Kill` 和幽幽子重置会移除；正常离场、跨夜、退房都不清理（第 8 项）。按控制器查找每次线性扫描两遍全表。等待队列每帧在所有场景执行，只检查连接身份，不检查场景 | 源码推断 |
| R2 | 等待队列在 Hook 内同步排空 | `To()` 立即 `Drain()`，排队操作在原版方法中途执行。第 3 项即此：`GenerateOrderInternal` Postfix 推进到 `WaitingServe` 时新订单尚未 `PushToOrder`，`OrderSeq`（取 `AllOrdersCount`）仍是旧值，旧的空确认被当作当前订单 | 源码推断 |
| R3 | 订单身份隐式 | 普通顾客用 `AllOrdersCount` 作序号，它在原版方法内变化；首单另用 `IsFirstOrder`。幽幽子本体已用显式序号 | 已核对 |
| R4 | 状态不符即销毁，两端都如此 | 主机 Hook 在意外时序触发（符卡、未知路径）时 `Kill`，强制清理原版仍在运行的顾客并广播。客机超时同样销毁本地副本，主机不知情 | 已核对 |
| R5 | 手写重放与原版分叉 | 稀客生成不计 `AddServedGuest` 与图鉴到店，`OnLeaveCallback` 置空；`tryToJumpQueue`、自定义后处理不同步；出队拆成两条消息（第 2 项） | 已核对 |
| R6 | 等待条件靠 Hook 顺序推断 | `PostEvaluation` 返回即进入 `ContinueDecision`，此时吃饭计时仍在进行（第 6 项）。客机吃完的可靠时点是被跳过的 `MainOrderCycle` 入口 | 源码推断 |
| R7 | 每顾客队列丢失跨顾客顺序 | 主机先 A 离桌、后 B 入座；客机 A 的离场若排在 A 的阻塞项之后，B 因桌位未释放一并等待，直到 A 超时被销毁 | 源码推断 |
| R8 | 生命周期缺少通道 | 生成出口 1、4：主机静默 `MoveToSpawn`，两端状态停在 `Constructed`，客机无等待项、永不超时（第 4 项；不阻塞打烊）。排队客经打烊、快进、符卡离开时不发消息，状态停在 `Queued`，打烊时各端自清。伙伴上菜不同步：客机伙伴写本地订单、消耗本地库存，`EvaluateOrder` 被跳过，本地订单已满后该桌面板无法再打开 | 已核对；伙伴后果为源码推断 |
| R9 | 数值各端各算 | 心情初值 = 本地玩家等级加成 + 本地随机，两端系统性不同，只有赶客同步主机值（第 9 项）；心情影响客机耐心上限、心情条和符卡的心情归零驱逐。补菜恢复耐心（`FinishOrderStatus` → `AddPatient`）只在上菜端生效，主机仍按原耐心判定。连击只在赶客时同步。评价结果取自彩蛋和改判回调之前（第 7 项） | 已核对 |
| R10 | 剧情期间同步整体失效 | `ShouldSkipAction` 含 `InStory`：剧情中该端所有顾客 Hook 走原版，主机不广播、客机本地生成；顾客消息 `DiscardOnStory` 被丢弃 | 条件已核对；营业中途是否播放 timeline 待验证 |
| R11 | 上菜预测没有归属 | 冲突按菜品内容比较，同样两份菜无法区分；被替换的物品退回库存或保温箱而非原托盘。外部关闭面板只发空确认，其他端保留旧的待上菜显示和预填 | 源码推断 |
| R12 | 判断散落 | 顾客 Patch 中 `ShouldSkipAction \|\| !HasRoomPeers` 29 处，`IsBody`、`IsReimuProtectionGuest`、`IsManualGuest` 27 处；不可达分支抛异常；查找返回 null 时多处直接解引用（如客机 `EvaluateOrder_Prefix`） | 已核对；空引用触发条件为源码推断 |

其他：

- 金钱经 `FundEdit`、`TipEdit` 等按金额同步，不带顾客标识；接收端按本地 `ExtraFundModifier` 缩放。托盘个人所有，不同步。
- 超出忍耐的稀客：客机跟随主机结果进入同一分支，也执行本地的 `TriggerNegativeBuff`，两端各一次、随机独立；离场 `triggerLeaveBuff` 透传，客机也触发本地离场卡。
- [#145](https://github.com/MetaMystia/MetaMystia/issues/145) 为原版缺陷：`WorkSceneServePannel.OnExitExtern` 两次调用 `ReturnPass`，后者固定退回 `willServeFood`，第二次退回空值，酒水丢失。逆向注释称二进制如此，4.4.0e 待核对。
- 逆向中 `EventManager.HasCreatorBoxCoroutineEnd` 为 `onCreatorBoxGuestInstantiateLoop == null`，按字面普通营业会让所有顾客从出口 1 离场，与实际不符，还原有误；客机置空该字段的影响待核对。

## 目标结构

### 原则

1. 主机端状态只记录原版实际发生的事；时序与预期不符时记日志并跟随，不销毁顾客。
2. 客机端状态只用于门控重放。应用结果分为已应用、等待（带原因）、过期；只有主机明确的终态才销毁。
3. 终态事件（离桌、耐心耗尽、赶客、未入座离开）越过等待队列，按客机当前状态清理。
4. 排空只在安全点：消息接收与每帧 Tick；Hook 内只改状态。
5. 显式身份：`RuntimeId`（主机分配）与每顾客订单序号（主机提交订单时递增并随消息下发）。TCP 有序，不另加事件序号。
6. 有后果的数值由主机快照下发，客机不重算：心情、资金、剩余点单数、耐心，及连击与断连击计数。快照覆盖的数值不再依赖客机的本地分支。
7. 重放优先在 `Bypass` 下调用原版入口；必须手写的部分注明对应的原版方法。
8. 轨道显式登记：复制（普通、稀客）、脚本（幽幽子本体）、本地（灵梦保护、路人）。Hook 开头只查一次登记表。

### 组成

| 部分 | 内容 |
|---|---|
| 登记表 | `Scopes.Scene` 内的状态：`RuntimeId` 与控制器地址双索引、轨道、每帧 Tick；离开场景整体失效，取代静态 `GuestsMap` |
| 顾客记录 | 原 `GuestFSM` 的状态、订单序号、等待队列、就绪条件（已到座、已吃完），不再含 On/Do 方法 |
| 通道 | 每条消息一个文件，含主机发送、客机应用和接受的源状态；原 On/Do 成对方法移入对应消息 |
| Hook | 仍在各游戏类 Patch 中，开头统一取模式：单机、本地轨道、主机、客机 |

通道调整：

| 通道 | 变化 | 对应 |
|---|---|---|
| 入座 | `MoveToDesk` 与 `SendFromQueue` 合并，带是否出队 | 第 2 项 |
| 离场 | 离桌、桌上与队列耐心耗尽、赶客、未入座离开、排队客离开合并，带原因与快照 | 第 4 项、R7、R8 |
| 点单 | 带订单序号、心情、耐心 | 第 3、9 项 |
| 评价 | 同时带改判前与最终结果；客机吃完由 `MainOrderCycle` 入口标记 | 第 6、7 项 |
| 上菜 | 槽位版本号替代内容比较；请求带编号，拒绝时单独回复，物品退回发起者托盘；外部关闭发撤回；补菜恢复耐心由主机执行 | R9、R11 |

## 分步实施

| 步骤 | 内容 | 协议 | 风险 |
|---|---|---|---|
| G1 登记表 | 场景作用域、双索引、轨道登记、统一模式判断；`PluginHost`、调试命令、幽幽子绑定改用登记表 | 不变 | 低 |
| G2 队列纪律 | 安全点排空；主机不再销毁；现有离场消息越过队列；`ContinueDecision` 改在 `MainOrderCycle` 入口；主机拒绝过期确认 | 不变 | 中 |
| G3 通道 | 合并入座与离场、订单序号、数值快照、评价最终结果、未入座及排队客离开 | 变化 | 中 |
| G4 上菜账本 | 槽位版本、请求编号、拒绝回复、撤回、主机恢复耐心；#145 原版缺陷单独修 | 变化 | 高 |
| G5 接入 | 符卡本地驱逐改走离场通道；伙伴；幽幽子本体改用共享记录（可选） | 视情况 | 中 |

G1、G2 的核心逻辑不依赖游戏对象，可按 Flow 测试的方式链接源码、以地址作键离线测试：作用域结束清空、过期确认、终态越过队列、续单等待吃完。实测用例另补：出队入座、桌上与队列耐心耗尽、双端赶客、打烊时有排队、两人同时上菜、放入后外部关闭面板、补菜后耐心、连续两夜。

## 待决

- G3 一次升版本，还是拆分。
- 客机等待超时：维持本地清理、向主机请求快照重建，或只报警继续等待。
- 消息合并（16 类减至约 10 类），或保留类型只加共同基类。
- 剧情：先核实营业中途是否播放 timeline；若会，客机缓存顾客消息还是丢弃。
- 客机伙伴：联机时禁用、仅主机伙伴生效，或把伙伴上菜作为上菜请求同步。
- 客机的个人进度副作用（稀客到店记录、接待计数、委托完成、小费成就）归谁，与符卡的个人归属一并决定。
- 代码位置：新建 `Multiplayer/Guests/`，或留在 `Managers/`。
