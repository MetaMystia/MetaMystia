# 料理对决（分队对抗）设计

基线：游戏 4.4.0e，模组 `f06a074`。原版依据为逆向仓库、4.4.0e IDA 与 DLC3 资源数据。第 1–3 阶段已实现并实测，用例见 `tests/runtime/cases/versus.md`；第 4 阶段未实现。

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

1. 房主在妖怪兽道联动交互点（原版 `CollabBehaviourComponent`）的菜单里选「料理对决」与节奏，或执行 `/mp versus [节奏]`；节奏三档对应挑战类型 8、10、9，默认中档。原版大赛没有重打入口。
2. 自由白天的成员弹出原版选择菜单：红队、蓝队、旁观、暂不。人少的一队在前；对话或剧情中的成员等结束后再弹；关掉后可在联动交互点重新打开。选队即就绪，同时上报本机 `HasDLC3`。
3. 全员已选、两队都有人、全员有 DLC3 时，沿用入口协调（新增对抗目的地）关闭入房，各端 `StartChallengeSession(类型, false, Difficulty.Normal, null)`。
4. 不消耗当天，没有奖励，不写剧情。

命令备用：`/mp team red|blue|watch`；房主 `/mp team <名字> red|blue|watch`、`/mp team shuffle`。

### 视角映射

每端把本队映射为原版玩家方、对方队映射为 Boss 方：占桌表、上菜面板拦截、两份连击、付款分流和结算面板沿用原版。旁观端固定红队为玩家方。

### 开场

- `GotoBossChallengeScene` 前缀：清空伙伴并跳过，场上没有 Boss 与任何伙伴。
- `OnBeforeChallengeStart` 直接回调，不执行调度事件，避免消耗处于大赛剧情中的玩家的节点。
- 各端向 `BossPartnerMatching` 注册一份大赛 Boss 资料的克隆，等级设为本机等级（`PartnerBossInfo.TipRate` 按自身等级取倍率），Boss 方桌子登记这个 ID，小费倍率差为 0；计分只在房主端，等同按房主等级。托管侧读不到 `bossMapping` 的内容，资料按 ID 3004、3003、3005 取第一个可用的。
- 全员进入夜间后，房主下发一次自己的食材、酒水表（去掉有人不能用的），客机在开场前替换按图鉴生成的部分。
- 不选伙伴：状态 1 发起打开伙伴选择面板，面板打开后立即以米斯蒂娅确认关闭（发起时关闭无效），原版回调照常放置角色；状态 3 重置 QTE 奖励并设 `disableReward`，移除温泉移速。
- 进入状态 3 的端上报就绪并停住；房主收齐后广播开始，各端进入状态 4。
- 只刷普客：`StartChallengeGuestInstantiateLoop` 的稀客参数改为 false。刷客仍只在房主端。

### 比赛规则

| 机制 | 规则 | 实现 |
|---|---|---|
| 占桌 | 先往面板放入物品或上菜的队占桌，客人离开释放；对方的桌打不开上菜面板 | 在现有上菜冲突裁定上增加：该桌属对方队则拒绝，未占则登记为操作者的队并广播；各端按视角写入占桌表并显示图标，他队占下本端正在上菜的桌时关闭面板、退回物品 |
| 计分 | 占桌队得付款和小费 | 房主 `GuestPay` 原版分流；对抗中不广播 `FundEdit`、`TipEdit`，房主在变化后广播两队分数与连击，各端写入 `EarnedFund`、`BossGetScore` 并刷新目标分 |
| 差评、赶客、耐心耗尽 | 付款与断连击记在桌的归属队，赶客记在赶客者的队；未占的桌耐心耗尽两队都断连击 | 原版 `PayByMood` 与这些路径的 `LoseAllCombo` 不查 Boss 占桌表；房主在调用期间设定归属队，他队的 `FundEdit` 改记 Boss 方，`LoseAllCombo` 改为 `SetBossCombo(0)` |
| 公平 | 两队按同一标准付款 | 付款、小费全在房主端按房主等级计算 |
| 厨具 | 共用；成品与存放物归开火、存放者的队 | 厨具交互入口（`CallCooker`）拦截他队的成品；原版调用方先取出再放入托盘或交换，不能只拦截 `Extract` |
| 保温箱 | 按队隔离 | 存取消息只在同队生效 |
| QTE 增益 | 关闭 | `QTERewardManager.disableReward` |
| 旁观 | 可走动，不能交互，不计分 | 拦截夜间交互入口与库存面板取出，房主拒绝旁观者的上菜；比分显示红对蓝 |

### 结束

- 计时以房主为准：客机状态 7 只显示，到时与否等房主的结束消息。
- 状态 8 清场由房主驱赶，客机随顾客同步离场。
- 进入状态 9 前，客机等房主的最终比分；各端回填 `EarnedFund` 为本队、`BossGetScore` 为对方（旁观为红、蓝），再运行原版面板与判定。同分为平局，模组另行公告。
- 返回走原版 `TryLeaveSession`；`CanKeepRoom` 放行白天进比赛与比赛回白天，同最终试炼。
- 成员断开，其队继续；房主离开、房间解散时，客机结束比赛回白天。

## 阶段

1. 骨架：队伍状态与显示、入口、去 Boss、跳过调度事件、开场就绪、统一结束与回白天。
2. 对抗：占桌、按队计分与连击、结算回填。
3. 隔离与公平：保温箱、厨具成品、统一库存、关闭 QTE 增益、只刷普客、不选伙伴、旁观限制。
4. 后续：三队以上、稀客与符卡归属、博丽大祭舞台入口、奖励。

每阶段在 `tests/runtime/cases` 补用例，三实例实测。

## 已核实

原版只在顾客离桌时释放玩家方桌，Boss 方桌由 Boss 行为释放，对决中两者都改由房主广播；评价时的连击、`GuestPay` 的付款与小费按 Boss 占桌表分流，差评离桌（`ExBadLeave` → `PayByMood`）、赶客与耐心耗尽的断连击不查；夜间交互只经 `WorkScenePlayerInputGenerator.Interact`，库存快捷键打开的是只读面板；QTE 增益来自所选伙伴的 `CurrentBuffReward`；比赛地图 8 桌（0–7）；直接调用 `StartChallengeSession` 传空 `Il2CppSystem.Nullable<int>` 正常；状态 3 前缀跳过原函数可停住主循环；进入前必须关闭白天常驻面板之上的面板，否则 `LoadScene` 抛异常并停在加载。
