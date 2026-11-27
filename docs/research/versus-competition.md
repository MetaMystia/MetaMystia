# 料理对决（分队对抗）设计

基线：游戏 4.4.0e，模组 `f06a074`。原版依据为逆向仓库、4.4.0e IDA 与 DLC3 资源数据；未实现，未经游戏实测。

在神灵庙料理大赛的比赛场景里，房间成员分红、蓝两队对抗，去掉原版 Boss。剧情中的料理大赛不变，联机时仍按未接入同步的入口退房。

## 原版依据

### 入口与收尾

`DaySceneDirector.StartChallengeSession(type, doNotFadeOut, difficulty, overrideIzakayaId)` 加载比赛地图 3006。`difficulty != None` 时原版（IDA）：

- 跳过全部阶段对话（`P1Start`…`P3End` 直接结束）。
- 不写 `DLC3_MausoleumCuisineCompetition_Result_P1/P2` 与伙伴交互点。
- 结束直接 `TryLeaveSession` 回白天并恢复行动次数，不经 `CloseIzakayaDelayed`，没有经验和资金结算。

挑战类型只决定锁定热情（资源数据）：8 为 20，10 为 60，9 为 100。单局 `singleRoundDuration` 180 秒，客源取店铺 2、5、8、12。

开场顺序：`GotoBossChallengeScene` 把 Boss 加入 `StatusTracker` 伙伴 → `RunTimeScheduler.OnBeforeChallengeStart` 调度事件 → `PartnerManager.Initialize(伙伴)` → `MainChallengeLoop`。主循环开头按本人图鉴把每种食材、酒水设为 100，结束时还原。

### 原版双方结构

| 环节 | 玩家方 | Boss 方 |
|---|---|---|
| 占桌 | 上菜送达后 `WorkSceneServePannel.InvokeOrderUpdate` → `TryAddPlayerOccupiedDeskCode`；客人离桌时释放 | `TryAddBossOccupiedDeskCode(desk, partnerId)` |
| 上菜面板 | `ShowOrder` 遇 Boss 桌直接返回 | — |
| 连击 | 原版连击 | `AddBossCombo`、`SetBossCombo(0)` |
| 付款 | `GuestPay` → `FundEdit` | `GuestPay` → `AddBossGetScore`；小费倍率按 `BossPartnerMatching[partnerId].TipRate` 减本人等级倍率 |
| 小费 | `TipEdit(Player)` | `TipEdit(Boss)` → `AddBossGetScore` |
| 判定 | `EarnedFund > BossGetScore` | |
| 结算面板 | `DLC3_BossBattleResultPannel` 显示两边比分，`myScore > bossScore` 时播放胜方特效，没有输赢字样 | |

`GuestPay` 用 `Dictionary.get_Item` 查 Boss 资料，Boss 桌的 `partnerId` 不在 `BossPartnerMatching` 时抛异常（IDA）。

### 主循环状态

`<MainChallengeLoop>d__59.MoveNext`（IDA）：

| 状态 | 进入时执行 |
|---|---|
| 0 | 清空库存、清空伙伴记录、锁定热情、安装双方占桌回调、移走玩家角色，`yield null` |
| 1 | 客源、菜谱、厨具、库存，打开伙伴选择面板，`Freeze`，等待选择 |
| 2 | `yield null` |
| 3 | `P1Start`/`P2Start`/`P3Start` |
| 4–6 | `Resume`，开始刷客与计时 |
| 7 | 每秒计时；到时停止刷客，等待符卡执行完 |
| 8 | 驱赶全部顾客，等待离场 |
| 9 | 打开结算面板，计算 `currentSucceed`，等待关闭 |
| 10 | `P1End`/`P2End`/`P3End` |
| 11–13 | 难度模式 `TryLeaveSession`，否则 `CloseIzakayaDelayed` |

## 设计

### 队伍状态

| 层 | 内容 | 实现 |
|---|---|---|
| 房间 | 成员 → 红、蓝、旁观；跨场次保留，新成员进人少的一队，离开即移除 | 房主持有，变化和新成员入房时广播完整快照；不改服务端协议 |
| 玩家 | 本机与远端玩家的 `Team` | 读房间队伍表，用于名字颜色和视角映射；不进世界资料 |
| 对局 | 开局冻结的名单、占桌、比分 | 场景作用域，比赛结束清理；局中改队不影响本局 |

### 入口

1. 房主在联动菜单选「料理对决」或执行 `/mp versus [节奏]`，节奏三档对应挑战类型 8、10、9，默认中档。
2. 自由白天的成员弹出原版选择菜单：红队、蓝队、旁观、暂不。人少的一队在前；对话或剧情中的成员等结束后再弹。选队即就绪，同时上报本机 `HasDLC3`。
3. 全员已选、两队都有人、全员有 DLC3 时，沿用入口协调（新增对抗目的地）关闭入房，各端 `StartChallengeSession(类型, false, Difficulty.Normal, null)`。
4. 不消耗当天，没有奖励，不写剧情。

命令备用：`/mp team red|blue|watch`；房主 `/mp team <名字> red|blue|watch`、`/mp team shuffle`。

### 视角映射

每端把本队映射为原版玩家方、对方队映射为 Boss 方：占桌表、上菜面板拦截、两份连击、付款分流和结算面板沿用原版。旁观端固定红队为玩家方。

### 开场

- `GotoBossChallengeScene` 前缀：清空伙伴并跳过，场上没有 Boss 与任何伙伴。
- `OnBeforeChallengeStart` 直接回调，不执行调度事件，避免消耗处于大赛剧情中的玩家的节点。
- 各端向 `BossPartnerMatching` 注册一份 Boss 资料的克隆，等级设为房主等级（`PartnerBossInfo.TipRate` 按自身等级取倍率，源码推断），Boss 方桌子登记这个 ID，小费倍率差为 0。
- 房主下发统一的食材、酒水表，替换按图鉴生成的部分。
- 伙伴选择只换外观：选择后清空 QTE 奖励与温泉移速，外观同步给其他端。
- 选完伙伴的端上报就绪，在状态 3 停住；房主收齐后广播开始，各端进入状态 4。
- 只刷普客：`StartChallengeGuestInstantiateLoop` 的稀客参数改为 false。刷客仍只在房主端。

### 比赛规则

| 机制 | 规则 | 实现 |
|---|---|---|
| 占桌 | 先上菜送达的队占桌，客人离开释放；对方的桌打不开上菜面板 | 在现有上菜冲突裁定上增加：该桌属对方队则拒绝，未占则登记为上菜者的队并广播；各端按视角写入占桌表并显示图标 |
| 计分 | 占桌队得付款和小费 | 房主 `GuestPay` 原版分流；对抗中不广播 `FundEdit`、`TipEdit`，房主在变化后广播两队分数与连击，各端写入 `EarnedFund`、`BossGetScore` 并刷新目标分 |
| 公平 | 两队按同一标准付款 | 付款、小费全在房主端按房主等级计算 |
| 厨具 | 共用；成品归开火者的队 | 他队不能取出 |
| 保温箱 | 按队隔离 | 存取消息只在同队生效 |
| QTE 增益 | 关闭 | 不触发、不广播 |
| 旁观 | 可走动，不能交互，不计分 | 拦截夜间交互入口；比分显示红对蓝 |

### 结束

- 计时以房主为准：客机状态 7 只显示，到时与否等房主的结束消息。
- 状态 8 清场由房主驱赶，客机随顾客同步离场。
- 进入状态 9 前，客机等房主的最终比分；各端回填 `EarnedFund` 为本队、`BossGetScore` 为对方（旁观为红、蓝），再运行原版面板与判定。同分为平局，模组另行公告。
- 返回走原版 `TryLeaveSession`；`CanKeepRoom` 放行白天进比赛与比赛回白天，同最终试炼。
- 成员断开，其队继续；房主离开、房间解散时，客机结束比赛回白天。

## 阶段

1. 骨架：队伍状态与显示、入口、去 Boss、跳过调度事件、开场就绪、统一结束与回白天。
2. 对抗：占桌、按队计分与连击、结算回填。
3. 隔离与公平：保温箱、厨具成品、统一库存、关闭 QTE 增益、只刷普客、伙伴只换外观、旁观限制。
4. 后续：三队以上、稀客与符卡归属、伙伴能力、博丽大祭舞台入口、奖励。

每阶段在 `tests/runtime/cases` 补用例，三实例实测。

## 待核实

- 联动交互点所在地图与可用时机。
- 比赛地图桌数。
- 旁观需要拦截的全部交互入口（夜间 `WorkScenePlayerInputGenerator.Interact` 之外的面板快捷入口）。
- 挑战内临时外观能否用现有皮肤同步表达。
- 比赛中 QTE 增益的来源。
- `StartChallengeSession` 直接调用时 `Il2CppSystem.Nullable<int>` 参数的运行时表现（Hook 已知不可用）。
- 主循环在状态 3 停留的方式（`MoveNext` 前缀不执行原函数并返回 true）。
