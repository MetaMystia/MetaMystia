# 迁移缺口清单

> **已被取代**：本文件是首轮口径的缺口台账。后续两轮的结论见 [`mystia-extension-port-pass2.md`](mystia-extension-port-pass2.md) 与 [`mystia-extension-port-pass3.md`](mystia-extension-port-pass3.md)；`src/MetaMystia.Mod/Patches/` 已整体删除，模组侧不再以 Harmony 形式保留任何补丁。本文件只作历史记录，不要当现状读。

本文件记录迁移后**仍以 Harmony 兼容形式保留**的补丁（位于 `src/MetaMystia.Mod/Patches/Compat/`），以及框架侧尚未提供的表达能力。逐条事实见 [`port/audit-facts.md`](port/audit-facts.md)，中间件侧能力清单见 [`port/mefx-handoff.md`](port/mefx-handoff.md)，本轮工单见 [`port/mefx-next-round-plan.md`](port/mefx-next-round-plan.md)。

格式：文件 | 原方法 | 行为（观察 / 跳过 / 改写） | 缺的回调。

## 1. 仍在册的兼容补丁

按本轮工单（W1–W7）的退役范围，`Patches/Compat/` 只应剩下下述四类；W1–W6 批次实际删除的文件见交付报告第 2 节。

### 1.1 幽幽子挑战族（11 个 `Yuyuko*` 类）

| 文件 | 原方法 | 行为 | 缺的回调 |
| --- | --- | --- | --- |
| `YuyukoBossDataPatch.cs` | `YuyukoBossData.MainChallengeLoop` | 观察 | 需要持有并停止原版协程句柄的能力 |
| `YuyukoChallengeContextPatch.cs` | Boss 评价回调（编译器生成） | 观察 + 跳过 + 改写 | Boss 专属评价钩子；写回捕获变量 |
| `YuyukoExtraDialogData__c__DisplayClass4_0Patch.cs` | 挑战确认回调（编译器生成） | 跳过 | 挑战确认钩子 |
| `YuyukoLockCookersPatch.cs` | 吞厨具协程 `MoveNext`（编译器生成） | 跳过 + 观察 | 协程状态机级钩子 |
| `YuyukoMainLoopPatch.cs` | `_MainChallengeLoop_d__16.MoveNext`（编译器生成） | 拦截（挂起一帧） | 协程状态机级钩子 |
| `YuyukoOnFailPatch.cs` | 失败剧情协程 `MoveNext`（编译器生成） | 观察 | 挑战失败通知 |
| `YuyukoPhase2GuestSpawnPatch.cs` | 二阶段刷客协程（编译器生成） | 跳过（挂起） | 阶段刷客可控（语义近似 `SetSpawnEnabled`） |
| `YuyukoPhase3GuestSpawnPatch.cs` | 三阶段刷客协程（编译器生成） | 跳过 + 写游戏捕获变量 | 同上 + 捕获变量写回 |
| `YuyukoRetakeContextPatch.cs` | 重打评价回调与收尾（编译器生成） | 观察 + 跳过 + 改写 | 同 Boss 评价钩子 |
| `YuyukoTimedNegativeSpellPatch.cs` | 限时负面符卡协程（编译器生成） | 跳过 | 协程状态机级钩子 |
| `YuyukoTimingPatch.cs` | 阶段计时协程（编译器生成） | 跳过 + 改写 | 阶段计时同步钩子 |

### 1.2 幽幽子相关控制器（2 个类）

| 文件 | 原方法 | 行为 | 缺的回调 |
| --- | --- | --- | --- |
| `NightSceneDirectorPatch.cs` | `TryLeaveSession` / `GetControlled` / `SpawnManualControlledSpecialGuest` | 观察 | 幽幽子本体捕获 |
| `IncomeControllerYuyukoPatch.cs` | `SetContext` / `SetTargetProgress` | 观察 + 改写 | HUD 控制器级钩子 |

### 1.3 面板与立绘（2 个类）

| 文件 | 原方法 | 行为 | 缺的回调 |
| --- | --- | --- | --- |
| `WorkSceneSustainedPannelPatch.cs` | `OpenServePanel` / `OnFastForwardSubmit` | 改写回调 + 跳过 | 面板回调包装与「营业内快进」拦截钩子 |
| `NoteBookProfilePannelPatch .cs` | `NoteBookProfilePannel.OnPanelOpen` | 改写 | 面板开启 + 皮肤立绘（若 `IPortraitProvider` 覆盖该用途则删除） |

### 1.4 日程缺口（1 个类）

| 文件 | 原方法 | 行为 | 缺的回调 |
| --- | --- | --- | --- |
| `RunTimeSchedulerGapsPatch.cs` | `Method_Internal_Static_Void_Action_PDM_0`（灵梦保护窗口，编译器生成的局部函数） | 跳过 | 无可用落点（`ScheduleEvent` 语义不等价）；`DuringReimuProtection` 令牌一并保留 |

## 2. 编译可行性

生成互操作（`Mystia.InteropGen` 产物）与游戏自带 BepInEx interop 对**编译器生成类型**的命名不同：

- 生成互操作用源工程命名：`<>c__DisplayClass16_6`、`<MainChallengeLoop>g__LockCookersYuyuko|41>d` —— 含 `<>`、`|`，**无法在 C# 源码中引用**。
- BepInEx interop 用发行版命名：`__c__DisplayClass16_6`、`ObjectCompilerGenerated…InObSpCoObObUnique` —— 可引用。

本轮前已切换到游戏自带的 BepInEx interop（见交付报告偏差第 1 条），`MetaMystia.csproj` 中**不再有任何 `<Compile Remove>` 排除**，`Patches/Compat/` 与 `Managers/YuyukoGuestSync*.cs` 全部参与编译。此前按 4.3.1 布局排除的幽幽子族已按 4.4.0e 互操作成员布局适配完毕。

## 3. 框架能力缺口

### 3.1 视角 retrofit（WV）在框架侧尚未落地

SDK 已定义 `Mystia.Scenes.{ServePannelView, StoragePannelView, GuideMapView, PrepConfigView, ShopPannelView}` 并实现转发体，但桥接仍以游戏面板派发 `IWorkListener`/`IPrepListener`，且部分视图成员不足以表达模组既有用法。

| 缺口 | 现象 | 受影响的实现 |
| --- | --- | --- |
| 监听签名未替换 | `Mystia.Net.Sdk/GameApi/Listeners.cs` 的 `IWorkListener`（7 个 serve-panel 成员）与 `IPrepListener`（4 个 prep-panel 成员）仍以 `WorkSceneServePannel`/`IzakayaSelectorPanel_New`/`IzakayaConfigPannel` 为形参；桥接 `ListenerSeams.Work.cs`、`HarmonySeams.cs` 也仍派发面板 | 模组已按视图签名实现 `WorkSync`，但在框架替换签名之前这些方法不构成接口实现，派发会落到接口的默认空实现（编译通过、运行期静默失效） |
| `StoragePannelView` 无投递路径 | 桥接未在 `WorkSceneStoragePannel.OnPanelOpen` 构造/派发该视图，`IWorkListener` 也没有承载它的成员 | `WorkSync.RefreshStoragePanel`（远端存放/取出食物后的本地面板刷新）只能继续直接解析游戏面板 |
| `GuideMapView` 不暴露地图点 | `IPrepNightMapServices.Confirm(IGuideMapSpot spot, IzakayaLevel level)` 需要地图点对象，而视图只提供 `SelectedMapLabel`/`SelectedLevel` | `PrepSync.OnGuideMapConfirmed` 无法改用视图（选店共识依赖提交时记下的 `IGuideMapSpot`） |
| `PrepConfigView` 缺稳定实例标识 | 视图按回调新建，`PrepSync` 用面板实例是否变化判断「面板开启窗口」 | `PrepSync.OnConfigTabSelected` 无法改用视图 |
| `PrepConfigView` 缺原面板回读 | `PrepSync.ConfigPanel` 的消费者 `YuyukoBossDataPatch` 需要 `IsPanelOpened`/`name`/`OnPanelCloseFadeFinishToken`/`ClosePanel` | `PrepSync` 的备菜面板缓存无法改为视图 |
| `ServePannelView` 缺「只渲染视觉、不占待上菜槽位」的入口 | 原实现把 `willServeFood/Beverage` 置空、仅以「不可取消」方式渲染已确认的菜 | `GuestFSM.TryUpdateServePanel(canCancel: false)` 只能「写槽位 → 刷新 → 清槽位」近似，图标可取消标志与原实现不同 |

### 3.2 游戏专有链路

| 缺口 | 现象 | 受影响的实现 |
| --- | --- | --- |
| 协程状态机级钩子 | 幽幽子挑战的刷客/计时/失败/立绘重建都挂在编译器生成的 `MoveNext` 上 | §1.1 中的 9 个 `Yuyuko*` 类 |
| Boss 评价钩子 | 目标为闭包方法，且需写回捕获变量 | `YuyukoChallengeContextPatch`、`YuyukoRetakeContextPatch` |
| HUD 控制器钩子 | 幽幽子生命值只在 UI 刷新点可观测 | `IncomeControllerYuyukoPatch` |
| 面板回调包装 | `WorkSceneSustainedPannel.OpenServePanel` 的三个回调需要按订单号限定 | `WorkSceneSustainedPannelPatch` |
| 营业内快进拦截 | 无「营业场景快进」前置钩子 | `WorkSceneSustainedPannelPatch` |
| 运行时立绘提供者覆盖面 | `IPortraitProvider` 只覆盖服装立绘，未覆盖 NoteBook 面板的立绘替换 | `NoteBookProfilePannelPatch .cs` |
| 灵梦保护窗口 | 目标是编译器生成的局部函数，`ScheduleEvent` 语义不等价 | `RunTimeSchedulerGapsPatch`（保持缺口） |

### 3.3 模组侧仍依赖的旁路令牌

| 项 | 现状 | 结论 |
| --- | --- | --- |
| `Managers/GuestReentryPermits.cs` | 已删除：嵌套离场由桥接 `LeaveDispatch` 按「最外层离场方法」去重；「模组自发离场不广播」改由 `GuestSync.LeaveBroadcastSuspended` 布尔旁路表达 | 离场 seam 已取代 |
| `Patches/PatchBypassToken.cs` | 仍被 `RunTimeSchedulerGapsPatch.DuringReimuProtection` 使用 | **保留文件**；其中已无消费者的 `PatchSkipPermit` 子类已删除 |
| `Patches/HarmonyPrefixFlow.cs` | `SkipOriginal`/`RunOriginal` 被 §1 的 11 个兼容补丁使用 | **保留**（无法删除） |
| `Patches/Compat/` 死文件 | 逐个核对后未发现「能力已由中间件覆盖但文件仍留存」的项 | 本轮无删除 |

### 3.4 框架数据面已有、桥接尚未写入的字段

| 字段 | 现状 | 说明 |
| --- | --- | --- |
| `NpcData.Description` | 未写 | 日间语言表没有描述列，暂无处落地 |
| `GuestRequestLine.Enable` | 未写 | 请求行仍全部写入，未按开关过滤 |
| `DialogLine.Actions` 与 5 个行级 flag | 未写 | 逐行仍只写说话人/位置/文本，并复用模板第 0 行 meta |
| `SpecialGuestData.Kind`/`IsParticular`/`IsCollabCharacter`/`HideInAlbum`/`CommisionAreaLabel`/`Kizuna` | 未写 | 需要逐项对照游戏侧构建与 kizuna 事件链 |
| `SpecialGuestData.SpawnMarker` | 由桥接点位管线承担，不经数据表写入 | 见 `SpawnMarkerPipeline` |
| 符卡宣言立绘 pivot | 已知差异 | 框架按整图缓存（pivot 0.5/0.5），原实现为宣言重建 pivot，需按 pivot 另建 sprite |

设计性偏差（非缺口）：`OnInjectNpcs` 与 `OnInjectNormalGuests` 故意留空——原版会从注入后的 `DataBaseCharacter.SpecialGuest` 自行生成白天 NPC 记录，另行注入会改掉定位点与对话池键；资源包格式也没提供普通客人所需字段。

### 3.5 实施中发现并修复的互操作缺陷

`MystiaInteropGen`/BepInEx 生成的壳代码把「含引用的原生结构体」生成为类，导致**方法参数**传入装箱指针，值被错写到值槽（字段属性与 `Il2CppReferenceArray` 元素写入不受影响）。桥接侧已对 `DataBaseDay.allNPCs`/`allMerchants`/`mapData`、`DataBaseNight.SpecialGuestSpellPortrayal`、`DataBaseLanguage.SpecialGuest` 改走 `il2cpp` 原生 `set_Item` + 拆箱通道，并修好了 NPC 反向标签表原先因反射转换失败而只写单元素的问题。游戏原生方法的结构体参数（例如 `Dictionary.TryGetValue` 的 `out`）仍受该缺陷影响，桥接已在该类调用点绕开。

## 4. 本轮收尾更新（W1–W7 / WV / WY 完成后）

- **WV 缺口已全部关闭**：§3.1 中「监听签名未替换 / `GuideMapView` 不暴露地图点 / `PrepConfigView` 缺稳定实例标识 / `PrepConfigView` 缺原面板回读 / `StoragePannelView` 无投递路径」五条均已解决——签名已替换为视图，`GuideMapView.SelectedSpot` 与 `PrepConfigView.{IsOpen,Name,CloseWithFadeToken}` 已补，`StoragePannelView` 因无消费方而删除。
- **`OnGuideSpotSelected` 保留默认实现**：模组侧无消费点（原 `cachedSpots` 缓存已被 `OnGuideMapConfirmed` 的提交记录取代），故不强制实现。
- **`SceneLoops.cs` 的 16 个脚手架默认体保留**：其内层实现类位于 `SceneServices.cs`，删除会 CS0535；这些成员实际由 `PresentationServices`/`WorkSceneGuestServing`/`WorkSceneTimeServices` 等装饰器覆盖，运行时不可达。
- **当前在册兼容补丁 16 个**：幽幽子族 12（`Yuyuko*` 11 + `IncomeControllerYuyukoPatch`）、`NightSceneDirectorPatch`（本体捕获）、`NoteBookProfilePannelPatch`（笔记本立绘开关，待定去留）、`RunTimeSchedulerGapsPatch`（奖励拦截 + 灵梦保护窗口）、`WorkSceneSustainedPannelPatch`（按当前订单包装回调 + 营业内快进）。
- **新增已知缺口**：`PlayVfx`/`PlayAudio` 为占位（需框架补模组级资源路径上下文与预制件/音频加载管线）；符卡宣言立绘 pivot 数据面已带但桥接未消费；`IWorkMetricsListener` 的后缀不带运算类型；`EventManager.Fever` 仅在 5 个奖励方法路径上被覆盖；`ServeBeverage` 为同步落菜、无投掷动画与飞行中复查。
- **已知测试抖动**：`MetaMystia.Network.Tests` 在高负载下偶发 `Room operation pending or already joined`（测试自身时序竞态，重跑即通过），与本轮改动无关。
