# AI 游戏测试实践

本目录为独立调试载荷，不参与 Mod 编译。通过 Il2cppConsoleMod 操作运行中的游戏；所有 HTTP 请求均须走提权审批，携带 `X-Debug-Token`。Token 仅作为调用参数传入，不写入文件。

这份记录面向接手的操作 agent 和开发 agent：前者据此选择动作、判断结果，后者据此定位代码、复现行为、验证修改。维持“单份主文档 + 多个小脚本”，暂不建立自动游玩框架。

默认 Agent 只需完成开发任务，无需开展本目录的游戏内验证实践或请求用户启动游戏。只有用户明确要求游戏实测、操作验证或探索时，才执行下述接手和操作流程；开发任务本身要求的源码审计、编译和必要测试仍照常进行。项目约定见 [AGENTS.md](../../AGENTS.md)。

## 接手时先读

- 本次记录日期：2026-09-07。游戏内为 7 月 11 日；最后观察到 `Work` 场景、“人里推车”和“雀酒屋绝赞营业中”。用户随后结束测试并自行关闭游戏，当前不应继续请求接口。
- 截图曾显示 MetaMystia v0.27.0、1 个资源包、Multiplayer Off。未完整记录游戏版本、资源包版本和初始存档副本，不能视为严格可复现的基准测试。
- 已走通：继续存档 → 白天移动与互动 → 采集、消费、邀请、委托 → 日终结算 → 人里推车 → 准备菜单 → 开始营业。未完成一整晚经营，也未核验存档重载。
- 文中坐标、余额、羁绊、库存和资源编号均来自本次存档。历史轮次的“最后位置”只描述当时状态；本次临时截图已清理，截图脚本保留供后续使用。
- 新会话先读取工具 `/help`、当前 UI 和场景，再按需初始化脚本。不要重放整段历史，或默认游戏仍在营业准备页。

## 从 agent 角度理解游戏

每次决策先回答六件事：我在哪个阶段、正在显示哪个面板、目标是谁、动作是否可用、会花什么、怎样证明完成。

| 要理解的状态 | 主要证据 | 容易误判的地方 |
| --- | --- | --- |
| 游戏阶段 | Unity 场景 `Day` / `WorkPrep` / `Work`，加载时为 `Load` | “前往开店”在选店页和准备页各出现一次 |
| 当前地图与位置 | `CurrentActiveMapLabel`、玩家位置、切图状态 | 缓存 UI 的“即将前往”不代表实际地图 |
| 目标身份 | 角色标签或资源 ID，加当前位置和互动范围 | 原版与资源包角色可以同名 |
| 操作条件 | 输入可用性、当前面板、按钮状态、库存、羁绊、冷却 | 对象激活不等于可见，也不等于可操作 |
| 动作代价 | 行动点、时钟、余额和库存的前后差值 | 白天现实时间等待与游戏时钟是两回事 |
| 完成状态 | 邀请名单、委托队列、购物结算、实际场景 | “请求返回”“台词答应”“状态已登记”有不同完成时机 |

采用“观察 → 读代码 → 一个动作 → 等待完成 → 再观察”的循环。移动可以重试，但先确认已停止；购买和邀请不能因响应不清楚就再次提交。出现超时、切图或对象销毁时，重新观察后决定下一步。

### 操作入口决定验证范围

| 本轮方式 | 验证了什么 | 没有覆盖什么 |
| --- | --- | --- |
| `UpdateInputDirection` + 游戏物理移动 | 碰撞、绕路、触发地图出口 | OS 键盘、完整 Input System 输入链 |
| `TryInteract`、按钮选择/提交/长按入口 | 互动目标、控件回调及后续游戏流程 | 鼠标像素命中、所有按键绑定 |
| `RegisterToDaily*`、`RegisterToCookers` | 准备配置方法、拥有检查、最终菜单与进入营业 | 逐个拖拽/选择菜单、全部 UI 容量与去重检查 |
| 只读状态和帧末截图 | 解释动作结果、定位碰撞与隐藏对象 | 自动证明整条业务流程正确 |

没有修改坐标、余额或时钟字段来伪造操作成功；快捷传送和结束白天使用游戏原有流程。准备菜单则直接调用已审计的配置方法，必须保留这个区别。

## 把实测反馈给开发

操作 agent 提交最小复现和状态差值，开发 agent 对照逆向代码、项目 Hook 与 Interop 类型定位问题。修复后用同样前置条件重放，分别检查界面结果和业务状态；两种角色可以由同一个 agent 先后承担。

| 实测现象 | 对代码理解和调试的帮助 |
| --- | --- |
| 邀请对白中 `attempted=True`、`invited=False`；最后一句结束才登记并扣点 | 检查邀请同步或通知是否放在对话完成回调，不能只在按钮提交时判断成功 |
| 委托先登记，日终才入库 | 将“接受委托”和“领取产物”分开验证；不能用库存未变判断委托失败 |
| 地图内有原版与扩展同名角色 | 定位和同步应核对标签，避免只比较显示名 |
| 多次切图仍能枚举到旧地图对象 | 检查对象生命周期和地图归属，不能只凭 `FindObjectsOfType` 结果执行动作 |
| 商品获取文字有双份，余额只扣一次 | 重复 UI 文本不等于重复结算；查余额、库存或状态记录后再报业务错误 |
| 直线移动受阻，绕行成功 | 先排查碰撞和路线，再判断移动输入、同步或角色控制失效 |

以上是排查方向，不是已发现的 Mod 缺陷。本轮为单机测试，联机仍需分别观察主机、客机、角色归属与消息处理次数。

后续每个案例在本文追加以下短记录即可；需要完整证据时再附相对路径文件：

```text
目标 / 完成条件：
环境：游戏与 Mod 版本、资源包、单机或主客机、存档前置条件
观察前：场景、目标标签、相关数值
操作：载荷或按钮、参数、源码入口、是否跳过 UI
预期 / 实测：前后差值、对话或协程完成点
证据：快照、日志或截图；仅记录实际保存的文件
结论：运行时实测 / 源码推断 / 待验证
开发跟进：相关代码、最小复现、修复后检查项
```

下面是本次会话的历史记录；尚无独立保存的逐请求日志，不应把摘要当作完整原始证据。

## 第一轮：UI 与基础互动

1. 主菜单选择“继续”，进入已有存档：7 月 11 日，10:00，夜雀小屋。
2. 读取 UI 文本、可交互按钮、当前选择，以及玩家位置和附近互动区域。
3. 向右行走进入电话范围，通过玩家 `TryInteract()` 打开电话。
4. 电话 → 联系好友 → 妖怪兽道 → 露米娅，完成问候与一段闲聊，再逐级退出并挂断。
5. 通过帧末截图识别餐桌阻挡，沿左侧地毯绕行，正常触发出口进入妖怪兽道。
6. 第一轮结束：妖怪兽道，小屋门外，玩家约 `(-9.99, -0.24)`，已停止，游戏时间仍为 10:00。

闲聊会走游戏原有的聊天记录与羁绊逻辑；本轮未核对羁绊变化数值，也未主动存档。没有改坐标、改资源或安装 Harmony 补丁。

## 第二轮：移动、采集与行动点

目录已改名为 `MetaMystia.AITest`。本轮从小屋门外出发，绕过楼梯栏杆、树丛、岩壁和平台装置，采集后往返竹林，最后通过家门回到小屋。所有位移均走正常移动与碰撞逻辑，没有传送或修改地图状态。

| 操作 | 剩余行动点 | 时钟 | 实测结果 |
| --- | ---: | --- | --- |
| 起点及多段行走 | 16 | 10:00 | 行走和现实时间等待不耗点 |
| 采集 `BeastForest_Plant_A` | 15 | 10:30 | 露水 +8；该点冷却变为 3 |
| 采集 `BeastForest_Plant_C` | 14 | 11:00 | 蜂蜜 +2、蝉蜕 +2；露水点冷却降至 2 |
| 妖怪兽道 → 竹林 | 13 | 11:30 | 收费出口耗 1 点；露水点冷却降至 1 |
| 采集 `BamBooForest_Bamboo` | 12 | 12:00 | 竹子 +3、竹笋 +1；竹子点冷却变为 23 |
| 竹林 → 妖怪兽道 | 11 | 12:30 | 返回也耗 1 点；蜂蜜点已恢复可采 |
| 行走返家并进入小屋 | 11 | 12:30 | 家门切图免费 |

最后位置：`Home`，约 `(-0.23, -3.35)`。本轮三次采集、两次收费跨图，共耗 5 点；没有主动存档。

### 时钟与采集规则

- 普通白天为 10:00～18:00，共 16 点；1 点对应 30 分钟。当前时钟可理解为 `18:00 - 剩余点数 × 30 分钟`。
- 行走距离、绕路和现实等待不推进白天时钟，也不降低采集冷却。
- 采集统一调用 `CollectTrackedCollectable`，随后扣 1 点；出口通过 `shouldCostAction` 决定是否扣 1 点。家门需要互动，实测竹林出口自动触发。
- 扣点会同时减少所有已追踪采集点的冷却，包括其他地图的点；跨图不会把库存或冷却重置。
- 采集先设置冷却，再扣掉本次行动，所以配置 4 点的植物采完显示 3 点，配置 24 点的竹子采完显示 23 点。
- 可采条件包括：已开放、冷却为 0、当前处于采集时段。露水点时段为 10:00～11:00：12:00 时冷却虽归零，仍不可采。蜂蜜时段为 10:00～18:00：12:30 时冷却归零，可采。
- 实际产量可能受概率产物、重复采集与道具加成影响，不能把本轮产量当作固定掉落。长冷却跨天行为未实测；后续第五轮通过快进走完日终流程。

### 导航经验

- `Transform.position` 不是互动位置。植物和出口的 Collider 中心都可能偏移；竹林东出口原点约 `(34.15, -25.40)`，实际触发中心约 `(36.34, -32.83)`。必须核对真实触发范围和 `allInteractables`。
- `bounds` 是包围框，不等于精确可走区；即使目标在框内，也可能有树木或岩石阻挡。
- 定点移动加入到达、停滞、4 秒超时、输入禁用和切图停止条件。`blocked` 只表示位移停滞，需结合截图诊断，不能据此断言具体障碍。
- 平台侧边不可直接横穿到楼梯，先向上到入口，再沿楼梯下行。平台装置也有碰撞，需要从下方绕行。
- 直线追踪不会自动绕障。遇到树丛、岩壁时，读取截图，退回已知道路，增加中间点。返程重用已走通路线明显更高效。
- 切图时导航记录的是停止瞬间旧地图坐标；切图完成后，必须重新读取新地图与出生点，不能继续使用旧对象列表或旧目标坐标。

实测可走的主要中间点（用于本地图参考，不是通用寻路数据）：

```text
小屋平台下楼：(-10,-0.24) → (-5.37,-0.24) → (-5.37,1.24) → (-3,1.24) → (-3,-4.7)
露水采集：(-3,-4.7) → (-4.5,-9.3) → (-4.7,-10.65)
绕回北侧：(-4.7,-10.65) → (-8,-11) → (-3,-4.7) → (7,-3.3)
东侧竹林入口：(7,-3.3) → (18,-8.8) → (30,-8.8) → (34.15,-23.8) → (36.3,-32.8)
竹林竹子点：出生约 (39.88,24.07) → (47.8,23)
竹林返程：(47.8,23) → (39.9,24.1) → (39.9,35.2)
```

小屋平台第一段直走到 `(-4.9,-0.24)` 会在约 `(-5.37,-0.24)` 被栏杆阻挡，上述路线已改用该停靠点。

## 第三轮：博丽神社、赛钱与邀请

从小屋正常步行，经妖怪兽道东侧道路进入博丽神社，完成 1000 円赛钱，并邀请比那名居天子夜间来店。

| 操作 | 行动点 / 时钟 | 核验结果 |
| --- | --- | --- |
| 小屋 → 妖怪兽道 | 11 / 12:30 | 免费切图 |
| 妖怪兽道 → 博丽神社 | 10 / 13:00 | 扣 1 点 |
| 赛钱 1000 円 | 10 / 13:00 | 余额 2259173 → 2258173；累计赛钱 87600 → 88600 |
| 邀请天子并完成对话 | 9 / 13:30 | `Tenshi`（ID 9）的 `attempted=True`、`invited=True` |

### 赛钱箱

- 接近箱子后走玩家互动入口，经过灵梦问候，菜单实际显示 100、500、1000 円及取消；本次选 1000 円。
- 捐款回应对话结束后才扣款。本次不消耗行动点；使用计数从 0 变 1，碰撞互动入口关闭，当天不能再次使用。
- 逆向代码中，至少 500 円会安排灵梦夜间正面符卡；至少 1000 円另安排“实惠”标签料理制作时间减少 20%。本次夜间效果队列从 0 增至 2；后续虽进入营业，仍未单独核验这两项效果的实际执行。
- 捐款还调用灵梦羁绊增加逻辑；当前灵梦已满 5 级，未观察到经验变化。虽然对象有 `finalDonateNum=25500`，本次菜单没有该选项，不能仅凭字段推断可选金额。

### 天子邀请

- 走到天子身边互动，结束问候后选择“邀请（耗时30分钟）”。当前羁绊 5 级，审计的邀请逻辑为必定接受；低等级存在失败概率。
- 邀请开始即记录“已尝试”。天子答应后，最后一句仍在显示时，实测 `invited=False`、时钟仍为 13:00；结束整段对话后才登记受邀名单并扣 1 点。
- 最终停留在神社天子身边，13:30、剩余 9 点；已确认邀请登记，尚未验证夜间到店。

### 本轮导航补充

妖怪兽道北侧高台无法直接横穿到东侧神社道路。结合截图退回南侧，再走 `(18,-8.8) → (29.9,-8.8) → (29.9,11.7)`，成功触发神社入口。

神社出生约 `(6.73,-41.61)`，沿 `(6.7,-16) → (4.5,8) → (4.5,8.6)` 接近赛钱箱；再经 `(6.7,-12) → (12.1,-16)` 到天子身边。坐标仅作当前地图参考，移动结束后仍须核对实际位置与互动范围。

## 第四轮：货币传送与琪露诺委托

- 通过 `DaySceneSustainedPannel.OpenFastTravelPanelParameterless()`（地图按钮原有入口）打开快捷传送面板，选择红魔馆。面板提供“确认前往（15:00）”和“八云传送（300¥）”。
- 选择八云传送，余额 2258173 → 2257873；地图变为 `ScarletMansion`，时钟保持 13:30、行动点保持 9。使用了游戏正常付费流程。
- 出生约 `(12.93,-18.86)`。直走琪露诺时在小恶魔旁停滞，截图确认前方树丛；经 `(25.2,-24) → (43.9,-21.8)` 绕行成功，琪露诺互动范围为真。
- 与琪露诺交谈，选择“委托采集（耗时30分钟）”，完成全部回应。`HasCommission("Cirno")` 从 `False` 变为 `True`，时钟推进至 14:00、行动点降至 8，余额不再变化。
- 委托要求羁绊至少 5 级、有对应采集地图且当前没有该角色待处理委托。提交时登记委托，对话结束时扣 1 点；产物在后续 `ReceiveCommissionsAsync()` 中入库，本轮只完成委托请求，未领取产物。
- 地图父控件也包含子节点文本，可能导致按文本匹配出现多个结果；本轮使用快照中的唯一按钮实例 ID 选择红魔馆与付费选项，不能跨场景复用这些 ID。

## 第五轮：原版商店、结束白天与人里推车

- 两位“小恶魔”显示名相同：原版标签为 `Koakuma`，资源包角色为 `_ResourceExample_Koakuma`。从西侧接近原版位置 `(24.16,-21.51)`，在 `(23.57,-21.89)` 仅原版进入互动范围。
- 原版商店使用 `OnBuyAll`（“我全都要”）加入全部库存，再提交确认购买。购入阿芙加朵 9、红雾 5、红魔馆红茶 5，共 398 円；余额 2257873 → 2257475，时钟仍为 14:00。
- 离开互动范围后，通过快进按钮 `TryExecuteHold` 进入正常长按流程，时钟变为 18:00。结束日终对话后，自动结算琪露诺委托，并显示产物汇总。
- 汇总面板上 `TryInvokeCurrentCancel()` 仍未生效，使用 `MultipleGetProductsPanel.ClosePanel()` 关闭成功。选店地图选择人间之里，再提交 Lv1，实际名称为“人里推车”，工作桌和客人桌各 3 张。
- 确认“前往开店”后已进入 `WorkPrep` 营业准备场景，`LoadingSceneManager.IzakayaMapIndex=3`。尚未提交准备页的“前往开店”，未开始夜间营业。

补充审计：逆向仓库的 `DayScene/UI/DaySceneShopPannel.cs`（全选购物车与购买）、`Common/UI/ExtendedSustainedPannel.cs` 和 `DayScene/UI/DaySceneSustainedPannel.cs`（快进）、`DayScene/SceneManager.cs`（日终顺序）、`Common/UI/MultipleGetProductsPanel.cs`（汇总关闭）、`DayScene/UI/DaySceneIzakayaSelectorPannel.cs`（选店确认）。以上均位于 `src/Assembly-CSharp/`。

## 第六轮：准备菜单并开始营业

最终准备配置经 `prep-state.cs` 读取确认：

| 类别 | 配置 | 本次资源 ID |
| --- | --- | --- |
| 料理 | 大江户船祭、白雪、山泉双色果盘 | 2008、19、11000 |
| 酒水 | 十四夜、雀酒 | 20、12 |
| 厨具槽 0 | 极·料理台 →「魔人经板」 | 19 → 5000 |
| 厨具槽 1 | 极·油锅 → 极·煮锅，满足白雪的要求 | 17 → 15 |
| 厨具槽 2 | 保留极·烧烤架 | 16 |

- 上架时两道菜板料理可制作次数分别为 328、198，白雪为 838；十四夜库存 93、雀酒 170。数量只描述当时条件，不保证后续经营够用。
- 使用准备面板调用的同一组配置方法，先下架旧菜单，再注册新菜单，保留拥有检查并刷新完成条件。原始槽位与脚本预期不符时，`prep-menu.cs` 会拒绝执行。
- 本次用户称“DLC5 的菜板”，实际装备并核验的是 ID 5000「魔人经板」。资源名称与说明已读取，但没有独立核验 DLC 归属；后续应按完整名称确认，不能仅从编号或外观推断。
- 长按准备页 `m_GotoWorkButton.ExternalStartHold()` 后，观察到 `Scene: Work`、“人里推车”和“雀酒屋绝赞营业中”。这是本轮最终完成证据，未继续验证做菜、上菜、结账或打烊。

## 脚本用法

以下命令从仓库根目录执行。先由用户启动游戏和调试服务、提供本次 Token；读 `/help` 核对接口。执行所有 HTTP 命令的工具调用必须指定 `sandbox_permissions: require_escalated`；脚本自身不会申请 Codex 提权。游戏已关闭时不要试运行。

```powershell
& './src/MetaMystia.AITest/Invoke-Debug.ps1' -Token '<本次 Token>' -File './src/MetaMystia.AITest/ui-snapshot.cs'
& './src/MetaMystia.AITest/Invoke-Debug.ps1' -Token '<本次 Token>' -File './src/MetaMystia.AITest/day-walk-init.csx' -Endpoint script
```

当前服务固定为回环地址端口 18765。`/exec` 接收带 `Payload.Execute()` 的完整 C#，用于单次执行；`/script` 保留会话定义，用于跨帧协程。主机重启或脚本会话重置后定义失效；`/script` 的 `using` 和类型不能当作下一次会话自带环境。

下表“通用”只指可复用方法，仍依赖本游戏与当前 Interop；场景专用脚本不是可直接套用的自动用例。

| 文件 | 用途 |
| --- | --- |
| `Invoke-Debug.ps1` | 发送载荷；失败时显示工具错误，不把“请求成功”当作游戏操作成功 |
| `ui-snapshot.cs` | 读取精简 UI；文本、控件 ID、选择状态，适用于 UGUI/TMP |
| `ui-focus.cs` | 修改 `Target` 后选择唯一匹配按钮；当前游戏的列表选择会触发自动滚动 |
| `ui-submit.cs` | 修改 `Target` 后提交唯一可见按钮；文字含换行时须保留原始换行 |
| `ui-cancel.cs` | 取消入口实验；电话地区菜单实测无效，不能当作可靠返回方法 |
| `continue.cs` | 提交主菜单“继续”，加载已有存档 |
| `day-observe.cs` | 读取玩家状态、最近 16 个互动区域及是否进入互动范围 |
| `day-walk-init.csx` | 在 `/script` 中定义短步移动工具，启动托管协程 |
| `day-walk.csx` | 修改方向与秒数后执行；时长限制在 0.02～2 秒，结束时自动停止 |
| `day-walk-status.csx` | 读取移动结果与时间缩放 |
| `day-motion-check.cs` | 检查移动标记、速度、刚体和附近碰撞体 |
| `day-nav-init.csx` | 定义 `AITestNav.Go(x,y)`，正常步行追踪一个中间点；用 `AITestNav.Status` 读结果 |
| `day-world.cs` | 读取地图、行动点、时钟，以及采集点和出口的实际 Collider 范围 |
| `shrine-state.cs` | 核验赛钱金额、使用次数、夜间效果队列与天子邀请登记 |
| `gather-info-init.csx` | 定义 `AITestGather.Read(key)`，读取采集配置、库存、冷却和可用状态 |
| `day-interact.cs` | 调用玩家当前互动入口，不直接调用远处目标 |
| `dialog-next.cs` | 单次对话确认；可能只补全文字，须再次观察 |
| `capture.csx` | 在 `/script` 中安排帧末截图，暂存在会话内存；当前宿主为白天玩家 |
| `Save-Capture.ps1` | 获取截图并覆盖本目录 `capture.png`，不把 Base64 输出到对话 |
| `prep-state.cs` | 仅在营业准备页读取当前菜单、三个厨具位和本次目标库存；不是跨场景探针 |
| `prep-menu.cs` | 本次场景专用配置载荷；固定三道菜、两款酒和槽位，变更前必须读源码与当前状态 |

优先复用：`ui-snapshot`、`ui-focus`、`ui-submit`、`day-nav-init`、`day-interact`、`dialog-next`。`day-walk*` 保留为短步实验，`day-motion-check` 用于诊断，`ui-cancel` 保留为失败案例。`shrine-state`、`prep-*` 的目标与编号是本轮特定条件。

脚本多数返回文本，没有统一机器可读的成功状态。`unavailable`、`blocked`、`Expected one button` 等也可能出现在 HTTP 成功响应中，必须阅读结果。`prep-menu.cs` 不适用于锁定菜单或任意店型，也不能自动证明每道菜都有对应厨具；本轮另外进行了源码和实际配置核验。

`.cs` 用 `/exec`，`.csx` 用 `/script`。移动先执行初始化，再执行动作；会话重置或游戏重启后重新初始化。截图先执行 `capture.csx`，帧末完成后运行：

```powershell
& './src/MetaMystia.AITest/Save-Capture.ps1' -Token '<本次 Token>'
```

`Invoke-Debug.ps1` 也支持 `-Code` 发送短语句，与 `-File` 二选一。先执行导航或采集信息初始化，再调用：

```powershell
& './src/MetaMystia.AITest/Invoke-Debug.ps1' -Token '<本次 Token>' -Code 'AITestNav.Go(-3f, 1.24f)' -Endpoint script
& './src/MetaMystia.AITest/Invoke-Debug.ps1' -Token '<本次 Token>' -Code 'AITestNav.Status' -Endpoint script
```

目标坐标必须来自当前地图观察。动作尚未完成时不要启动另一套移动工具。发送端请求体须保留 `byte[]` 类型；PowerShell 条件表达式会展开数组，不显式声明类型可能把源码变成数字串，导致大量编译错误。

## 经验与边界

- **每次动作后核验。** 返回 `Submitted` 只代表调用返回；必须观察新界面或坐标。场景加载、对话、滚动和互动会跨帧完成。
- **激活不等于可见。** 首次直接枚举得到大量缓存面板。结合 Canvas、透明度、裁剪状态筛选后，对话通常只需十余行。该筛选不能证明像素未被遮挡或位于屏内，也不是鼠标命中测试；室外快照仍读到“即将前往”文字，但截图没有显示，需继续改进筛选。
- **路径不唯一。** 同一列表有多个同名 `Selection(Clone)`。用组件实例 ID 或唯一文本定位，ID 仅在当前对象生命周期内有效。
- **选择和确认分离。** `UIButtonBase.CallSubmitAction()` 对 `PointerClickAsSelect` 先选择再返回。可先 `ui-focus`，待滚动完成再 `ui-submit`；不要盲目重复提交。
- **可见条目不是完整列表。** 地区列表有屏外内容；选择目标会调用游戏的 `SnapToTransform`。`ui-focus` 搜索全部激活且可交互的匹配，歧义时拒绝执行。
- **UI 读取与场景导航互补。** 文本适合菜单和台词；截图帮助识别餐桌。靠目标坐标直走可能撞墙，位移为零不能直接认定输入失效。
- **移动保留物理碰撞。** 使用 `UpdateInputDirection`，由游戏 `FixedUpdate` 推进，结束调用 `ExternalStop`。第二轮已验证定点移动与停滞检测；尚未做自动寻路和长期稳定性测试。
- **输入入口不是物理键盘。** 本轮验证的是游戏方法与控件提交入口；没有验证 OS 按键、完整 Input System 输入链或夜间经营。
- **Interop 的值类型可能是包装类。** `default(InputAction.CallbackContext)` 在本环境触发空对象转换错误；`new InputAction.CallbackContext()` 成功。仅因已审计 `DialogPannel.Interact` 不读取参数，才可这样调用。
- **不能把参数替代方法推广到所有输入回调。** 快进和商店的本次入口不读取该参数；选店的 `ConfirmChoiceStart` 会 `ReadValue<float>()`，不能照抄空回调上下文。
- **IL2CPP 集合不等于 .NET 集合。** 对商店的 `List<Product>` 直接调用 LINQ `Select` 编译失败；改用与实际集合兼容的遍历，不能靠反射绕过类型问题。准备页读取已使用 `foreach`。
- **协程接口需匹配。** 最初 `WrapToIl2Cpp` 未解析成功；使用 `BepInEx.Unity.IL2CPP.Utils` 的 `StartCoroutine(IEnumerator)` 扩展通过实测。跨请求执行的协程放在 `/script`，不用可回收的 `/exec` 载荷承载。
- **返回和取消不能混同。** `UILogicalUnit.TryInvokeCurrentCancel()` 本次没有使电话菜单返回；使用明确的“返回上级”和“挂断”按钮成功。
- **截图必须等帧末。** `WaitForEndOfFrame` 后截取、编码 PNG，再释放临时纹理。当前脚本只在白天场景验证，其他场景需要合适的协程宿主。

## 审计依据

逆向仓库位置沿用仓库根目录的本地说明。以下路径相对于逆向仓库：

- `src/Assembly-CSharp/MainScene/UI/MainMenuPannel.cs`：继续按钮与存档加载。
- `src/DEYU.AdaptiveUISystem/DEYU/AdpUISystem/LogicalCollection/UIButtonBase.cs`、`UILogicalUnit.cs`：提交、选择、取消和单帧保护。
- `src/Assembly-CSharp/DayScene/Input/DayScenePlayerInputGenerator.cs`：输入可用性、附近目标、延迟互动。
- `src/Assembly-CSharp/Common/CharacterUtility/CharacterControllerInputGeneratorComponent.cs`、`CharacterControllerUnit.cs`：移动与停止、物理推进。
- `src/Assembly-CSharp/DayScene/Interactables/InteractableArea.cs`：互动范围；其中 mimo 还原仅作参考，并与运行时范围变化交叉核对。
- `src/Assembly-CSharp/DayScene/Interactables/Collections/BehaviourComponents/NitoriTelephoneComponent.cs`：电话菜单与角色联系。
- `src/Assembly-CSharp/DayScene/UI/DaySceneChatSelectionPannel.cs`：闲聊、按钮配置、选择滚动、邀请概率与对话结束后的登记和扣点。
- `src/Assembly-CSharp/DayScene/Interactables/Collections/BehaviourComponents/HakureiMoneyBoxBehaviourComponent.cs`、`CharacterBehaviourComponent.cs`：赛钱菜单与结算、角色互动入口。
- `src/Assembly-CSharp/DayScene/Interactables/Collections/ConditionExtensions/StatusTrackerConditionExtension.cs`：赛钱箱使用次数限制。
- `src/Assembly-CSharp/GameData/RunTime/Common/RunTimeScheduler.cs`：赛钱安排的灵梦正面符卡；同目录 `StatusTracker.cs`：邀请尝试记录、受邀名单与委托登记和领取。
- `src/Assembly-CSharp/DayScene/UI/DaySceneSustainedPannel.cs`、`FastTravelPanel_New.cs`、`DaySceneFastTravelSubPannel.cs`：快捷传送入口、目的地确认与货币扣除；部分还原仅作参考，已核验实际面板和余额。
- `src/Assembly-CSharp/Common/DialogUtility/DialogPannel.cs`：逐句确认。
- `src/Assembly-CSharp/DayScene/Interactables/Collections/BehaviourComponents/MapTransitionBehaviourComponent.cs`：出口触发；与实测地图切换交叉核对。
- `src/Assembly-CSharp/DayScene/SceneManager.cs`、`DayScene/UI/UIManager.cs`：跨图完成时扣点、时钟换算。
- `src/Assembly-CSharp/GameData/RunTime/DaySceneUtility/RunTimeDayScene.cs`、`Collection/TrackedCollectable.cs`：扣点、全局冷却、采集产物与开放时段。
- `src/Assembly-CSharp/GameData/Core/Collections/DaySceneUtility/Collections/Collectable.cs`、`Product.cs`：冷却配置、产物名称；`GameData/RunTime/Common/RunTimeStorage.cs`：库存读取。
- `src/Assembly-CSharp/PrepNightScene/UI/IzakayaConfigPannel.cs`：菜单上下架回调、厨具要求、完成条件、正式开店；`GameData/RunTime/NightSceneUtility/IzakayaConfigure.cs`：配置增删、拥有检查与锁定配方限制。
- `src/Assembly-CSharp/GameData/Core/Collections/Cooker.cs`：厨具类型、系列及额外烹饪类型。具体资源名称和库存同时读取运行时数据。

部分逆向回调仍有未完成标记，因此只复用已存在的游戏入口，没有据此重写其逻辑。实际接口可用性以本轮载荷的编译和执行结果为准。

## 后续改进顺序

1. 优先补证据：每个动作保存小份前后快照、环境版本和结果；输出以差值为主，不再重复输出整棵 UI。保留身份与歧义信息，不能为了压缩丢掉判断依据。
2. 再改善操作：增加当前场景/面板检查，将“动作发出”与“动作完成”分开；只对观察做有限重试，消费类动作不自动重放。统一移动互斥，当前两套移动工具的 `Busy` 互不相通。
3. 增加可重复场景：邀请完成回调、同名商人、日终委托、准备与营业切换。控制存档、地图、资源包版本，再比较修改前后结果。
4. 最后扩展覆盖：完整夜间服务、赛钱效果、受邀客人到店、存档重载、主客机同步和断线重连；根据需要再做路径规划。

本次整理仅离线检查文档、链接与脚本说明，不重新运行游戏载荷。已有脚本的运行成功只对应上述会话条件；这些改进尚未实现。
