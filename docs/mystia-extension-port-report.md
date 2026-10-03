# 迁移交付报告

配套：[`mystia-extension-port.md`](mystia-extension-port.md)（规范）、[`port/`](port/)（工单与审计）、[`mystia-extension-port-gaps.md`](mystia-extension-port-gaps.md)（缺口台账）。

## 1. 验收结果

| 项 | 命令 | 结果 |
| --- | --- | --- |
| 中间件编译 | `dotnet build MystiaExtensionFramework.slnx -c Debug` | 由框架侧批次验证（本轮模组批次未复验，且禁止改动中间件仓库） |
| 中间件测试 | `dotnet test src/Mystia.Net.Sdk.Tests` | 同上（框架侧批次报 21/21） |
| 模组编译 | `dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Debug` | 通过（0 错误；710 条既有可空性/文档警告） |
| 服务端编译 | `dotnet build src/MetaMystia.Server` | 通过 |
| 网络测试 | `dotnet run --project src/MetaMystia.Network.Tests` | ALL PASS（214 条断言） |
| 流程测试 | `dotnet run --project src/MetaMystia.Flow.Tests` | ALL PASS（103 条断言） |
| 静态检查 | `bash docs/port/static-check.sh` | 8/8 通过 |

规范里写的 `dotnet test src/MetaMystia.Network.Tests` / `dotnet test src/MetaMystia.Flow.Tests` 对这两个 `OutputType=Exe` 的自研断言工程不适用（它们没有测试框架），实际可用的等价命令是 `dotnet run --project`，上表即为实测命令。全部结论为**源码/编译级**，未做游戏内实测。

静态检查项：模组内无 BepInEx API、无 Compat 之外的 Harmony、无 `BasePlugin`、无 `IGuestDirector` 实现、无 Compat 之外的 `HarmonyReversePatch`、无 `UnityEngine.Debug`、无 `ManualLogSource`、无 `MyPluginInfo`。

## 2. 补丁台账

下表为迁移首轮的逐文件台账。本轮（W1–W7 工单）在首轮基础上又退役了下列兼容补丁（迁入监听/服务后删除文件）：

`CollabBehaviourComponentPatch.cs`、`DataBaseNightPatch.cs`、`DaySceneChatSelectionPannel__c__DisplayClass17_0Patch.cs`、`DaySceneMapPatch.cs`、`DaySceneShopPannelPatch.cs`、`DaySceneSustainedPannelPatch.cs`、`DaySceneUIManagerPatch.cs`、`DesktopPlatformProfilePatch.cs`、`NormalGuestsControllerPatch.cs`、`QTERewardManagerPatch.cs`、`RunTimeAlbumPatch.cs`、`RunTimeSchedulerPatch.cs`、`SaveManagementPatch.cs`、`SellablePatch.cs`、`SpecialGuestsControllerPatch.cs`、`StatusTrackerPatch.cs`、`TrackedMissionDataPatch.cs`、`UIManagerPatch.cs`、`WorkSceneCookingSelectionPannelPatch.cs`、`NightSceneEventManagerPatch.cs`、`BuffPatch.cs`（共 21 个，随 W1–W6 批次落地）。

**最终在册清单以缺口台账 §1 为准**（共 16 个：幽幽子挑战族 11 个类 + 幽幽子相关控制器 2 个类 + 面板/立绘 2 个类 + 日程缺口 1 个类）。其中 `RunTimeSchedulerPatch.cs` 改名并缩为 `RunTimeSchedulerGapsPatch.cs`（只留灵梦保护窗口一项）。

### 首轮台账（Historical）

| 原补丁类型 | 归宿 | 编译状态 |
| --- | --- | --- |
| `MainSceneManagerPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `DaySceneManagerPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `NightSceneManagerPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `PrepNightSceneManager.cs` | 已迁 | 已删除（迁入监听/服务） |
| `ResultSceneManagerPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `StaffSceneManagerPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `DesktopPlatformProfilePatch.cs` | 缺口 | 本轮退役（已删除） |
| `CharacterControllerInputGeneratorComponentPatch.cs` | 升级 | 已删除（迁入监听/服务） |
| `DialogPannelPatch.cs` | 删除 | 已删除（迁入监听/服务） |
| `IzakayaSelectorPanelPatch.cs` | 删除 | 已删除（迁入监听/服务） |
| `NoteBookProfilePannelPatch .cs` | 缺口 | 保留（Patches/Compat，编译通过） |
| `SaveManagementPatch.cs` | 缺口 | 本轮退役（已删除） |
| `SpecialGuestDescriberPatch.cs` | 删除 | 已删除（迁入监听/服务） |
| `UniversalGameManagerPatch.cs` | 已迁/升级 | 已删除（迁入监听/服务） |
| `CollabBehaviourComponentPatch.cs` | 升级 | 本轮退役（已删除） |
| `DaySceneChatSelectionPannel__c__DisplayClass17_0Patch.cs` | 待核对 | 本轮退役（已删除） |
| `DaySceneMapPatch.cs` | 升级/待核对 | 本轮退役（已删除） |
| `DaySceneMapProfilePatch.cs` | 缺口 | 已删除（数据面承担） |
| `DayScenePlayerInputPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `DaySceneShopPannelPatch.cs` | 缺口 | 本轮退役（已删除） |
| `DaySceneSustainedPannelPatch.cs` | 升级 | 本轮退役（已删除） |
| `DaySceneUIManagerPatch.cs` | 升级 | 本轮退役（已删除） |
| `StatusTrackerPatch.cs` | 删除 | 本轮退役（已删除） |
| `YuyukoExtraDialogData__c__DisplayClass4_0Patch.cs` | 缺口 | 保留（Patches/Compat，编译通过） |
| `GuestsManagerPatch.cs` | 缺口 | 已删除（迁入监听/服务） |
| `GuestsManager__c__DisplayClass174_0Patch.cs` | 升级 | 已删除（迁入监听/服务） |
| `GuestGroupControllerPatch.cs` | 缺口 | 已删除（迁入监听/服务） |
| `NormalGuestsControllerPatch.cs` | 缺口 | 本轮退役（已删除） |
| `SpecialGuestsControllerPatch.cs` | 缺口 | 本轮退役（已删除） |
| `SellablePatch.cs` | 缺口 | 本轮退役（已删除） |
| `CookControllerPatch.cs` | 升级 | 已删除（迁入监听/服务） |
| `CookSystemManagerPatch.cs` | 升级 | 已删除（迁入监听/服务） |
| `WorkSceneServePannelPatch.cs` | 升级 | 已删除（迁入监听/服务） |
| `WorkSceneStoragePannelPatch.cs` | 删除 | 已删除（迁入监听/服务） |
| `WorkSceneSustainedPannelPatch.cs` | 缺口 | 保留（Patches/Compat，编译通过） |
| `WorkSceneCookingSelectionPannelPatch.cs` | 缺口 | 本轮退役（已删除） |
| `GameTimeManagerPatch.cs` | 升级 | 已删除（迁入监听/服务） |
| `NightSceneEventManagerPatch.cs` | 缺口 | 本轮退役（已删除） |
| `QTERewardManagerPatch.cs` | 缺口 | 本轮退役（已删除） |
| `NightSceneDirectorPatch.cs` | 缺口 | 保留（Patches/Compat，编译通过） |
| `BuffPatch.cs` | 缺口 | 本轮退役（已删除） |
| `UIManagerPatch.cs` | 缺口 | 本轮退役（已删除） |
| `DataBaseCorePatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `DataBaseCharacterPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `DataBaseDayPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `DataBaseNightPatch.cs` | 升级 | 本轮退役（已删除） |
| `DataBaseSchedulerPatch.cs` | 升级 | 已删除（迁入监听/服务） |
| `DataBaseAchievementPatch.cs` | 删除 | 已删除（迁入监听/服务） |
| `DataBaseLanguagePatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `NightSceneLanguagePatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `DaySceneLanguagePatch.cs` | 升级 | 已删除（迁入监听/服务） |
| `IzakayaConfigPannelPatch.cs` | 已迁 | 已删除（迁入监听/服务） |
| `IzakayaConfigurePatch.cs` | 升级 | 已删除（迁入监听/服务） |
| `RunTimeAlbumPatch.cs` | 待核对 | 本轮退役（已删除） |
| `RunTimeDayScenePatch.cs` | 删除 | 已删除（商人链路承担） |
| `RunTimeSchedulerPatch.cs` | 缺口 | 本轮退役（已删除） |
| `TrackedMissionDataPatch.cs` | 待核对 | 本轮退役（已删除） |
| `YuyukoBossDataPatch.cs` 等 10 个幽幽子补丁 + `IncomeControllerYuyukoPatch.cs` | 缺口 | 保留（Patches/Compat，编译通过） |

## 3. 行为变更清单

1. **幽幽子挑战协同的编译排除已解除**：`YuyukoGuestSync.cs` / `YuyukoGuestSync.Challenge.cs` 及 12 个幽幽子缺口补丁已按 4.4.0e 发行版互操作成员布局适配，不再从编译中排除，`Managers/YuyukoGuestSync.Stub.cs` 不再需要（已删除）。
2. **上菜面板全部改走 `Mystia.Scenes.ServePannelView`**：`WorkSync` 不再持有 `WorkSceneServePannel`，`ServePanel` 改为缓存监听回调给出的视图（`ServePannelView?`）；`GuestFSM`/`Spell_Mai` 对「当前上菜面板」的判空、桌号、待上菜槽位与关闭动作一律经视图（`DeskCode`、`PendingFood`/`PendingBeverage`、`RefreshPendingVisual()`/`ResetPendingVisual()`、`Close()`）。`WorkSync` 的 `s_servePanelOpen`、面板属性 `ServePanel`、`ServePanelDeskCode` 一并移除。`Managers/GuestService.cs` 的面板动作全部经 `GuestFSM.TryCloseServePanel`，自身无面板直取，无需改动。
3. **已确认上菜的图标按「可取消」渲染**：原实现以 `SetServedVisualOnUI(..., canCancel: false)` 渲染已确认的菜；`ServePannelView` 没有「只渲染视觉、不占槽位」的入口，现改为「写槽位 → `RefreshPendingVisual()` → 清槽位」，槽位语义不变，仅图标上的可取消标志与原来不同（缺口台账 §3.1）。
4. **面板打开时的空槽会被显式清理**：原实现只渲染非空槽，现统一用 `RefreshPendingVisual()` 处理两个槽位。
5. **`PatchSkipPermit` 已删除**：全仓库已无消费者（`WorkSync` 用局部计数取代）。
6. Result 场景的 `GuestsManager.Initialize` 调用删除（原实现会在没有该组件的场景里凭空重建空壳管理器）。
7. 配置改为模组自有 JSON（`config.json`，落在框架提供的模组存储目录），不再写 BepInEx 的 ini；历史控制台命令记录同理由模组存储承载。
8. 日志改走框架 `ILog`（写入宿主 `host.log`），可见性与原 BepInEx/游戏控制台不同。
9. 控制台命令名与文案保留 `enable_bepin_console` 与「BepInEx 控制台」字样（不改对外行为），底层改为自建 `AllocConsole`。
10. 输入焦点：当前游戏构建不含 `GUI.SetNextControlName`/`GUI.FocusControl`，改为通过 `drawer.KeyboardControl` + `drawer.LastControlId` 聚焦输入框。
11. 白天结束（`OnDayOver`）改由场景循环 `Update` 内调用 `Schedule.End()`，比原同步调用晚至多一帧。
12. 备菜「开始营业」不再被拦截：`PrepOver` 改在关面板后补算菜谱，等待移交营业场景的 `BusinessStart`；主机最终表可能不再下发到已切场景的客户端。
13. 换图后的刷新改为监听 `OnDayMapEntered`，覆盖面小于原 `SwapMap` 回调（游戏自发的换图事件条件更窄）。
14. 服饰皮肤下标改为追加到已有 `dlcs` 之后（原为整组替换）；像素图集语义从「叠加模板」变为「整组帧」，且不再写头发/背部帧。
15. 三张映射表（`FoodsMapping`/`BeveragesMapping`/`RecipesMapping`）的值由硬编码 `"ResourceEx"` 改为注入模组自身的 id（`MetaMystia`）。
16. 资源包图片：包内 PNG 会解包到 `<模组目录>/ResourceExAssets/<包>/...`，数据面使用相对路径；非 PNG 图片跳过并记警告。
17. 离场通知改为按「最外层离场方法」去重：嵌套离场（`PatientDepletedLeave → PayAndLeave → LeaveFromDesk`）只派发一次且取最外层语义；模组侧的 `Managers/GuestReentryPermits.cs`（三个令牌）整类删除，「模组自发离场不广播」改由 `GuestSync.LeaveBroadcastSuspended` 布尔旁路表达。

## 4. 与计划的偏差

1. **编译用互操作改为游戏自带的 BepInEx interop**（计划原本要求用 `Mystia.InteropGen` 从游戏工程源码生成）。原因：由源码生成的互操作使用 Roslyn 名字（`<>c__DisplayClass16_6`、`<…>d`），含非法 C# 标识符，导致 45+ 处既有引用（含缺口补丁与幽幽子 Manager）无法编译；而发行版是混淆过的，现有代码与框架桥接的字符串名都是按发行版命名写的。切换后框架与模组均编译通过，且与运行时真实成员名一致。源码生成的互操作保留在 `artifacts/interop-generated/` 以备对照。
2. 框架侧为此补了一处：`DatabaseAsInject` 里 `NPC.Destination.switchConditionLabel` 在发行版中不存在，已按发行版字段写入（`Switch` 字段暂无目标列）。
3. `conventions.md`/`mefx-handoff.md` 原写「`cancelInvocation = true` 表示不取消」，与桥接实现（`return !cancel`）相反；实现与测试以 `true = 取消原版调用` 为准。
4. **WV（面板 → 代理视图）只完成了可表达的部分**：`WorkSync` 的四个 serve-panel 成员与其消费者已改用 `ServePannelView`；`PrepSync` 与 `WorkSync.RefreshStoragePanel` 未改，原因是 SDK 侧视图不足以表达既有用法（缺口台账 §3.1 逐条列出）。
5. **WV 在框架侧尚未落地**：`IWorkListener`/`IPrepListener` 仍以游戏面板为形参，桥接仍派发面板，因此 `WorkSync` 的视图签名方法当前不构成接口实现（编译通过、派发落到接口默认空实现）。必须由框架侧先替换签名、改桥接为构造并派发视图、并重打包 SDK 后，本节第 2/4 条才会生效。
6. **W7 的删除项按「逐项确认」保留**：`Patches/PatchBypassToken.cs`（仍被 `RunTimeSchedulerGapsPatch` 使用）与 `Patches/HarmonyPrefixFlow.cs`（被 §1 的 11 个兼容补丁使用）未删除；其中已无消费者的 `PatchSkipPermit` 子类已删除。`Managers/GuestReentryPermits.cs` 由 W1–W3 批次删除（离场 seam 已取代）。`Patches/Compat/` 未发现能力已覆盖却仍留存的死文件。
7. **`CompatPatches.Applied` 的消费者未改**：该语义切换的前提是「Compat 清空」，而本轮退役后 `Patches/Compat/` 仍有 16 个类（幽幽子族 11 + 控制器 2 + 面板/立绘 2 + 日程缺口 1），`CompatPatches.ApplyAll()` 与 `Applied` 仍是有效门槛，故 `SceneFlow.cs`、`ModEntry.cs`、`GameSession.cs`、`MultiplayerStatus.cs` 四处保持原样。

## 5. 第二阶段（本轮补完）

1. **5 项框架能力缺口已补齐并已被模组消费**：可取消的刷客钩子（含 `GuestSpawnRequest` 生成参数）、`OnGroupSpawned(group, request)`、`OnPrePlayerRepel`、备菜 `SetCompleteEnabled`、`SetEvaluationEnabled`。模组侧据此恢复了：客机方法级刷客短路、生成参数广播、客机赶客请求、备菜「开始营业」原语义、客机评价硬门控。
2. **重放改走服务**：`GuestFSM`/`GuestService`/消息处理不再直接调用被开关拦住的游戏方法，改为在营业场景循环 `Update`（服务作用域内）经 `IWorkSceneGuests.Leave/Seat/BeginOrderSession/Evaluate` 与 `IWorkSceneIzakaya.Close` 执行重放。
3. **数据注入迁移完成**：E2（特殊客人、对话）、E3（商人、白天地图）、E4（任务/事件节点、符卡数据、服装像素集、运行时立绘提供者）落地，对应旧补丁删除；商人链路、已拥有食谱过滤、缺键安全、三张映射表均由框架承担。
4. 桥接层修掉三处由迁移反馈暴露的问题：离场通知的嵌套重复派发、无参刷客路径的重复派发、示例模组的 `OnGroupSpawned` 签名。
5. 同时修掉两处互操作缺陷与一处存档兼容问题：结构体参数错写（改走原生 `set_Item` + 拆箱）、NPC 反向标签表只写单元素，以及 `SchedulerDataRecovery` 对新映射标签（模组 id）的识别。

## 6. 仍未完成的工作

1. **框架侧 WV**：替换 `IWorkListener` 的 7 个 serve-panel 成员与 `IPrepListener` 的 4 个 prep-panel 成员的签名为对应视图，桥接在 `WorkSceneServePannel.OnPanelOpen`、`WorkSceneStoragePannel.OnPanelOpen`、`IzakayaSelectorPanel_New`、`IzakayaConfigPannel.GoToSpecific` 处构造并派发视图，随后重打包 SDK；`GuideMapView` 需补地图点回读、`PrepConfigView` 需补稳定实例与原面板回读、`ServePannelView` 需补「只渲染视觉」入口，`StoragePannelView` 需补投递成员。
2. 模组侧待框架落地后收口：`PrepSync.OnGuideMapConfirmed`/`OnConfigTabSelected`/`OnPrepConfirmed`、`WorkSync.RefreshStoragePanel`、`PrepSync.ConfigPanel` 的视图化。
3. `Patches/Compat/` 中仍有 16 个游戏专有缺口补丁（见缺口台账 §1），等待中间件补足对应回调。
4. 演练/实测：全部结论仍为源码与编译级推断，未在游戏中验证。


---

## 7. 第三阶段（W1–W7 / WV / WY）最终状态

- 中间件：新增 8 组监听接口（`ISessionListener`/`IStatusListener`/`IMissionListener`/`IDayUiListener`/`IWorkUiListener`/`IWorkMetricsListener`/`IQteListener`/`IScheduleListener`/`IChatOptionListener`/`IChatMenuProvider`/`ICookSelectionListener`/`Mystia.Spells.ISpell`）+ 5 个服务面（`IWorkSceneEconomyServices`/`IQteServices`/`IWorkSceneBuffs`/`ISpellHost`/`IPlatformInfo`）+ 5 个视图类；桥接新增约 20 个文件（seam 与管线），`BridgeSpell` 改为驱动模组 `ISpell`（每恢复步包裹作用域），点位管线接管 `DaySceneMapPatch`。
- 模组：新增 11 个监听实现，删除 Compat 补丁 24 个 + `SpawnMarkerRegistry`/`SpellBaseEx`/`GuestReentryPermits`/`PatchSkipPermit`；`Spell_Mai` 改为纯托管 `ISpell`；消息层改走服务排队。
- 幽幽子族：13 条编译排除全部解除，`Stub` 删除，协议分支恢复。
- 最终验收：框架 0 错 / SDK 21-21；模组 0 错；服务端 0 错；`Network.Tests` 214（高负载下偶发竞态，重跑通过）；`Flow.Tests` 103；静态检查 8/8。
- 仍留兼容补丁 16 个（见缺口台账 §4），其中 12 个为幽幽子挑战（游戏专有、需协程状态机级钩子）。

## 8. 如何从中断处继续

### 当前分支与状态

- 模组仓库分支：`mystia-extension-fx-port`；中间件仓库：`main`（本轮改动均在各自工作树内已提交）。
- 验收基线（本机实测）：框架 0 错 + `Mystia.Net.Sdk.Tests` 21/21；模组 0 错；服务端 0 错；`Network.Tests` 214（高负载下偶发竞态，重跑通过）；`Flow.Tests` 103；静态检查 8/8。

### 环境搭建（换机后先做这四步）

1. 两个仓库放在**同级目录**（`nuget.config` 用相对路径 `../MystiaExtensionFramework/artifacts/nuget` 指向本地 SDK 源）。
2. 复制 `MetaMystia.local.props.example` 为 `MetaMystia.local.props`，至少填两项：
   - `BepInExPath`：本机游戏安装目录下的 `BepInEx`（旧工程与测试工程仍用它取互操作）；
   - `MystiaInteropDir`：中间件互操作目录（见第 3 步）。
3. 生成互操作并把 SDK 打包到本地源：
   ```text
   cd <MystiaExtensionFramework>
   dotnet run --project src/Mystia.InteropGen -- <游戏工程目录> <游戏安装目录>
   dotnet pack sdk/Mystia.Extension.Sdk/Mystia.Extension.Sdk.Pack.csproj -c Release
   ```
   互操作产物落在 `artifacts/interop/`（**该目录被 gitignore，不入库**）。当前本机使用的是游戏自带 BepInEx 互操作（发行版混淆命名），`artifacts/interop-generated/` 保留着由源码工程生成的版本备查。
4. 首次构建前清一次 SDK 包缓存（同版本号覆盖时必需）：`rm -rf ~/.nuget/packages/mystia.extension.sdk`。

### 常用命令

```text
# 中间件
dotnet build MystiaExtensionFramework.slnx -c Debug
dotnet test  src/Mystia.Net.Sdk.Tests/Mystia.Net.Sdk.Tests.csproj -c Debug

# 模组
dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Debug
dotnet build src/MetaMystia.Server/MetaMystia.Server.csproj -c Debug
dotnet run   --project src/MetaMystia.Network.Tests -c Debug
dotnet run   --project src/MetaMystia.Flow.Tests -c Debug
bash docs/port/static-check.sh          # 迁移静态检查 8 项
```

### 剩余工作

1. **16 个兼容补丁**（见 `mystia-extension-port-gaps.md` §4）：幽幽子族 12、`NightSceneDirectorPatch`、`NoteBookProfilePannelPatch`（待定去留）、`RunTimeSchedulerGapsPatch`（奖励拦截 + 灵梦保护窗口）、`WorkSceneSustainedPannelPatch`（按订单包装回调 + 营业内快进）。
2. **框架侧待补**：`PlayVfx`/`PlayAudio` 的真实实现（需模组级资源路径上下文 + 预制件/音频加载管线）；符卡宣言立绘 pivot 的消费；`SceneLoops.cs` 中 16 个脚手架默认体（需允许改 `SceneServices.cs` 后清理）。
3. **游戏内实测清单**：幽幽子 10 项（见缺口台账 §2 与 `yuyuko-challenge-sync.md`）、离场去重、备菜共识链路、聊天菜单入口、选菜提交拦截、符卡驱动与作用域包裹、点位注入。
4. 未决设计：`IWorkMetricsListener` 后缀不带运算类型；`EventManager.Fever` 仅在 5 个奖励方法路径上覆盖；`ServeBeverage` 为同步落菜（无投掷动画与飞行中复查）。
