# 游玩路径日志 2026-09-08

环境：Touhou Mystia Izakaya RELEASE 4.4.0e，MetaMystia v0.27.0，本地单机，继续 7 月 11 日存档。
本轮起点：主菜单 → 继续 → Day 夜雀小屋，AP=16，10:00。

## 路线与证据

| 时间 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 02:29 | 启动 steamclient_loader.exe | 游戏进程 23364，token 自 LogOutput.log 获取 | LogOutput.log 2:28:41 |
| 02:30 | Splash → Load → Main | 主菜单“继续”已选中 | ui-snapshot |
| 02:31 | 提交“继续” | Day/夜雀小屋，AP=16，10:00 | ui-snapshot |
| 02:33 | Home 出口 → BeastForest | 切图免费，出生 (-9.99,-0.24) | day-world/day-observe |
| 02:36 | Plant_A 采集 | AP 16→15，10:30，冷却 3；露水基准确认 1152 | day-world、AITestGather |
| 02:38 | 绕平台北侧→东侧道路 | (-4.74,-10.53) → (6.92,-3.31) | AITestNav 多段 |
| 02:40 | Plant_C 采集 | AP 15→14，冷却 3；蜂蜜 1272、蝉蜕 1175（无此前基线） | AITestGather |
| 02:42 | 与“游手好闲的妖兽”对话 | “远远看到你挂着的红灯笼…”→米斯蒂娅“感谢惠顾~！”；时钟 11:00，AP 仍 14（对话不耗点） | ui-snapshot、dialog-next |
| 02:45 | Plant_A2 采集 | AP 14→13，11:30；露水 1152→1157（+3，主产物无副产物） | AITestGather 前后差值 |
| 02:48 | Plant_C4 采集 | AP 13→12，12:00；蜂蜜 +2、蝉蜕 +2 | AITestGather 前后差值 |
| 02:51 | 南行受阻回退，经东干道 (29,-8.8)→(34,-23) | 受阻点 (29.30,-15.07) 不可南下，回绕成功 | AITestNav |
| 02:53 | BeastForest_Bamboo（雀酒点）采集 | 雀酒 170→185（+5）；AP 12→11，12:30，冷却 47 | AITestGather 前后差值 |
| 02:55 | 竹林出口 A 切图 → BambooForest | AP 11→10，13:00，扣 1 点；出生 (39.88,24.07) | day-world |
| 02:57 | BamBooForest_Bamboo 采集 | 竹子 274→277（+3）、竹笋 573→574（+1）；AP 10→9，冷却 23 | AITestGather 前后差值 |
| 03:02 | 竹林高台探索受阻 | 因幡帝 (44.37,6.98)、八云蓝 (36.23,10.78) 从出生区无法走近；台地疑似仅连北出口与竹子点 | AITestNav 多次 blocked |
| 03:05 | 出口 A 返 BeastForest | AP 9→8，14:00；出生 (36.96,-23.88) | day-world |
| 03:08 | 西行探路受阻（(13.48,-10.37) 向南不可达） | 妖怪兽道西侧到 BambooForest_Trigger_B 道路不通 | AITestNav blocked |
| 03:11 | 经 (29.9,-8.8)→(29.9,11) 神社主入口 | AP 8→7，14:30；出生 (6.73,-41.61) | day-world |
| 03:14 | 与“虔诚的老婆婆”闲谈 | 单句植物人笑谈，不扣点 | ui-snapshot |
| 03:17 | 天子“关于食材”对话 | 豆腐 1252→1253、白果 835→836（ID5/ID22 +1）；AP 7→6，15:00 | AITestGather、ui-snapshot |
| 03:21 | 赛钱箱捐 1000 円 | fund 2259173→2258173，累计捐赠 87600→88600，buff 队列 0→2，箱 used 0→1；AP 不变 | shrine-state |
| 03:24 | “卖剩货的妖精女仆”打折店 | Merchant_Maid 倍率 0.509：猩红恶魔×7、极上金枪鱼×5、野猪肉×5、和牛×1、松露×4；总价 303¥，fund 2258173→2257870；库存核对 +7/+5/+5/+1/+4 | 商品清单 + ui-snapshot + 字典核验 |
| 03:31 | 与见习天狗记者闲谈 | “赛钱箱异变”两句花絮，不扣点 | ui-snapshot |
| 03:35 | 邀请博丽灵梦 | 对话流程：免单提议→顾虑名声→同意今晚消费；AP 6→5，15:30；attempted/invited=True (Reimu id=7) | shrine-state + 邀请名单查询 |
| 03:40 | 神社 Tree_A/西区探路受阻 | 桃子点 (18.65,-15.04) 从通道不可达；中庭与广场部分被挡墙隔开 | AITestNav blocked |
| 03:45 | 神社南出口 → BeastForest | AP 5→4，16:00；出生 (29.39,6.54) | day-world |
| 03:50 | 妖怪兽道西区新目标 | 喜欢料理的女妖怪（单句）、杂货商人、橙（未接近）、胆小的妖兽小孩；另见 DLC4 钓鱼区/水獭祭点位 | day-observe |
| 03:52 | “杂货商人”购物 | Merchant_BeastForest 倍率 0.6，15 种食材/酒水共 649¥；fund 2257870→2257221；清单见下 | ui-snapshot + 商品清单 |

### 杂货商人本次清单（Merchant_BeastForest，倍率 0.6）

鸡蛋×10、猪肉×10、牛肉×10、豆腐×13、土豆×9、洋葱×8、南瓜×10、萝卜×8、海苔×18、辣椒×3、竹笋×2、三文鱼×4；酒水：果味 High Ball×13、淇×11、果味 SOUR×10。共 15 项 649¥。

### 商店操作记录

- 商店“我全都要”按钮不是 UIButtonBase，未直接点击成功；调用面板 `OnBuyAll(CallbackContext)`（游戏“全选/购买全部”同一入口）成功把全部商品放入购物车并自动选中“确认购买”，再提交按钮完成购买。
- 余额与库存仅扣/加一次：-303¥、5 种商品各加对应数量；结算面板文字双份不代表双倍结算。
- 该店为随机打折商人（本次 0.509 倍），库存 205（总件数）。高级食材（和牛、松露、极上金枪鱼、野猪肉、猩红恶魔 Lv2）。

### 关键规则观察

- “关于食材/关于酒水/邀请”耗时 30 分钟；食材对话结束在结算面板发 2 种食材各 +1，UI 双份显示不代表双倍入库（库存仅 +1）。
- 神社新一天：赛钱箱 used=0、天子 attempted/invited=False、夜间 buff 队列 0，说明存档在当天开始时点（此后操作会再次产生当天状态）。

补充观察：切图后 `day-observe` 仍能枚举到旧地图对象（如妖兽老人、芙兰朵露），与 README 已知现象一致，操作前须核对实际归属与坐标。

## 已记录产物 ID 映射（本次用到）

ID 17 蘑菇、24 蜂蜜、25 蝉蜕、27 露水。库存表以 AITestGather 输出为准。

## 工具改动

- `gather-info-init.csx` 增加 INGREDIENTS 全量表行，用于差值核验；仍用 foreach 避免 IL2CPP 集合 LINQ 问题。

## 夜间经营后续（时刻取 BepInEx LogOutput.log）

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 03:01:25 | 白天结束进入选店 | DaySceneManagerPatch OnDayOver；次日同存档 | LogOutput |
| 03:05:38 | 妖怪兽道 Lv1 开店配置载入 | WorkPrep；自动预设 8 菜 8 酒，厨具 19/17/16；Guest flow rate 0.1x | IzakayaConfigurePatch 日志 |
| 03:09:14 | 长按“前往开店” | LoadScene → WorkScene，兽道推车 | UI/日志 |
| 03:09:16 | 赛钱箱效果：ReimuProtection | Spawn id=7 施放正面效果后离店，不计收入；另一条“永续热火朝天”作弊 buff 自动生效 | TraceLog/RunTimeSchedulerPatch |
| 03:09:22-58 | 普客一桌 | 首单 炙猪肉饭团 → 备餐 → 评价 → 付款离开 | TraceLog/StoreFood |
| 03:11:22 | 受邀灵梦到店 | SpawnSpecialGuestGroup 入桌 2；订单：高级料理 + 可加热饮品，ServFood/ServBev 均为空 | TraceLog + 桌位探针 |
| 03:11:41 | 灵梦首轮下单 | mood 73，remain 4/orders 1；全程未出现 StoreFood | 探针/LogOutput |
| 03:13:22 | 打烊开始 | TryCloseIzakaya；关店后仍有普客完成付款；灵梦因无匹配菜单菜品滞留桌 2 | TraceLog + 桌位探针 |
| 03:14+ | 解除卡店 | 移除当前 Fever buff 并关闭 CheatFever 配置，等待耐心耗尽/结算 | 调试载荷 |
| 03:18:02 | 灵梦耐心耗尽离店 | PatientDepletedLeave → GuestPay → LeaveFromDesk；桌位清空 | TraceLog |
| 结算 | Result 面板 | 来访 3（普通 2、特殊 1）、最大气氛 87%、最大连击 4、经验 +90、经营 591¥（小费 447¥）、净利 -1361¥ | ui-snapshot |

### 本轮夜间问题记录

- 预设菜单无“高级”料理与“可加热”饮品，无法满足受邀灵梦的特殊订单；灵梦 03:11:22 到店并下单，但直至打烊未被服务。
- “永续热火朝天”作弊（CheatFever=true，耐心不减）会使未满足的客人永不离开，打烊流程卡在 `OnWaitForAllGuestToLeave`。本次通过游戏 Buff 接口移除 Fever 并置 false 后恢复。
- 已记录灵梦邀请夜到店路径：白天邀请登记（id=7）→ Work 03:11:22 SpawnSpecialGuestGroup → 桌 2 下单。是否被服务到需在配置匹配菜单的下一晚继续验证。

## 7 月 12 日复核：配置灵梦可满足菜单后再营业

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 03:19:35 | ResultScene → Day 新一天 | 7/12 10:00，Home；未写盘，7/11 旧档未覆盖 | LogOutput/LoadScene |
| 03:20:54 | 步行至博丽神社 | HakureiShrine；随后对话邀请 | LogOutput SwapMap |
| 03:23:45 | 邀请博丽灵梦 | `RecordInvitedGuest id=7`；fund 2257712，AP 16→15，10:30 | StatusTrackerPatch 日志 |
| 03:24:51 | 快进白天 | OnDayOver 收尾后进入选店；18:00 | DaySceneManagerPatch |
| 03:24:59 | 选店面板 | 默认聚焦 BeastForest | IzakayaSelectorPanelPatch |
| 03:27:57 | 提交 Lv1 | 兽道推车，3 工作桌/3 客人桌；历史伙伴厨师/传菜/酒水已占满 | ui-level + ui-snapshot |
| 03:27:58 | 长按前往开店 | LoadScene → IzakayaPrepScene | MpManager 日志 |
| 03:28:01 | WorkPrep 载入 | 自动预设 8 菜 8 酒；厨具 19/17/16；Guest flow rate 0.1x | IzakayaConfigurePatch 日志 |
| 03:31:09 | 替换菜单 | 料理：大江户船祭/分子蛋/无意识妖怪慕斯/山泉双色果盘/饭团/能量串/热松饼/班尼迪克蛋；酒水：玉露茶/冬酿/红魔馆红茶/果味High Ball/果味SOUR/雀酒/十四夜/阿芙加朵 | prep-tags 候选核验 + prep-state 前后差值 |
| 03:31:46 | 长按前往开店(GotoWork) | LoadScene → WorkScene，兽道推车/雀酒屋绝赞营业中；键山雏(厨)、咲夜(传菜)、宫古芳香(酒水)在岗 | ui-hold + ui-snapshot |
| 03:31:52 | 首位普客入店 | SpawnNormalGuestGroup → 入座 | TraceLog |

### 菜单覆盖核验

灵梦 `LikeFoodTag`（权重展开去重）：实惠(-2)、饱腹(9)、甜(17)、不可思议(27)、高级(4)、流行·喜爱(-20，点单过滤器排除)；`LikeBevTag`：无酒精(-1)、低酒精(0)、可加热(4)。

- 高级：大江户船祭 2008、分子蛋 5012、无意识妖怪慕斯 70
- 甜：分子蛋、慕斯、山泉双色果盘 11000、热松饼 65
- 不可思议：分子蛋 5012
- 实惠/饱腹：饭团 35、能量串 5、班尼迪克蛋 56 等
- 无酒精+可加热：玉露茶 22、红魔馆红茶 13；低酒精+可加热：冬酿 19

本次直接调用 `IzakayaConfigure.Logoff/RegisterToDaily*` 与 `SolveDailyCompletion()`（与准备面板同一组配置方法），跳过菜单拖拽 UI；进入营业前已用 prep-state 复核配置，经营中是否能被服务到待后续观察记录。

### 工具改动

- 新增 `ui-level.cs`：提交 `Lv1` UIButtonToggle。
- 新增 `ui-hold.cs`：长按目标 UIButtonHold（Target 常量按需改）。
- 新增 `prep-tags.cs`：输出库存料理/饮品的标签名与数量，供菜单设计。
- 新增 `guest-pref.cs`：输出指定特殊客人的喜恶标签名。
- 新增 `prep-menu-reimu.cs`：本次场景专用菜单配置，替换全部菜单为覆盖灵梦标签的 8 菜 8 酒，不改厨具。

## 7 月 12 日晚实测：菜单覆盖后仍未被服务

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 03:31:49 | WorkScene 开始 | 兽道推车，键山雏/咲夜/宫古芳香在岗 | LogOutput/MpManager |
| 03:31:52-32:27 | 普客一桌 | 首单热松饼→StoreFood 03:32:11→评价→付款离店；伙伴自动完成 | TraceLog + StoreFood 日志 |
| 03:33:52 | 灵梦到店 | SpawnSpecialGuestGroup 入桌 2 | TraceLog |
| 03:34:09 | 灵梦首轮下单 | 订单：高级料理 + 无酒精饮品；此后 70 秒无 StoreFood | FirstOrder 日志 + work-desk 探针 |
| 03:35:52 | 打烊开始 | TryCloseIzakaya | TraceLog |
| 03:36:15 | 灵梦耐心耗尽 | PatientDepletedLeave → GuestPay → LeaveFromDesk，订单未完成 | TraceLog |
| 03:36:20 | ResultScene | 来访 2（普通 1、特殊 1）、经营 220¥、小费 186¥、净利 120¥、经验 18 | ui-snapshot |

### 结论与源码对照

- 菜单覆盖已核验（高级、甜、不可思议、实惠、饱腹各至少一道；无/低酒精+可加热饮品齐备），但灵梦 70 秒未被服务：**普通伙伴不处理特殊订单**，覆盖菜单不足以自动上菜。
- 源码依据：`PartnerCookBehaviour.FocusingOrder` 仅以 `NormalOrder` 为目标；`PartnerManager` 中只有 `PartnerBossBehaviour` 处理 `SpecialOrder`。本轮厨师为键山雏（普通伙伴），故灵梦订单不会被自动备餐。
- 特殊客人点单后游戏会登记 `ShowOrder` 打开玩家上菜面板（`WorkSceneServePannel`），需要玩家侧从餐台/托盘选择匹配料理与饮品；下一步验证玩家侧上菜路径。

## 7 月 13 日：结算后返回白天（未写盘）

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 03:39:28 | ResultScene → 存档面板 → 放弃保存 | 回到 Day：7/13 10:00 夜雀小屋；磁盘槽位仍是 7/11 档，未覆盖 | result-continue.cs + result-save-close.cs + ui-snapshot |

## 7 月 13 日重跑：受邀灵梦晚“实惠+无酒精”未及备餐

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 03:45 前 | 快捷传送至神社 | 7/13 白天，AP16 | day-fast-shrine.cs |
| 03:49 | 邀请博丽灵梦 | id=7，AP16→15，10:30 | LogOutput |
| 18:00 | 选兽道 Lv1 → WorkPrep 替换 8 菜 8 酒 | prep-menu-reimu.cs | LogOutput |
| 03:54 | 灵梦入 0 号桌下单“实惠料理+无酒精饮品” | 尚未备餐，随后自然打烊 | work-desk 探针 |
| 结算 | ResultScene | 普通 1、特殊 1、净利 134¥、小费 180¥、经营 234¥、经验 48 | ui-snapshot |

## 7 月 14 日：玩家侧备餐路径首次走通，但上菜未判定成功

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 结算后 | 放弃保存 → 7/14 10:00 夜雀小屋 | 内存日 7/14 周日；磁盘仍 7/11 | result-save-close.cs |
| 04:01 | 快捷传送神社 + 邀请灵梦 | id=7，AP16→15，fund 2257966 | LogOutput 04:01:52 |
| 04:02 | 快进 18:00 → 兽道 Lv1 → WorkPrep 替换菜单 → 进入 Work | 8 菜 8 酒覆盖灵梦标签 | prep-menu-reimu.cs |
| 04:05:41 | 灵梦入 1 号桌 | SpawnSpecialGuestGroup | TraceLog |
| 04:05:58 | 首轮下单 | 订单：高级料理+无酒精饮品 | FirstOrder 日志 + work-desk 探针 |
| 04:06-04:07 | 玩家侧备餐 | 板台 recipe 5012（分子蛋）直接扣料+SetCook+StartCookCountDown；取餐成功（DelegateSupport 转换 Action<Sellable>）；玉露茶（id=22）入托盘 | work-cook-start.cs、work-cook-extract.cs、work-bev-out.cs、LogOutput |
| 04:07:39 | ExcuteEventAtCorodinate(1) 打开上菜面板 | ServePannel 显示灵梦“无酒精饮料/高级料理”订单，托盘含分子蛋与玉露茶视觉 | ui-snapshot |
| 04:07:49 | Send(分子蛋) + Send(玉露茶) + CloseExternPanel | 仅 TraceLog Send×2、OnPanelClose；无 EvaluateOrder/StoreFood；04:07:51 自然打烊，04:08:14 灵梦耐心耗尽离店 | LogOutput 04:07:49-04:08:14 |
| 04:08:19 | 结算 ResultScene | 特殊 1、普通 2、净利 98¥、小费 167¥、经营 198¥、经验 18 | ui-snapshot |

### 本轮记录要点

- 玩家侧烹饪链路已验证可行：直接 Recipe 扣料 + `SetCook` + `StartCookCountDown(-1f)` 可出成品，`CookController.Extract` 需 `DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Sellable>>`；扣料用 `RunTimeStorage.IngredientOut`，未走 `StatusTracker.AddBussinessIngredientConsumes`（跳过统计，源码推断）。
- 上菜面板 `Send/CloseExternPanel` 调用后订单未进入 EvaluateOrder，服务未成功。可能原因是提交时间贴近打烊（剩余约 2 分钟）或调用时序与玩家点击流程不一致，待源码审计与下一晚验证。

## 7 月 15 日：提前备餐 + 订单“高级+低酒精”，仍未被判定服务成功

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 04:2x | ResultScene → 放弃保存 → 7/15 10:00 夜雀小屋 | 磁盘仍 7/11 档 | result-save-close.cs |
| 04:2x | 快捷传送神社 → 走近灵梦（西南楼梯→西平台→主殿前） | 新路线：南侧公共区 x2.6 有楼梯，y<8 区域与东侧高地被 x≈8.2 纵向围栏隔开 | AITestNav 分段 |
| 04:2x | 对话选“邀请” | id=7 登记，AP15，10:30，fund 2258064 | guest-status-reimu.cs |
| 04:2x | 神社内直接 `OnFastForwardSubmit` → 18:00 | 快进按钮在神社场景不可交互，改用面板提交方法 | day-warp-night.cs |
| 04:2x | 兽道 Lv1 → 替换菜单 → 进入 Work | prep-menu-reimu.cs 复核通过 | prep-state.cs |
| 04:23 前后 | 首位普客（妖怪狸）自动服务 | 伙伴完成 | work-desk 探针 |
| 04:25:34 | 灵梦入 1 号桌 | SpawnSpecialGuestGroup | LogOutput |
| 04:25:49 | 首轮下单 | 订单：高级料理 + 低酒精饮品；countdown≈79 | work-desk 探针 + LogOutput |
| 04:26:0x | 玩家侧备餐 | 分子蛋 recipe 5012 扣料+SetCook+StartCookCountDown(-1f) 完成；冬酿 id=19（低酒精）入托盘 | work-cook-start.cs、work-cookers.cs、work-cook-extract.cs、work-bev-out19.cs |
| 04:27:25 | 打开 1 号桌上菜面板 | ShowOrder/OnPanelOpen；此前 work-open-serve 按 id 查找两次返回“Reimu not at any desk”，同帧 work-desk 却可查到（定位差异待查） | LogOutput |
| 04:27:35 | Send(分子蛋) + Send(冬酿) + CloseExternPanel | 仍只记录 Send×2/OnPanelClose，无评估链；04:27:44 打烊 | LogOutput |
| 04:28:05 | 灵梦耐心耗尽离店 | PatientDepletedLeave → GuestPay → LeaveFromDesk | LogOutput |
| 04:28:11 | ResultScene | 特殊 1、普通 1、净利 108¥、小费 167¥、经营 208¥、经验 18 | ui-snapshot |

### 本轮新线索

- 排除了“提交太晚”单独成因：这次打烊前约 9 秒已提交并关闭面板，仍无 EvaluateOrder/RemoveFromPatientCountdown，随后耐心耗尽。
- 全程日志无 Fever/ThrowDeliver 启用记录，`isThrowDeliverMode` 理论应为 false；但仍需在面板打开时实测该标记与 Send 后订单字段是否写入。
- 新增 `work-serve-debug.cs`：逐段打印托盘与订单 ServFood/ServBeverage，用于下一晚定位 Send 是否真正赋值。

## 7 月 16 日：定位到“CloseExternPanel 实际是取消路径”

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 04:2x | Result → 7/16 → 神社邀请灵梦 | AP15，10:30 | guest-status-reimu.cs |
| 04:3x | 兽道 Lv1 营业 | 2 号桌灵梦订单：实惠料理 + 可加热饮品（countdown≈94） | work-desk 探针 |
| 04:3x | 饭团 recipe 35 + 冬酿 id=19 备餐 | 玩家侧直接烹饪取餐成功 | work-cook-start35.cs、work-cook-extract.cs、work-bev-out19.cs |
| 04:39:36 | 打开 2 号桌上菜面板 | ShowOrder/OnPanelOpen | LogOutput |
| 04:39:37 | 带探针逐步 Send → CloseExternPanel | **关键证据**：Send 后托盘空（willServe 已取走）；CloseExternPanel 后饭团被退回托盘、饮品未退回 → 关闭走的是 OnExitExtern 回收路径而非提交判定 | work-serve-debug2.cs 输出 |
| 04:40 前后 | 打烊边界尝试直接 OnPanelClose | 面板已无法再开，未成 | LogOutput |
| 04:41:23 | ResultScene | 服务仍未成功（未留存结算快照，直接进入下一日） | LogOutput |

### 本轮结论

- 之前几晚失败的根因不是备餐或时间，而是 `CloseExternPanel` = 取消/回收待上菜物品；`Send` 本身会正确写入待上菜字段。
- 正确提交应走面板的 `OnPanelClose` 生命周期（判定 `IsFullfilled` → `EvaluateOrder`），不能走外部取消关闭。

## 7 月 17 日：首次玩家侧服务灵梦成功

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 04:4x | Result → 7/17 → 神社邀请灵梦 | AP15，10:30，fund 2258324 | guest-status-reimu.cs |
| 04:4x | 兽道 Lv1 营业 | 2 号桌灵梦订单：不可思议料理 + 无酒精饮品（countdown≈84） | work-desk 探针 |
| 04:48:44 | 首轮下单 | FirstOrder/AddToPatientCountdown | LogOutput |
| 04:4x | 分子蛋 recipe 5012 + 玉露茶 id=22 备餐 | 玩家侧备餐 | work-cook-start.cs、work-cook-extract.cs、work-bev-out.cs |
| 04:49:45 | 打开 2 号桌上菜面板 | ShowOrder/OnPanelOpen | LogOutput |
| 04:49:55 | Send(分子蛋) + Send(玉露茶) + **直接 OnPanelClose** | OnPanelClose → EvaluateOrder → RemoveFromPatientCountdown → Evaluate → TryOverrideEvaluateByBuff；灵梦评价“这一顿，就算花光所有钱也是值得的！”后离店 | LogOutput + ui-snapshot |
| 04:51:42 | ResultScene | 特殊 1、普通 2、来访 3、最大气氛 51%、最大连击 2、经验 39、经营 369¥、小费 192¥、净利 269¥ | ui-snapshot |

### 验证说明

- 玩家侧特殊订单服务链路已首次完整走通：`ExcuteEventAtCorodinate` 打开面板 → 托盘选菜（Send 模拟托盘按钮提交）→ `OnPanelClose` 判定 → EvaluateOrder → 评价付款。
- 直接 `OnPanelClose` 不会把面板从 UI 栈移除，面板视觉残留到切场景时才销毁（04:51:41 OnPanelDestroyed）；后续可尝试先重开托盘再 `ClosePanel` 以获得更干净的关闭流程。
- `work-prep-auto.cs` 的桌位查找在同一帧探测中不稳定（与 work-open-serve 现象一致），暂以 work-desk 探针 + 固定桌号载荷完成；待后续查 GuestsManager 列表口径。

## 7 月 18 日：Send + ClosePanel 干净关闭并二次成功服务

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 04:5x | Result → 7/18 → 神社邀请灵梦 | AP15，10:30，fund 2258593 | guest-status-reimu.cs |
| 04:58:46 | 兽道 Lv1，1 号桌首轮下单 | 订单：实惠料理 + 低酒精饮品 | LogOutput + work-desk 探针 |
| 04:58:5x | 饭团 recipe 35 + 冬酿 id=19 备餐 | 玩家侧备餐 | work-cook-start35.cs、work-cook-extract.cs、work-bev-out19.cs |
| 04:59:12 | 打开 1 号桌上菜面板 | ShowOrder/OnPanelOpen | LogOutput |
| 04:59:22 | Send(饭团) + Send(冬酿) + **ClosePanel** | OnPanelClose → EvaluateOrder → RemoveFromPatientCountdown → Evaluate；**面板从 UI 栈正常移除（无残留）** | LogOutput + ui-snapshot |
| 04:59:36 | 灵梦高评价触发第二轮订单 | MainOrderCycle → 不可思议料理 + 低酒精饮品（撞上打烊，未再服务） | LogOutput + work-desk 探针 |
| 05:00:36 | 打烊 TryCloseIzakaya | 第二轮订单等待耐心耗尽 | LogOutput |
| 05:02:51 | ResultScene | 特殊 1、普通 1、来访 2、最大气氛 52%、最大连击 2、经验 66、经营 342¥、小费 258¥、净利 242¥ | ui-snapshot |

### 结论

- 干净提交路径确定为：`Send(food)` → `Send(bev)` → `ClosePanel()`。此时托盘仍开着，面板生命周期能正常关闭托盘、判定订单并移出 UI 栈，不再残留到切场景。
- `CloseExternPanel` 是取消/回收路径，切勿用于提交；此前多晚失败全部源于此。
- 特殊客人好评后会进入下一轮订单；若已到打烊边界，第二轮只能按耐心耗尽处理，不影响首轮服务成功判定。

## 7 月 19 日（结果：失败）：红魔馆邀请红美铃，兽道 Lv1 首次尝试

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 05:08:3x | 快捷传送 → 红魔馆 ScarletMansion | 落点约 (12.93,-18.86)，白天剩余 AP 16 | LogOutput |
| 05:09~10 | 走近红美铃并完成“邀请” | 05:10:17 `RecordInvitedGuest` id=15 红美铃；登记 invitedCount=1 ids=15；AP15、10:30、fund 2258735 | guest-status-all.cs + LogOutput |
| 05:10:56 | 提交 `OnFastForwardSubmit` | `OnDayOver_Prefix` → 18:00 | LogOutput |
| 05:11 | 对话“继续” | 18:00 选店面板 | ui-snapshot |
| 05:12:00 | 选店面板 | 默认选中妖怪兽道 BeastForest | IzakayaSelectorPanelPatch 日志 |
| 05:12:0x | 提交 Lv1 + 长按前往开店 | DayScene → LoadScene → IzakayaPrepScene | ui-level.cs、ui-hold-goto.cs |
| 05:13:54 | 替换菜单 | 料理：饭团/炙猪肉饭团/毛玉熔岩豆腐/炒肉丝/炸猪肉排/蜜汁叉烧/一击☆必杀/华光玉煎包；酒水：玉露茶/冬酿/红魔馆红茶/咖啡/十四夜/教父/阿芙加朵/冰山毛玉冻柠；厨具 19/17/16 未动 | prep-menu-meirin.cs + prep-state.cs + LogOutput |
| 05:14:06 | 长按 GotoWork | IzakayaPrepScene → LoadScene → WorkScene | ui-hold.cs |
| 05:14:12 | 兽道 Lv1 营业 | 推车场景；正常客群进店 | LogOutput + ui-snapshot |
| 05:14:26 | 首位普通客人下单 | FirstOrder/GenerateOrderSession | LogOutput |
| 05:16:12 | 红美铃到 0 号桌 | SpawnSpecialGuestGroup → TrySendToSeat | LogOutput |
| 05:16:33 | 红美铃首轮下单 | 订单：**力量涌现料理 + 可加热饮品**；TotalCountDown 由 120 持续下降 | work-desk + LogOutput |
| 05:17:0x | 玩家侧备餐 | 一击☆必杀(2016)：野猪肉/鹿肉/洋葱扣料 + Grill(3,3) SetCook/StartCookCountDown(-1f) 完成并取盘；冬酿(19)入托盘 | work-cook-start2016.cs、work-cook-extract2016.cs、work-bev-out19.cs |
| 05:18:12 | 打烊 TryCloseIzakaya | TotalCountDown=0；红美铃仍坐等，未立即离开 | LogOutput |
| 05:18:39 | 红美铃耐心耗尽离店 | PatientDepletedLeave → GuestPay → LeaveFromDesk；此时才执行 work-open-serve15，返回“Meirin not at any desk” | LogOutput |
| 05:18:46 | ResultScene | 来访 2（特殊 1/普通 1）、净利 -30¥、小费 131¥、经营 170¥、最大气氛 35%、最大连击 1、经验 18 | ui-snapshot |

### 7/19 失败分析

- 备餐链路本身成功：一击☆必杀按时出锅上盘，冬酿同步入盘，托盘内 Food 2016 + Beverage 19 备齐；失败不在菜单或烹饪。
- 失败原因是服务窗口被操作耗时耗尽：红美铃 05:16:33 下单，05:18:12 打烊、05:18:39 才耐心耗尽。备餐约在 05:17:2x 完成，但之后继续读取/制作通用载荷，未在打烊后的 27 秒窗口内及时打开上菜面板并提交。
- `TotalCountDown=0` 只代表夜场结束（TryCloseIzakaya），不表示特殊客人立即离店；只要耐心未耗尽仍可服务。本次误把 countdown=0 当“已失败”，白白错过窗口。
- 教训：面向 id=15 的打开面板载荷应在红美铃到店前预建；到店后若托盘已备齐，应立即 `ExcuteEventAtCorodinate(桌号)` → 快照确认面板 → `Send+ClosePanel`，不再临时造载荷。

## 7 月 20 日（复测成功）：红美铃服务链首次走通

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 05:22:02 | 快捷传送 → 红魔馆 ScarletMansion | 八云传送 -200¥，10:00 落点；AP16 | fast-teleport.cs + LogOutput |
| 05:25:14 | 走近红美铃完成邀请 | `RecordInvitedGuest` id=15；AP15、10:30、fund 2258705 | guest-status-all.cs + LogOutput |
| 05:26:47 | 快进 18:00 → 兽道 Lv1 选店 | DayScene → LoadScene → IzakayaPrepScene | day-warp-night.cs |
| 05:27:0x | 应用红美铃菜单（同 7/19 菜单） | prep-state 复核 8 菜 8 酒与厨具 19/17/16 | prep-menu-meirin.cs + prep-state.cs |
| 05:27:17 | WorkScene 营业开始 | 兽道 Lv1 推车 | ui-snapshot + LogOutput |
| 05:29:20 | 红美铃到 1 号桌 | SpawnSpecialGuestGroup；TotalCountDown≈108 | work-desk + LogOutput |
| 05:29:34 | 首轮下单 | 力量涌现料理 + **提神**饮品 | work-desk |
| 05:29:5x | 通用助手开烤/取酒 | 一击☆必杀(2016) Grill + 咖啡(1001)入盘 | work-cook-helper.csx（Start/Extract/Bev） |
| 05:30:56 | 固定桌号打开 1 号桌面板 | ShowOrder/OnPanelOpen；**id 查找载荷连续两次失败，固定桌号成功** | work-open-desk1.cs + LogOutput |
| 05:31:08 | Send(2016)+Send(1001)+ClosePanel | OnPanelClose → EvaluateOrder → RemoveFromPatientCountdown → Evaluate → TryOverrideEvaluateByBuff | LogOutput |
| 05:31:22 | 红美铃付款离店 | MainOrderCycle → GuestPay → LeaveFromDesk | LogOutput |
| 05:31:38 | ResultScene | 触发奖励 1、特殊 1、普通 1、来访 2、最大气氛 81%、最大连击 3、经验 60、净利 165¥、小费 195¥、经营 365¥ | ui-snapshot |

### 7/20 结论

- 红美铃的服务链路与灵梦同款验证成立：到店后先读订单，用已加载的通用备餐助手在约 20 秒内完成烧烤与取酒，再用固定桌号打开面板并 `Send+ClosePanel` 提交，全程在打烊/耐心窗口内完成。
- `work-open-serve15.cs` 按 `SpecialGuest.Id` 遍历桌位不可靠（连试两次均找不到，而 work-desk 同帧可查到红美铃在 1 号桌）；固定桌号 `ExcuteEventAtCorodinate(1)` 可靠。该现象与 7/17~18 对灵梦的观察一致，根因仍待查。
- 本轮饮品订单为“提神”，选用咖啡(1001)（无酒精/现代/可加热/提神，TrueValue 62）替代冬酿，服务照常成功；说明只需命中订单标签即可，不必与上一轮组合一致。
- 新增 `work-cook-helper.csx`（/script 会话级）：`AITestCook.Start(recipeId)`/`Extract()`/`Bev(id)`/`Tray()`，可按 CookerType 自动找空闲厨具，显著缩短备餐载荷编写与请求轮数。
- 结算后已覆盖写入存档 #3（Mystia#3.memory，05:32:30）；当前为 7/21 10:00 夜雀小屋，AP16。

## 7 月 21 日（进行中）：红魔馆本地营业探索

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 05:32:33 | 7/21 10:00 夜雀小屋 | 存档 #3 载入 | ui-snapshot |
| 05:3x | 快传红魔馆，走向东侧两只“小恶魔” | (24.16,-21.51) 为**进货商店**（DaySceneShopPannel）；(25.70,-21.46) 为对话 NPC，仅“相关任务/闲聊”，无邀请选项 | day-npc-map.cs + shop-close.cs + ui-snapshot |
| 05:3x | 新增 `day-npc-map.cs`、`shop-close.cs`、`nav-far.csx` | 枚举全部 CharacterCondition；商店面板可用 `ClosePanel()` 直接关闭；AITestNav2 支持长距离 16s 导航 | 本目录脚本 |
| 05:39~40 | 红美铃邀请 | 05:40:28 `RecordInvitedGuest` id=15；AP15、10:30、fund 2258870 | guest-status-all.cs |
| 05:41:22 | 晚间选店 | OnGuideMapSpotSelected BeastForest → 红魔馆；UI 同步为“红魔馆雀食堂” | IzakayaSelectorPanelPatch 日志 + ui-snapshot |
| 05:41:54 | WorkPrep | 红魔馆 Lv1 厨具仍为 19/17/16（料理台/油锅/烧烤架），默认菜单同兽道 | prep-state.cs + IzakayaConfigurePatch 日志 |
| 05:42:24 | 红魔馆推车营业开始 | WorkScene | LogOutput |
| 05:44:27 | 红美铃到 2 号桌 | SpawnSpecialGuestGroup；TotalCountDown≈115 | LogOutput + work-desk |
| 05:45:0x | 首轮下单 | 肉料理 + 古典饮品 | work-desk |
| 05:45:1x | 玩家/伙伴共做华光玉煎包 | 键山雏也在为 1 号桌鸦天狗做同款；助手取盘为华光玉煎包(FoodId 63) | AITestCook 输出 |
| 05:46:27 | 打烊 TryCloseIzakaya | 红美铃仍在等餐 | LogOutput |
| 05:46:46 | 红美铃耐心耗尽离店 | PatientDepletedLeave；玩家未能在窗口内提交 | LogOutput |
| 05:47:26~35 | 固定桌 1 补送鸦天狗 | 煎包+冬酿提交触发 EvaluateOrder；鸦天狗第二轮由伙伴自动续接 | LogOutput |
| 05:48:xx | ResultScene | 触发奖励 0、特殊 1、普通 2、来访 3、最大气氛 88%、最大连击 4、经验 104、净利 417¥、小费 409¥、经营 617¥ | ui-snapshot |

### 探索笔记

- 红魔馆地图同屏有两个“小恶魔”：`Koakuma`（商店商人）与 `_ResourceExample_Koakuma`（对话 NPC）；对话 NPC 当前未解锁“邀请”，聊天选项按羁绊等级解锁（源码 DaySceneChatSelectionPannel 显示邀请需羁绊≥2，任务/食材/酒水分别需≥3/4/5，源码推断）。
- 商店面板不能用通用取消关闭；直接调用 `DaySceneShopPannel.ClosePanel()` 有效。
- 东侧可直线走到两只小恶魔/钓鱼翁区，但向北（芙兰朵露所在的 (9.86,12.70)）和向西（大妖精）均被围栏挡路；AITestNav2 记录受阻点便于换路。
- 红魔馆 Lv1 与兽道 Lv1 使用同一套厨具与默认菜单；差异主要在店面名称/客人池（红魔馆夜场含帕秋莉/幽幽子等）。

### 7/21 失败分析（红魔馆首测）

- 备餐其实只花了约 10 秒，失败点是 agent 请求往返总耗时太长：订单 05:45:0x 出现，固定桌 2 面板直到红美铃离店后 05:46:50 才真正提交成功路径，属于实时性瓶颈而非游戏机制问题。
- 额外发现：打烊后仍坐席的客人可继续服务；本晚 1 号桌鸦天狗在 05:47:26~35 被玩家补送成功并触发 EvaluateOrder，第二轮由伙伴自动完成。
- 下一步策略：不再逐轮人工“看订单→备餐→开盘”，改为在 /script 会话里预先放置自动看护协程（检测红美铃 SpecialOrder 后直接备菜入盘、固定桌开盘、Send+ClosePanel），把关键动作压到 1 秒内。

### 菜单设计说明

- 红美铃偏好（guest-pref15.cs）：料理 力量涌现/饱腹/中华/肉，饮品 提神/可加热/古典。
- 兽道 Lv1 只安装料理台(19)/油锅(17)/烧烤架(16)，故仅选 CookerType 为 CuttingBoard/Fryer/Grill 的菜，全部菜谱与酒水覆盖至少一类偏好标签；力量涌现由“一击☆必杀”承担，中华覆盖炒肉丝/毛玉熔岩豆腐/蜜汁叉烧/华光玉煎包，饱腹与肉大量重叠覆盖。
- 饮品覆盖：古典=玉露茶/冬酿/十四夜/教父；可加热=玉露茶/冬酿/红茶/咖啡/十四夜；提神=红茶/咖啡/阿芙加朵/冻柠。
- 菜单替换直接调用 `IzakayaConfigure.Logoff/RegisterToDaily*` 与 `SolveDailyCompletion()`，未走拖拽 UI；进入营业前已用 prep-state 复核。

## 7 月 22 日：红魔馆次夜（第一单成功、追加轮次漏单）

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 05:49:13 | 7/22 10:00 夜雀小屋 | 从存档 #3 载入，AP16、fund 2259287 | ui-snapshot |
| 05:5x | 快传红魔馆并再次邀请红美铃 | AP15、10:30 | fast-teleport.cs + guest-status-all.cs |
| 05:52:44 | 红魔馆 Lv1 营业开始 | WorkScene，红魔馆推车 | LogOutput |
| 05:53:03~22 | 首组普通客 | 下单→EvaluateOrder→GuestPay 离店 | LogOutput |
| 05:54:47 | 红美铃本轮第一次到店 | SpawnSpecialGuestGroup | LogOutput |
| 05:55:03 | 红美铃下单 | 力量涌现 + 可加热（1 号桌） | LogOutput + work-desk |
| 05:55:52~54 | 伙伴料理入库并完成一组普通单 | StoreFood 炒肉丝 → EvaluateOrder | LogOutput |
| 05:55:5x~56:07 | 玩家手工备餐 | 一击☆必杀(2016) + 玉露茶(22) 入盘 | work-cook-start2016.cs 等 |
| 05:56:07 | 固定桌 1 打开上菜面板 | ExcuteEventAtCorodinate(1) → ShowOrder/OnPanelOpen | LogOutput |
| 05:56:17 | Send(2016)+Send(22)+ClosePanel | EvaluateOrder 完整链路成功 | LogOutput |
| 05:56:33 | 红美铃结账离店 | GuestPay → LeaveFromDesk | LogOutput |
| 05:56:57 | 红美铃本轮第二次到店 | 再次 SpawnSpecialGuestGroup；05:56:58 TryCloseIzakaya（TotalCountDown=0） | LogOutput |
| 05:57:13 | 红美铃下单 | 力量涌现 + 古典（1 号桌），服务字段为空 | work-desk + LogOutput |
| 05:59:19 / 05:59:36 | 两位客人耐心耗尽离店 | PatientDepletedLeave ×2，本单未服务 | LogOutput |
| 05:59:41~43 | ResultScene | 触发奖励 1、触发惩罚 0、接待特殊 2、接待普通 3、来访 5、最大气氛 100%、最大连击 4、经验 104、净利 755¥、小费 569¥、经营 955¥ | ui-snapshot |
| 06:01:06 | 覆盖写入存档 #3 | Mystia#3.memory 更新；进入 7/23 10:00 夜雀小屋，AP16、fund 2260242 | 存档文件 + ui-snapshot |

### 7/22 复盘

- 第一单再次验证固定桌 1 + “一击☆必杀(2016)+玉露茶(22)”可覆盖“力量涌现+可加热”，服务链一次成功。
- 第二轮到店发生在 TryCloseIzakaya 之后（营业倒计时已为 0），说明“打烊后仍可服务坐席特殊客”的规则再次成立；本轮窗口约 2 分钟（05:57:13 下单 → 05:59:19 耐心耗尽）。
- 失败仍是 agent 实时性不足：05:57:1x 已发现待服务订单并读取了桌位/托盘，但继续读脚本与源码分析，未先执行“入盘→开盘→Send”，最终错过窗口。
- `night-auto-meirin.csx` 的看护协程仍未触发：此前在白天调用 Start 返回 watching，跨场景后协程宿主（GuestsManager）或 /script 会话上下文失效，进入 Work 后没有轮询动作。需改到 Work 场景开始后再启动，并补 Status 日志验证。
- 结论与下一步：发现待服务订单时先做服务动作，读源码/写文档放到场间；自动看护改为“WorkScene 开始后启动”，配合直接入盘载荷把提交压到秒级。

## 7 月 23 日（进行中）

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 06:01:06 | 7/23 10:00 夜雀小屋 | 存档 #3 载入，AP16、fund 2260242 | ui-snapshot |
| ~06:02 | 八云传送 → 红魔馆 | 扣 200¥，fund 2260042；AP16、10:00 | guest-status-all.cs |
| 06:04~05 | 走近红美铃 (10.14,-17.14) | AITestNav2 在 (10.38,-17.34) 停滞，已进入互动范围 | day-observe.cs |
| 06:06:20 | 邀请完成 | RecordInvitedGuest id=15；AP15、10:30 | guest-status-all.cs + LogOutput |
| 06:06:32~47 | 结束白天、晚间选店 | OnDayOver → 选店面板 | LogOutput |
| 06:07:01 | 选择红魔馆并切 Lv1 | 默认 Lv3，用 ui-level.cs 切到 Lv1 | ui-toggle-state.cs |
| 06:08:47 | 前往开店 | 进入 IzakayaPrepScene | LogOutput |
| 06:09:03 | 应用红美铃菜单 | 8 菜 8 酒 + 厨具 19/17/16；prep-state 复核通过 | prep-menu-meirin.cs + prep-state.cs |
| 06:09:41 | 红魔馆 Lv1 夜场开始 | WorkScene，伙伴为芳香/咲夜/键山雏 | ui-snapshot + LogOutput |
| ~06:11 | 在 Work 内启动自动看护 | AITestAutoServe.Start()；2 秒后 ticks=34、lastTickAge≈0.1 | night-auto-serve.csx |

### 7/23 操作笔记

- 本次验证结论：`night-auto-meirin.csx` 之前在白天 Start 后不触发的根因是协程宿主跨场景失效；新版在 WorkScene 内启动即持续心跳（0.2s/轮询），等待红美铃到店自动验证服务。
- 新增 `night-auto-serve.csx`：带 `Ticks/LastTickTime/Running` 心跳与重复启动保护；检测 id=15 的 SpecialOrder 后直接扣料入盘（不经厨具 UI）、固定桌开盘并 Send+ClosePanel，把提交时间压到秒级。
- 新增 `ui-toggle-state.cs`：读取 UIButtonToggle 的 on/interactable 状态；选店时确认红魔馆默认停在 Lv3，需显式切 Lv1 复现此前环境。
- 当前：红魔馆 Lv1 营业中，今日收入 37¥；普通客已开始流动，自动看护正在等待红美铃。

### 7/23 夜场补记（自动看护首轮调试）

| 时刻 | 事件 | 证据 |
| --- | --- | --- |
| 06:11:44 | 红美铃到 1 号桌；V2 刚启动即对空订单栈调用 PeekOrders 抛异常，协程停止 | LogOutput |
| ~06:12:00 | 红美铃下单 | 饱腹 + 提神（1 号桌） | work-desk |
| 06:12~13 | V2 空栈修复后继续运行（ticks 正常），但 `as SpecialOrder` 转型恒为 null，始终 watching | night-order-debug.cs |
| 06:14:04 | 红美铃耐心耗尽离店 | PatientDepletedLeave |
| 06:14:11 | ResultScene | 触发奖励 0、接待特殊 1（未服务）、普通 3、来访 4、最大气氛 69%、最大连击 3、经验 60、净利 288¥、小费 285¥、经营 488¥ | ui-snapshot |
| 06:15:32 | 覆盖写入存档 #3 | 进入 7/24 10:00 夜雀小屋，AP16、fund 2260530 | 存档文件 + guest-status-all.cs |

### 自动看护根因结论

- 空栈问题：特殊客 SpawnSpecialGuestGroup 入座到 FirstOrder 之间约 15 秒，此时 AllOrdersCount=0；直接 PeekOrders 抛 Stack empty 并终止协程。必须先检查 `AllOrdersCount > 0`。
- 转型问题：`order as GuestsManager.SpecialOrder` 在运行时恒为 null，即使 order.Type=Special、ToString 也含 ReqFoodTag/ReqBevTag。实际可从 OrderBase 直接读 `foodRequest/beverageRequest` 标签 ID，V3 已改用该方式。
- 会话存活：Work 内启动后 0.2s 轮询的心跳正常，证明协程本身可跑；夜场结束时宿主随 GuestsManager 销毁，每场需重新 Start。
- V3 已定义于 `/script`（AITestAutoServeV3），待 7/24 夜场实测。

## 7 月 24 日：V3 自动服务实测成功

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 06:15:32 | 7/24 10:00 夜雀小屋 | 存档 #3 载入，AP16、fund 2260530 | guest-status-all.cs |
| 06:16~17 | 关闭当天自动打开的剪报 | 新一天先弹剪报，需 note-close.cs 后再开快传 | ui-snapshot |
| 06:17:24 | 八云传送 → 红魔馆 | fund 2260330 | guest-status-all.cs |
| 06:18:46 | 邀请红美铃 | RecordInvitedGuest id=15；AP15、10:30 | guest-status-all.cs |
| 06:19:11~18 | 晚间选店 | 红魔馆 + Lv1 | ui-level.cs + LogOutput |
| 06:19:38~41 | 营业准备 | 应用红美铃菜单（8 菜 8 酒） | prep-menu-meirin.cs |
| 06:20:01 | 红魔馆 Lv1 夜场开始 | WorkScene | LogOutput |
| ~06:20:2x | 启动 AITestAutoServeV3 | running=True；06:20:36 检查 ticks=121、lastTickAge=0.1 | night-auto-serve3.csx |
| 06:22:04 | 红美铃到店（第一轮） | SpawnSpecialGuestGroup | LogOutput |
| 06:22:18.58 | 第一轮下单 | 力量涌现 + 古典 | LogOutput + work-desk |
| 06:22:18.66~.68 | V3 自动完成第一单 | ExcuteEventAtCorodinate → Send×2 → EvaluateOrder（2016+19），下单到提交约 0.1 秒 | AITestAutoServeV3.Info + LogOutput |
| 06:23:03 | 红美铃第二轮下单 | 中华 + 古典 | work-desk |
| 06:23:18 | 手动重启 V3 后自动完成第二单 | 蜜汁叉烧(49)+冬酿(19)，EvaluateOrder 成功 | LogOutput |
| 06:23:21 | 红美铃结账离店 | GuestPay → LeaveFromDesk | LogOutput |
| 06:23:52 | 大妖精到店（随机特殊客） | SpawnSpecialGuestGroup；06:24:07 下单清淡+直饮 | LogOutput + work-desk |
| 06:26:19 | ResultScene | 触发奖励 2、接待特殊 2、普通 3、来访 5、最大气氛 100%、最大连击 5、经验 135、净利 759¥、小费 517¥、经营 959¥ | ui-snapshot |
| 06:27:35 | 覆盖写入存档 #3 | 进入 7/25 10:00 夜雀小屋，AP16、fund 2261289 | 存档文件 + guest-status-all.cs |

### 7/24 夜场结论

- V3 空订单保护 + 基类标签字段方案实测成功：两轮红美铃订单都在下单后约 0.1 秒完成提交，不再需要人工逐轮备餐。
- V3 每次成功后即退出（yield break），后续订单需手动重启；已升级为 V4：同一协程持续监听 id=15 与 id=9000、覆盖多轮/多组特殊客、窗口 480 秒。
- 大妖精（id=9000）偏好：料理 清淡/甜/凉爽/小巧/果味，饮品 无酒精/低酒精/可加冰/直饮/甘/气泡。本轮随机入店时菜单未适配（清淡料理不在当日菜单），未服务即离开。
- 下一步菜单：把 55 华光玉煎包替换为 11000 山泉双色果盘（一条料理同时覆盖大妖精全部料理标签），饮品保留 27 冰山毛玉冻柠并针对“低酒精”备 19 冬酿。

## 7 月 25 日：V4 连续自动服务验证

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 06:37 前一日存档 | 7/25 10:00 夜雀小屋 | AP16、fund 2261289 | guest-status-all.cs |
| ~06:2x | 红魔馆快传 + 邀请红美铃 | 06:30:44 RecordInvitedGuest id=15；AP15、10:30 | LogOutput |
| 06:31:5x | 应用“红美铃+大妖精”菜单 | 8 菜含 11000 山泉双色果盘替换 55；8 酒含 27/19；厨具 19/17/16 | prep-state.cs |
| 06:31:59.6 | 红魔馆 Lv1 夜场开始 | WorkScene | LogOutput |
| ~06:32 | 启动 AITestAutoServeV4 | 同时监听 id=15/9000，480s 窗口，成功后不退出 | night-auto-serve4.csx |
| 06:34:02.8 | 红美铃到店 | SpawnSpecialGuestGroup | LogOutput |
| 06:34:16.2 | 红美铃下单 | FirstOrder | LogOutput |
| 06:34:16.38 | V4 自动提交 | ExcuteEventAtCorodinate → Send×2 → EvaluateOrder；下单后约 0.2 秒 | LogOutput + V4.Info |
| 06:34:27 | 红美铃结账离店 | GuestPay → LeaveFromDesk | LogOutput |
| 06:36:10 | ResultScene | 触发奖励 0、接待特殊 1、普通 2、来访 3、最大气氛 85%、最大连击 4、经验 99、净利 391¥、小费 288¥、经营 591¥ | ui-snapshot |
| 06:37:11 | 覆盖写入存档 #3 | 进入 7/26 10:00 夜雀小屋，AP16、fund 2261680 | 存档文件 + guest-status-all.cs |

### 7/25 结论

- V4 连续模式成立：同一协程内完成服务后继续轮询，夜场全程无需人工重启；本轮红美铃单次到店一单即完成（菜单 2016/19 均被正确选用）。
- 本轮未随机出现大妖精，其 id=9000 分支与 11000/27 映射仍待实测；菜单已备好（11000 同时覆盖其全部料理标签）。

## 7 月 26 日：通用看护 V5 + 手工补特殊客

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 06:41:17 | 红魔馆邀请红美铃 | RecordInvitedGuest id=15 | LogOutput |
| 06:42:19.1 | 红魔馆 Lv1 夜场开始 | WorkScene；应用“红美铃+大妖精”菜单 | LogOutput |
| ~06:42:3x | 启动 AITestAutoServeV5 | 按菜单标签 ID 通用匹配任意特殊客 | night-auto-serve5.csx |
| 06:44:22.4 | 红美铃到店 | SpawnSpecialGuestGroup | LogOutput |
| ~06:44:35.9 | V5 自动服务红美铃 | 2016+13（一击☆必杀+红魔馆红茶），EvaluateOrder 成功 | LogOutput + V5.Info |
| 06:46:10.3 | 射命丸文(id=4000)到店 | SpawnSpecialGuestGroup | LogOutput |
| 06:46:24.3 | 射命丸文下单 | 招牌 + 提神；V5 报 unsupported（当日菜单无招牌菜） | V5.Info |
| 06:47:36.6 | 手工直接备餐 + 开盘 | 大江户船祭(2008)+冰山毛玉冻柠(27) 入 2 号桌 | work-prep-aya.cs |
| 06:47:43.8 | 手工提交 | EvaluateOrder 成功；V5 协程后续继续自动处理追加轮 | LogOutput |
| 06:48:00.9 / 06:48:18.3 | 射命丸文追加轮自动服务 | EvaluateOrder ×2（V5 未真正停止的协程继续工作） | LogOutput |
| 06:48:36.7 | ResultScene | 触发奖励 3、接待特殊 2、普通 3、来访 5、最大气氛 100%、最大连击 11、经验 332、净利 1527¥、小费 870¥、经营 1727¥ | ui-snapshot |
| 06:49:15 | 覆盖写入存档 #3 | 进入 7/27 10:00 夜雀小屋，AP16、fund 2263207 | 存档文件 + guest-status-all.cs |

### 7/26 结论与发现

- V5 通用标签匹配可服务红美铃（菜单内标签）；对菜单外标签会明确报 unsupported 而非误服务。
- 菜单外特殊客（招牌）可临时用“直接入盘 + 固定桌”补服务，2008 大江户船祭是可靠的自有招牌菜；后续菜单可考虑保留一道招牌菜扩大覆盖。
- `Reset()` 只改标志、没有真正停协程，V5 在结算前继续完成了射命丸文追加轮；这是意外收益，但也说明需要 StopCoroutine 才算真正停止。
- 射命丸文(id=4000)偏好：料理 招牌/适合拍照/家常/下酒/肉/和风，饮品 高酒精/提神/可加冰/烧酒。
- 西侧 `_ResourceExample_Daiyousei` 在白天被围栏隔离，实测直线与北绕均无法接近；大妖精(id=9000)分支只能靠夜场随机验证。

## 7 月 27 日：V6 覆盖缺口（幽幽子漏单）

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 06:51:43 | 红魔馆快传 | SwapMap → ScarletMansion | LogOutput |
| 06:52:56 | 邀请红美铃 | RecordInvitedGuest id=15 | LogOutput |
| 06:53:09 | 结束白天、选店 | OnDayOver | LogOutput |
| 06:53:39~50 | 营业准备 | 进入 IzakayaPrepScene，应用“红美铃+大妖精”菜单（35/7/68/10/34/61/2016/11000，酒 22/19/13/1001/20/17/14/27） | LogOutput |
| 06:53:59 | 红魔馆 Lv1 夜场开始 | WorkScene，启动 V6 自动看护 | LogOutput |
| 06:56:02.4 | 红美铃到店 | SpawnSpecialGuestGroup | LogOutput |
| 06:56:17.8 | V6 自动服务红美铃 | ExcuteEventAtCorodinate → Send×2 → EvaluateOrder 成功 | LogOutput |
| 06:57:39.5 | 幽幽子(id=40)到店 | SpawnSpecialGuestGroup；订单=大份+可加冰 | work-desk + V6.Info |
| ~06:58 | V6 判定 unsupported | 菜单无“大份”标签料理，保持轮询未服务 | V6.Info |
| 06:59:59.7 | 幽幽子耐心耗尽离店 | PatientDepletedLeave | LogOutput |
| 07:00:04.7 | ResultScene | 接待特殊 2、普通 6、来访 8、最大气氛 100%、最大连击 6、经验 153、净利 743¥、小费 451¥ | ui-snapshot |

### 7/27 结论与下一步

- V6 能自动覆盖红美铃（料理/饮品标签均在菜单内），但菜单与候选列表都缺“大份”；幽幽子请求标签大份+可加冰时只能等手工服务，本次未赶上。
- 自有“大份”料理：全肉盛宴(1002, Grill)、绝叫关东煮(2002, Pot)、鱼跃龙门(2014, Steamer)、海盗熏肉(3004, Grill)。下一夜菜单至少加入一道，并把候选列表同步扩展，避免同类漏单。
- 特殊客的耐心窗口不宽裕：从下单到离店约 1~2 分钟，遇到 unsupported 应立即手工“直接入盘+开盘提交”，分析类工作放到场间。

## 7 月 28 日：V7 补大份、魔理沙菌类需手工补

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 07:01:28 | 7/28 10:00 夜雀小屋 | 载入存档 #3，AP16、fund 2263950 | guest-status-all.cs |
| 07:03:04 | 八云快传 → 红魔馆 | fund 2263750、AP16、10:00 | LogOutput + guest-status-all.cs |
| 07:05:40 | 邀请红美铃 | RecordInvitedGuest id=15；AP15、10:30 | LogOutput + guest-status-all.cs |
| 07:06:30~50 | 晚间选店 | 红魔馆 Lv1；进入 IzakayaPrepScene | LogOutput |
| 07:09:31 | 应用 V7 菜单 | 1002 全肉盛宴替换 68 毛玉熔岩豆腐；酒水 8 种不变；厨具 19/17/16 | prep-menu-v7.cs + prep-state.cs |
| 07:09:45 | 红魔馆 Lv1 夜场开始 | WorkScene；随后启动 AITestAutoServeV7 | LogOutput + V7.Info |
| 07:11:49 / 07:12:03 | 红美铃到店并下单 | V7 自动服务（2016+13），下单到 EvaluateOrder 约 0.1 秒 | LogOutput + V7.Info |
| 07:12:17 | 红美铃结账离店 | GuestPay → LeaveFromDesk | LogOutput |
| 07:13:27 / 07:13:40 | 魔理沙(id=10)到店并首轮下单 | V7 自动服务（2008+19，传说+可加冰） | LogOutput |
| ~07:15:07 | 魔理沙追加轮报 unsupported | 订单=菌类+可加冰；V7 候选池缺菌类 | V7.Info |
| 07:16:20~26 | 手工直接备餐+提交 | 烤蘑菇(FoodID 38)+27 入 0 号桌，EvaluateOrder 成功 | work-prep-marisa.cs + LogOutput |
| 07:16:29~17:07 | 魔理沙追加轮自动服务 | V7 继续完成多次（2008+19、7+19 等），EvaluateOrder ×3 | V7.Info + LogOutput |
| 07:17:19 / 07:17:24 | 魔理沙结账离店 → ResultScene | 触发奖励 3、特殊 2、普通 3、来访 5、最大气氛 100%、最大连击 11、经验 308、净利 1672¥、小费 969¥、经营 1872¥ | ui-snapshot |

### 7/28 结论与下一步

- V7 首战成立：菜单与候选池补入 1002/3004 后，本夜未再出现幽幽子漏单；“大份”缺口已由全肉盛宴覆盖（本轮随机未遇幽幽子）。
- 魔理沙(id=10)同样会连续追加多轮；本夜首轮传说+可加冰由 2008+19 自动完成，菌类轮需手工烤蘑菇+冻柠，后续轮 V7 可自动。
- V7 候选池仍缺“菌类”，下一次编译前应在 FoodRecipes 中加入 32 烤蘑菇或 3017 什锦天妇罗（自带招牌/和风/力量涌现），避免再手工补。
- 新增载荷：`prep-menu-v7.cs`、`night-auto-serve7.csx`、`tag-v7-menu.cs`、`tag-find-mushroom.cs`、`work-prep-marisa.cs`。

## 8 月 1 日：V7 全自动双特殊客夜场

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 07:18:15 | 8/1 10:00 夜雀小屋 | 存档 #3 载入，AP16、fund 2265622 | guest-status-all.cs + ui-snapshot |
| 07:19:35 | 八云快传 → 红魔馆 | fund 2265422、AP16、10:00 | LogOutput + guest-status-all.cs |
| 07:21:24 | 邀请红美铃 | RecordInvitedGuest id=15；AP15、10:30 | LogOutput + guest-status-all.cs |
| 07:21:52~07:22:17 | 晚间选店与准备 | 红魔馆 Lv1，应用 V7 菜单（含 1002） | LogOutput |
| 07:22:28 | 红魔馆 Lv1 夜场开始 | WorkScene；启动 AITestAutoServeV7 | LogOutput + V7.Info |
| 07:24:32 / 07:24:46 | 红美铃到店并下单 | V7 自动服务（7 炙猪肉饭团+22 玉露茶），EvaluateOrder 通过 | V7.Info + LogOutput |
| 07:26:09 / 07:26:23 | 第二位特殊客(id=37)到店并首轮下单 | V7 自动服务（68+22），EvaluateOrder 通过 | V7.Info + LogOutput |
| 07:26:32 | 打烊 | TryCloseIzakaya；坐席特殊客仍在可服务窗口 | LogOutput |
| 07:26:37~07:27:31 | id=37 追加轮 | V7 连续自动完成（68+17 等），随后 GuestPay 离店 | V7.Info + LogOutput |
| 07:27:36 | ResultScene | 触发奖励 4、特殊 2、普通 3、来访 5、最大气氛 100%、最大连击 10、经验 269、净利 1778¥、小费 915¥、经营 1978¥ | ui-snapshot |

### 8/1 结论

- 本夜零人工干预：红美铃与第二位特殊客(id=37)的全部订单都由 V7 自动完成，打烊后的追加轮也继续可用。
- id=37 的订单可用 68 毛玉熔岩豆腐 + 22/17 覆盖；68 虽已不在当日菜单，但候选池保留即可直接入盘服务（再次验证直接服务不依赖菜单）。
- 存档已覆盖写入 #3，等待进入下一游戏日。

## 8 月 2 日：V7 再次全自动双特殊客

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 07:28:21 | 8/2 10:00 夜雀小屋 | 存档 #3 载入，AP16、fund 2267400 | guest-status-all.cs + ui-snapshot |
| 07:29:16 | 八云快传 → 红魔馆 | fund 2267200、AP16、10:00 | LogOutput + guest-status-all.cs |
| 07:31:08 | 邀请红美铃 | RecordInvitedGuest id=15；AP15、10:30 | LogOutput |
| 07:31:47~07:32:01 | 晚间选店与准备 | 红魔馆 Lv1，应用 V7 菜单（含 1002） | LogOutput |
| 07:32:04 | 红魔馆 Lv1 夜场开始 | WorkScene；启动 AITestAutoServeV7 | LogOutput + V7.Info |
| 07:34:08 / 约 07:34:2x | 红美铃到店并下单 | V7 自动服务（food 8+13），EvaluateOrder 通过 | V7.Info + LogOutput |
| 07:35:57 / 约 07:36:1x | 第二位特殊客(id=27)到店并下单 | V7 自动服务（68+17），EvaluateOrder 通过 | V7.Info + LogOutput |
| 07:36:09 | 打烊 | TryCloseIzakaya | LogOutput |
| 07:36:43 | id=27 追加轮后结账离店 | V7 完成（49+27 等），GuestPay → LeaveFromDesk | V7.Info + LogOutput |
| 07:36:48 | ResultScene | 触发奖励 2、特殊 2、普通 2、来访 4、最大气氛 100%、最大连击 5、经验 130、净利 795¥、小费 433¥、经营 995¥ | ui-snapshot |

### 8/2 结论

- 再次实现零人工干预：红美铃与 id=27 均由 V7 自动服务并完成追加轮；本夜客流较少，结算数值低于 8/1。
- 连续两晚验证 V7 在“双特殊客 + 打烊后追加轮”场景下稳定工作；菜单与候选池未再调整。

## 8 月 3 日：寒潮新闻 + 单特殊客夜场

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 07:38:15 | 8/3 10:00 夜雀小屋 | 载入存档 #3；弹出新闻剪报（寒潮/白夜，凉爽被讨厌） | ui-snapshot |
| ~07:38:2x | 关闭剪报 | note-close.cs 成功 | ui-snapshot |
| 07:39:07 | 八云快传 → 红魔馆 | fund 2267995、AP16、10:00 | LogOutput + guest-status-all.cs |
| 07:40:26 | 邀请红美铃 | RecordInvitedGuest id=15；AP15、10:30 | LogOutput |
| 07:41:10~29 | 晚间选店与准备 | 红魔馆 Lv1，应用 V7 菜单（含 1002） | LogOutput |
| 07:41:29 | 红魔馆 Lv1 夜场开始 | WorkScene；启动 AITestAutoServeV7 | LogOutput + V7.Info |
| 07:43:34 / 约 07:43:5x | 红美铃到店并下单 | V7 自动服务，EvaluateOrder 通过 | V7.Info + LogOutput |
| 07:45:35 | 打烊 | TryCloseIzakaya | LogOutput |
| 07:45:40 | ResultScene | 触发奖励 0、特殊 1、普通 3、来访 4、最大气氛 83%、最大连击 4、经验 99、净利 314¥、小费 261¥、经营 514¥ | ui-snapshot |

### 8/3 结论

- 本夜只出现红美铃一位特殊客，V7 自动完成；新闻提示“凉爽”食物被讨厌，后续菜单覆盖需留意流行标签变化。
- 存档已覆盖写入 #3。

## 8 月 4 日：id=36 辣+甘需手工补

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 07:47:2x | 8/4 10:00 夜雀小屋 | 载入存档 #3，AP16、fund 2268509 | guest-status-all.cs + ui-snapshot |
| 07:47:39 | 八云快传 → 红魔馆 | fund 2268309、AP16、10:00 | LogOutput + guest-status-all.cs |
| 07:48:45 | 邀请红美铃 | RecordInvitedGuest id=15；AP15、10:30 | LogOutput |
| 07:49:24~42 | 晚间选店与准备 | 红魔馆 Lv1，应用 V7 菜单（含 1002） | LogOutput |
| 07:49:42 | 红魔馆 Lv1 夜场开始 | WorkScene；启动 AITestAutoServeV7 | LogOutput + V7.Info |
| 07:51:46 / 约 07:52:0x | 红美铃到店并下单 | V7 自动服务（2016+22），EvaluateOrder 通过 | V7.Info + LogOutput |
| 07:53:35 / 约 07:53:5x | 第二位特殊客(id=36)到店并下单 | 订单=辣+甘；V7 候选池缺辣，报 unsupported | V7.Info |
| 07:53:47 | 打烊 | TryCloseIzakaya | LogOutput |
| 07:55:27 | 手工直接备餐+提交 | 2017 地狱激辛警告！+20 十四夜 入 0 号桌，EvaluateOrder 成功 | work-prep-laguest36.cs + LogOutput |
| 07:55:44 | ResultScene | 触发奖励 2、特殊 2、普通 2、来访 4、最大气氛 100%、最大连击 5、经验 127、净利 1411¥、小费 682¥、经营 1611¥ | ui-snapshot |

### 8/4 结论与下一步

- id=36 的标签为辣+甘（foodTag 34/bevTag 13）；当日 V7 只缺辣，手工用高值 2017+20 补上成功。
- 已新增 `night-auto-serve8.csx`：候选池补入 3017/32（菌类）与 2017/49/5002（辣），下一夜编译后应能自动覆盖已知缺口。
- 新增载荷：`tag-find-34.cs`、`work-prep-laguest36.cs`。
- 存档已覆盖写入 #3。

## 8 月 5 日：V8 首战（含 id=36 自动覆盖）

| 时刻 | 位置/操作 | 结果 | 证据 |
| --- | --- | --- | --- |
| 07:57:01 | 8/5 10:00 夜雀小屋 | 载入存档 #3，AP16、fund 2269920 | guest-status-all.cs + ui-snapshot |
| 07:58:31 | 八云快传 → 红魔馆 | fund 2269720、AP16、10:00 | LogOutput + guest-status-all.cs |
| 08:00:07 | 邀请红美铃 | RecordInvitedGuest id=15；AP15、10:30 | LogOutput |
| 08:00:57~08:01:22 | 晚间选店与准备 | 红魔馆 Lv1，应用 V7 菜单；WorkScene 启动 AITestAutoServeV8 | LogOutput + V8.Info |
| 08:03:26 / 约 08:03:4x | 红美铃到店并下单 | V8 自动服务（food 7+13），EvaluateOrder 通过 | V8.Info + LogOutput |
| 08:05:14 / 约 08:05:2x | id=36 到店并下单 | V8 自动服务（2008+19），EvaluateOrder 通过，不再手工补 | V8.Info + LogOutput |
| 08:05:26 | 打烊 | TryCloseIzakaya | LogOutput |
| 08:05:56 | ResultScene | 触发奖励 1、特殊 2、普通 2、来访 4、最大气氛 100%、最大连击 8、经验 233、净利 1737¥、小费 857¥、经营 1937¥ | ui-snapshot |

### 8/5 结论

- V8 首战即把 id=36 纳入自动覆盖（2008+19），未再出现手工干预；菌类/辣缺口由新增候选池补上。
- 存档已覆盖写入 #3。
