# 审计事实

读者：接手迁移的执行者。本文是「唯一事实来源」，工单里的判断都以本文的源码事实为依据。所有行号来自实际读取；未在游戏内验证的一律属「源码推断」。

- 逐补丁事实与归宿：见第 2 节。
- 中间件已有桥接面（seam、开关、数据写入、残留反射）：见第 3 节。
- 数据面字段对照（ResourceEx → 游戏表 → 框架代理）：见第 4 节。
- 未决与待核对项：见第 5 节。

## 1. 机制事实

### 1.1 场景循环与作用域

- 桥接用 `SceneLoopHost` 管理当前场景循环（`src/Mystia.Modding.Bridge/Game/SceneLoopHost.cs`）：`Enter(scene)` 先对上一个场景触发 `Shutdown`，再 `StockGate.Reset(scene)`，然后 `Setup`；`Tick(delta)` 触发 `Update`；同一场景重复 `Enter` 直接返回。
- 服务只在 `Setup`/`Update`/`Shutdown` 内有效：`ServiceScope` 是 `[ThreadStatic]` 深度计数，`Require()` 在深度为 0 时抛 `InvalidOperationException`（`SceneLoopHost.cs:8-24`）。
- 常驻宿主组件 `MainThreadPump`（`src/Mystia.Modding.Bridge/Game/RuntimeInstall.cs`）是 `DontDestroyOnLoad` 的注入 MonoBehaviour，其 `Update` 先 drain 主线程队列，再 `SceneLoopHost.Tick(Time.deltaTime)`。全局循环与协程泵应挂在这里。
- 场景事件与 `SceneId` 的对应：`Splash` 用 `Awake`/`Start`，`Main`、`Day` 用 `Awake`，`PrepNight`、`Night`、`Staff`、`Result` 用 `Start`（`Game/HarmonySeams.cs` 的 `SceneSeams`）。`SceneId.Night` 即营业场景。

### 1.2 放行与开关

- `StockGate` 保存一组每场景重置的布尔开关，并提供 `Allow(open)` 与 `Bypass(Action)`：`Bypass` 用 `[ThreadStatic]` 计数，凡经服务再次调用游戏方法时自动放行，因此模组自己发起的动作不会被自己的开关拦住。
- 各场景 `Reset` 的开关：`Day` → 白天结束、移动、冲刺、互动、夜转场；`PrepNight` → 地图确认；`Night` → 离场、订单、托盘关闭。新增开关必须同步加入对应场景分支。
- 已存在的「离场」开关覆盖全部离场路径：`PayAndLeave`、`ExBadLeave`、`RepellAndLeavePay`、`RepellAndLeaveNoPay`、`PlayerRepell`、`PatientDepletedLeave`、`LeaveFromDesk`（`StockHolds.cs` 的 `LeaveHolds`）。「订单」开关覆盖 `GenerateOrderSession` 与 `GenerateOrder`。「托盘关闭」开关在 `WorkSceneServePannel.OnPanelClose` 上，并会把面板里的待上菜对象退回托盘。
- 结论：不要再为「赶客」「耐心耗尽离店」「订单循环」新增开关，它们已被覆盖；`MainOrderCycle` 只是转调 `GenerateOrderSession`。

### 1.3 互操作与可见性

- 项目引用的是 Il2CppInterop 生成的壳代码，游戏成员（含 `private`）统一以 `public` 形式暴露；因此模组可以直接读写 `WorkSceneServePannel` 的待上菜字段、调用它的私有刷新方法，不需要反射，也不需要框架再造视图类型。
- 禁止对游戏对象使用反射；桥接内部若仍有 `AccessTools` 用法，属待清理项（见第 3 节）。

### 1.4 注册与顺序

- 每个实现类一个实例，`[AutoWire]` 生成器发出 `ModEntrance.Register`；`ModLoader` 按启动器 `modOrder` 顺序加载并注册，`Dispatch.Run<T>` 按注册顺序调用各实现——这就是「按玩家顺序跑管线」的实现。
- `IPostInitialize` 在每个模组注册完成后调用一次，拿到该模组的 `IModContext`（日志、主线程、路径、组件宿主、精灵加载）。

## 2. 逐补丁事实与归宿

口径：

- 行号 = 该补丁方法上方方法级 `[HarmonyPatch(...)]` 特性的行号；文件路径相对 `src/MetaMystia.Mod/Patches/`。
- 「游戏侧」列的行号相对游戏工程根（即 `Assets/Scripts/...`）；标「编译器生成」的目标在游戏源码中不存在同名成员，只能给到构造它的源码位置。
- 归宿取值：**已迁**（写出目标接口/服务）、**删除**（无行为或由框架取代）、**升级**（需要中间件新增能力，见 `mefx-handoff.md`）、**缺口**（保留兼容文件，登记进缺口清单）、**待核对**（见第 5 节）。
- 抽取口径下 Prefix 返回 false 计为 Skip；改写参数/字段/结果为 Rewrite；跳过并接管为 Hijack；其余为 Run（原方法照常执行）。

### 2.1 场景与白天（24 文件 / 42 补丁方法 / 3 个反向补丁）

| 文件:行 | 目标方法（游戏侧） | 种类 | 归宿 |
| --- | --- | --- | --- |
| SceneManager/MainSceneManagerPatch.cs:21 | `MainScene.SceneManager.Awake`（`Main/SceneManager.cs:36`） | Postfix | 已迁：`ISceneListener.OnSceneAwake(Main)`；版本比对与上报归 `IPostInitialize` |
| SceneManager/DaySceneManagerPatch.cs:23 | `DayScene.SceneManager.Awake`（`Day/SceneManager.cs:78`） | Prefix | 已迁：`OnSceneAwake(Day)` 的进入前重置 |
| SceneManager/DaySceneManagerPatch.cs:32 | 同上 | Postfix | 已迁：`OnSceneAwake(Day)` 的进入后刷新 |
| SceneManager/DaySceneManagerPatch.cs:62 | `DayScene.SceneManager.OnFirstEnterDaySceneFinish`（`Day/SceneManager.cs:108`） | Postfix | 升级：`IDayListener.OnDayFirstEntered()` |
| SceneManager/DaySceneManagerPatch.cs:69 | `DayScene.SceneManager.OnDayOver`（`Day/SceneManager.cs:335`） | Prefix | 已迁：`IDaySceneScheduleServices.SetEndEnabled(false)` + `End()` |
| SceneManager/DaySceneManagerPatch.cs:90 | 同上 | ReversePatch | 删除（改走服务） |
| SceneManager/DaySceneManagerPatch.cs:95 | `DayScene.SceneManager.SwapMap`（`Day/SceneManager.cs:409`） | Prefix | 升级：`IDaySceneMapServices.Swap(..., onFinished)` |
| SceneManager/NightSceneManagerPatch.cs:18 | `NightScene.SceneManager.Dispose`（`Night/SceneManager.cs:424`） | Prefix | 已迁：清理改到 `IWorkSceneGameLoop.Shutdown`（幽幽子状态复位属缺口部分） |
| SceneManager/NightSceneManagerPatch.cs:22 | `NightScene.SceneManager.Start`（`Night/SceneManager.cs:104`） | Postfix | 已迁：`OnSceneStart(Night)` |
| SceneManager/PrepNightSceneManager.cs:14 | `PrepNightScene.SceneManager.Start`（`PrepNight/SceneManager.cs:25`） | Postfix | 已迁：`OnSceneStart(PrepNight)` |
| SceneManager/ResultSceneManagerPatch.cs:14 | `ResultScene.SceneManager.Start`（`Result/SceneManager.cs:39`） | Postfix | 已迁：`OnSceneStart(Result)`；`GuestsManager.Initialize` 调用**删除**（行为变更） |
| SceneManager/StaffSceneManagerPatch.cs:14 | `StaffScene.SceneManager.Start`（`Staff/SceneManager.cs:107`） | Postfix | 已迁：`OnSceneStart(Staff)` |
| Launch/DesktopPlatformProfilePatch.cs:17 | `SteamPlatformProfile.GetActiveKeys`（包内源码 `com.omt.gameplatformadapter/Runtime/Profiles/SteamPlatformProfile.cs:21`，不在 Assets/Scripts） | Postfix | 缺口 |
| Common/CharacterControllerInputGeneratorComponentPatch.cs:19 | `CharacterControllerInputGeneratorComponent.UpdateInputDirection`（`Common/Character/.../CharacterControllerInputGeneratorComponent.cs:70`） | Prefix | 升级：`IDayInputListener.OnMoveInput(unit, direction)`（需带来源实例） |
| Common/DialogPannelPatch.cs:19 | `DialogPannel.GetSpeakerVisual`（`_Pannels/Shared/DialogPannel.Main.cs:497`） | Prefix（Skip+Rewrite） | 删除：立绘改由特殊客人数据注入（框架负责挂载） |
| Common/IzakayaSelectorPanelPatch.cs:26 | `IzakayaSelectorPanel_New.OnGuideMapInitialize`（`_Pannels/DayScene/IzakayaSelectorPanel_New.cs:61`） | Prefix | 删除：面板实例由框架传递，模组不再缓存 |
| Common/IzakayaSelectorPanelPatch.cs:35 | `IzakayaSelectorPanel_New._OnGuideMapInitialize_b__21_0`（同文件闭包） | Prefix（Skip） | 已迁：`IPrepNightMapServices.SetConfirmEnabled(false)` + `Confirm(...)` |
| Common/IzakayaSelectorPanelPatch.cs:149 | 同上 | ReversePatch | 删除（改走服务） |
| Common/IzakayaSelectorPanelPatch.cs:154 | `IzakayaSelectorPanel_New.OnGuideMapSpotSelected`（同文件 `:322`） | Prefix | 已迁：`IPrepListener.OnGuideSpotSelected` |
| Common/NoteBookProfilePannelPatch .cs:18 | `NoteBookProfilePannel.OnPanelOpen`（`_Pannels/Shared/NoteBook_New/NoteBookProfilePannel.cs:66`） | Postfix（Rewrite） | 待核对：笔记本立绘是否可完全由 `IPortraitProvider` 覆盖（该面板同样调用 `SetupPortrayalVisual`）；不能则缺口 |
| Common/SaveManagementPatch.cs:13 / :17 / :21 | `SaveManagement.LoadPlayerData` / `DisposeGameStatusAndBackToMainMenu` / `DisposeGameStatusAndRewindDay`（`Common/Utils/SaveManagement.cs:382/324/336`） | Prefix ×3 | 缺口（读档与回退前清理） |
| Common/SpecialGuestDescriberPatch.cs:18 | `SpecialGuestDescriber.Describe`（`Common/Utils/SpecialGuestDescriber.cs:215`） | Prefix（Rewrite token） | 删除：描述与立绘走数据注入 |
| Common/SpecialGuestDescriberPatch.cs:28 | 同上 | Postfix（Rewrite） | 删除：同上 |
| Common/UniversalGameManagerPatch.cs:23 | `UniversalGameManager.OpenDialogMenu`（`Common/UniversalGameManager.cs:511`） | Prefix（Rewrite+Skip） | 已迁/升级：`IDayListener.OnDialogOpened(package)` + `ICommonServices.SetNightTransitionEnabled` + `Dialogs.TryResolve` 回填空 `dialogContext` |
| Common/UniversalGameManagerPatch.cs:78 | `UniversalGameManager.LoadScene`（`Common/UniversalGameManager.cs:396`） | Prefix | 已迁：`IDayListener.OnSceneChanging` / `ICommonServices.LoadScene` |
| DayScene/CollabBehaviourComponentPatch.cs:15 | `CollabBehaviourComponent.OnInteract`（`Day/.../CollabBehaviourComponent.cs:52`） | Prefix（Rewrite） | 升级：故事回放入口改走聊天菜单扩展（形态待定，见第 5 节） |
| DayScene/DaySceneChatSelectionPannel__c__DisplayClass17_0Patch.cs:19 | `DaySceneChatSelectionPannel.__c__DisplayClass17_0.*_PDM_0`（编译器生成；`_Pannels/DayScene/DaySceneChatSelectionPannel.cs:226` 闭包） | Postfix（Rewrite availability） | 待核对：商人链路完备后应可删；否则缺口 |
| DayScene/DaySceneChatSelectionPannel__c__DisplayClass17_0Patch.cs:34 | 同上 `*_PDM_1` | Postfix（Rewrite availability） | 待核对：同上 |
| DayScene/DaySceneMapPatch.cs:28 | `DaySceneMap.GenerateSpawnMarkerData`（`Day/DaySceneMap.cs:119`） | Postfix | 升级/待核对：点位登记改由地图与 NPC 注入表达 |
| DayScene/DaySceneMapPatch.cs:35 | `DaySceneMap.SolveAndUpdateCharacterPositionInternal`（`Day/DaySceneMap.cs:182`） | Prefix（Hijack） | 待核对：能否完全由 Place 数据表达；不能则缺口 |
| DayScene/DaySceneMapProfilePatch.cs:13 | `DaySceneMapProfile..ctor`（`Common/DataBaseProfiles/DaySceneMapProfile.cs:12`，无显式构造器） | ctor-Postfix | 缺口 |
| DayScene/DayScenePlayerInputPatch.cs:18 | `DayScenePlayerInputGenerator.OnSprintPerformed`（`Day/DayScenePlayerInputGenerator.cs:156`） | Prefix（Skip） | 已迁：`IDaySceneInputServices.SetSprintEnabled(false)` |
| DayScene/DayScenePlayerInputPatch.cs:31 | `DayScenePlayerInputGenerator.OnSprintCanceled`（同文件 `:154`） | Prefix | 已迁：`IDayInputListener.OnSprintStopped` |
| DayScene/DayScenePlayerInputPatch.cs:39 | `DayScenePlayerInputGenerator.TryInteract`（同文件 `:227`） | Prefix（Skip） | 已迁：`SetInteractEnabled(false)` + `OnInteracted` |
| DayScene/DaySceneShopPannelPatch.cs:14 | `DaySceneShopPannel.OnPanelOpen`（`_Pannels/DayScene/DaySceneShopPannel.cs:129`） | Postfix | 缺口 |
| DayScene/DaySceneSustainedPannelPatch.cs:18 | `DaySceneSustainedPannel.OnFastForwardSubmit`（`_Pannels/DayScene/DaySceneSustainedPannel.cs:166`） | Prefix（Skip） | 升级：`OnPreDayFastForward`（形态待定，见第 5 节） |
| DayScene/DaySceneUIManagerPatch.cs:27 | `DayScene.UI.UIManager.OpenAfterChatMenu`（`Day/UIManager.cs:266`） | Prefix（Rewrite callbacks） | 升级：聊天菜单扩展（形态待定，见第 5 节） |
| DayScene/StatusTrackerPatch.cs:14 | `StatusTracker.RecordInvitedGuest`（`Common/GameData/DataBase/GlobalCollections/StatusTracker.cs:138`） | Postfix | 删除（原方法仅打日志） |
| DayScene/StatusTrackerPatch.cs:21 | 同上 | ReversePatch | 删除：改走 `IGuestRecords.RecordInvited` |
| DayScene/YuyukoExtraDialogData__c__DisplayClass4_0Patch.cs:21 | `YuyukoExtraDialogData.__c__DisplayClass4_0._Yuyuko_Challenge_b__2`（编译器生成） | Prefix（Skip） | 缺口 |

### 2.2 营业（19 文件 / 92 补丁方法 / 14 个反向补丁）

| 文件:行 | 目标方法（游戏侧） | 种类 | 归宿 |
| --- | --- | --- | --- |
| NightScene/GuestsManagerPatch.cs:66 / :80 / :86 / :91 | `GuestsManager.SetManualControllerOrderInternal` / `EvaulateManualOrder` / `CleanOrderInfo` / `SetManualControlledLeave`（`Night/GuestSystem/GuestsManager.cs:1441/2050/1502/1513`） | Prefix/Postfix | 缺口（幽幽子手动受控链路） |
| NightScene/GuestsManagerPatch.cs:190 / :243 / :291 | `GuestsManager.SpawnNormalGuestGroup` 无参与带参重载（`GuestsManager.cs:1150/1199`） | Prefix/Skip + Postfix | 升级：`IWorkSceneGuests.SetSpawnEnabled(false)` + `IGuestSpawnModifier.OnNormalGuestsGenerating` + `SpawnNormal(...)`；主动重抽不再需要暂存静态参数 |
| NightScene/GuestsManagerPatch.cs:311 / :351 | `GuestsManager.SpawnSpecialGuestGroup`（`GuestsManager.cs:1233`） | Prefix/Skip + Postfix | 升级：`IGuestSpawnModifier.OnSpecialGuestGenerating(ref guestId)` + `SpawnSpecial(...)` |
| NightScene/GuestsManagerPatch.cs:393 | `GuestsManager.PostInitializeGuestGroup`（`GuestsManager.cs:1284`） | Prefix | 已迁：`IGuestGroupListener.OnGroupSpawned(group)` |
| NightScene/GuestsManagerPatch.cs:418 | `GuestsManager.PlayerRepell`（`GuestsManager.cs:2898`） | Prefix/Skip | 已迁：`IWorkSceneGuests.SetLeaveEnabled(false)` + `OnGroupLeft(PlayerRepelled)` |
| NightScene/GuestsManagerPatch.cs:442 | `GuestsManager.EvaluateOrder`（`GuestsManager.cs:1803`） | Prefix/Skip | 待核对：客机「无主机评价结果时不本地评价」需要门控；优先复用框架的评价覆盖机制（`OnGroupEvaluated` + `IWorkSceneGuests.Evaluate`），确认不足再考虑新增开关（见第 5 节） |
| NightScene/GuestsManagerPatch.cs:468 | 同上 | Postfix | 已迁：评价后 FSM 推进由模组状态机自行处理（通知 `OnGroupEvaluated` 已可用） |
| NightScene/GuestsManagerPatch.cs:500 | `GuestsManager.RepellInternal`（`GuestsManager.cs:2985`） | Prefix | 已迁：主机同步走 `OnGroupLeft(Repelled*)`；不再需要保留旁路令牌 |
| NightScene/GuestsManagerPatch.cs:526 | `GuestsManager.TrySendToSeat`（`GuestsManager.cs:1035`） | Prefix/Skip | 升级：`IWorkSceneGuests.SetSeatingEnabled(false)` |
| NightScene/GuestsManagerPatch.cs:547 | `GuestsManager.GenerateOrderSession`（`GuestsManager.cs:1545`） | Prefix/Skip | 已迁/升级：`SetOrderingEnabled(false)` + 重放走 `IWorkSceneGuests.BeginOrderSession` |
| NightScene/GuestsManagerPatch.cs:571 | `GuestsManager.MainOrderCycle`（`GuestsManager.cs:1709`） | Prefix/Skip | 删除：该原版方法只转调 `GenerateOrderSession`，`SetOrderingEnabled` 已覆盖 |
| NightScene/GuestsManagerPatch.cs:593 | `GuestsManager.CheckAndSendFromQueue`（`GuestsManager.cs:2704`） | Prefix/Skip+Hijack | 升级：`SetSeatingEnabled(false)` + `OnGroupSeated` 通知（原补丁的劫持实现可删） |
| NightScene/GuestsManagerPatch.cs:618 | `GuestsManager.Method_Private_Void_GuestGroupController_PDM_1`（编译器生成；推定 `GuestsManager.cs:1312` 局部函数 `OnPatiendDepleted`） | Prefix/Skip | 缺口（队内耐心耗尽） |
| NightScene/GuestsManagerPatch.cs:640 / :665 | `GuestsManager.TryCloseIzakaya`（`GuestsManager.cs:641`） | Prefix/Skip + ReversePatch | 升级：`IWorkSceneIzakaya.SetCloseEnabled(false)`；主动关店走 `IWorkSceneIzakaya.Close()`，反向补丁删除 |
| NightScene/GuestsManagerPatch.cs:670 | `GuestsManager.AddToPatientCountdown`（`GuestsManager.cs:2288`） | Postfix | 缺口（幽幽子三阶段耐心×3） |
| NightScene/GuestsManagerPatch.cs:696 | `GuestsManager.PatientDepletedLeave`（`GuestsManager.cs:2824`） | Prefix/Skip | 已迁：`SetLeaveEnabled(false)` + `OnGroupLeft(Patience)` |
| NightScene/GuestsManagerPatch.cs:729 / :771 | `GuestsManager.LeaveFromDesk`（`GuestsManager.cs:2623`） | Prefix/Skip + ReversePatch | 已迁：`SetLeaveEnabled(false)` + `IWorkSceneGuests.Leave(...)`，反向补丁删除 |
| NightScene/GuestsManager__c__DisplayClass174_0Patch.cs:28 / :58 / :73 | `GuestsManager.__c__DisplayClass174_0`（编译器生成；`GuestsManager.cs:1583` `GenerateOrderInternal`、`:1551` `CheckRemainingFund`） | Prefix/Skip + Postfix ×2 | 升级：`IGuestGroupListener.OnGroupOrderGenerated(group, result, ref order)` + `IWorkSceneGuests.BeginOrderSession(group, result, order)` |
| NightScene/GuestGroupControllerPatch.cs:33 | `GuestGroupController.Evaluate`（`Night/GuestSystem/GuestGroupController.cs:442`） | Prefix/Skip | 缺口（幽幽子评价覆写） |
| NightScene/GuestGroupControllerPatch.cs:43 | `GuestGroupController.RefreshCurrentFundAndOrder`（`:749`） | Prefix | 升级：`OnGroupArrived(group)` |
| NightScene/GuestGroupControllerPatch.cs:61 | `GuestGroupController.MoveToDesk`（`:945`） | Prefix | 升级：`OnGroupMovingToDesk(group, desk)` |
| NightScene/GuestGroupControllerPatch.cs:78 | `GuestGroupController.MoveToQueue`（`:978`） | Postfix | 升级：`OnGroupQueued(group)` |
| NightScene/GuestGroupControllerPatch.cs:97 | `GuestGroupController.TryOverrideEvaluateByBuff`（`:719`） | Postfix | 已迁：`OnGroupEvaluated(ref result)` |
| NightScene/NormalGuestsControllerPatch.cs:19 | `NormalGuestsController.PostEvaluation`（`Night/GuestSystem/NormalGuestsController.cs:316`） | Postfix | 缺口（评价后回调无对应挂点） |
| NightScene/SpecialGuestsControllerPatch.cs:16 / :27 | `SpecialGuestsController.PostEvaluation`（`Night/GuestSystem/SpecialGuestsController.cs:498`） | Prefix + Postfix | 缺口（前者为幽幽子评价前处理） |
| NightScene/SellablePatch.cs:16 | `Sellable.GetPopTag`（`Common/GameData/DataBase/GlobalCollections/Sellable.cs:77`） | Prefix/Skip | 缺口（每次读标签都走的高频私有方法，且改的是标签计算而非待送出对象） |
| NightScene/CookControllerPatch.cs:21 / :37 / :42 | `CookController.SetCook`（`Night/CookSystem/CookController.cs:263`） | Prefix/Skip + ReversePatch + Postfix | 升级：`OnPreCookStarted(controller, ref result, ref recipe, ref cancelInvocation)` + `ICookListener.OnCookStarted`；反向补丁删除 |
| NightScene/CookControllerPatch.cs:54 / :59 | `CookController.Extract`（`:474`） | ReversePatch + Prefix | 已迁：`ICookListener.OnCookExtracted`；主动取出走 `IWorkSceneCook.Extract` |
| NightScene/CookControllerPatch.cs:70 / :75 | `CookController.Store`（`:457`） | ReversePatch + Prefix | 已迁：`ICookListener.OnCookStored`；主动存放走 `IWorkSceneCook.Store` |
| NightScene/CookControllerPatch.cs:85 / :90 | `CookController.StartCookCountDown`（`:351`） | ReversePatch + Prefix/Skip | 升级：`OnPreCookCountdownStarted(controller, ref qteScore, ref cancelInvocation)`；倒计时通知走 `ICookListener.OnCookCountdownStarted` |
| NightScene/CookSystemManagerPatch.cs:16 | `CookSystemManager.CallCooker`（`Night/CookSystem/CookSystemManager.cs:201`） | Prefix/Skip | 升级：`IWorkSceneCook.SetCallEnabled(false)` |
| NightScene/WorkSceneServePannelPatch.cs:33 | `WorkSceneServePannel.OnPanelOpen`（`_Pannels/WorkScene/WorkSceneServePannel.cs:79`） | Postfix | 升级：`IWorkListener.OnServePanelOpened(panel)`（待上菜字段由模组直接读写游戏对象） |
| NightScene/WorkSceneServePannelPatch.cs:71 | `WorkSceneServePannel.OnPanelClose`（`:228`） | Prefix | 升级：`OnPreServePanelClosed(panel, ref cancelInvocation)`（必须在原版结算前） |
| NightScene/WorkSceneServePannelPatch.cs:96 | `WorkSceneServePannel.Send`（`:396`） | Prefix/Skip | 升级：`OnPreDishServed(panel, ref dish, ref cancelInvocation)` |
| NightScene/WorkSceneServePannelPatch.cs:125 | `WorkSceneServePannel.Cancel`（`:430`） | Prefix/Skip | 升级：`OnPreDishCancelled(panel, ref dish, ref cancelInvocation)` |
| NightScene/WorkSceneStoragePannelPatch.cs:22 / :29 | `WorkSceneStoragePannel.OnPanelOpen` / `OnPanelClose`（`_Pannels/WorkScene/WorkSceneStoragePannel.cs:159/504`） | Postfix + Prefix | 删除（原补丁只缓存实例） |
| NightScene/WorkSceneStoragePannelPatch.cs:37 | `WorkSceneStoragePannel.Extract`（`:467`） | Prefix/Skip | 升级：`OnPreStorageExtracted(ref sellable, ref cancelInvocation)`；`IWorkListener.OnStorageExtracted` 保持通知 |
| NightScene/WorkSceneSustainedPannelPatch.cs:24 | `WorkSceneSustainedPannel.OpenServePanel`（`_Pannels/WorkScene/WorkSceneSustainedPannel.cs:296`） | Prefix | 缺口（包裹 8 参回调以限定当前订单；参数与源码签名不一致，见第 5 节） |
| NightScene/WorkSceneSustainedPannelPatch.cs:62 | `WorkSceneSustainedPannel.OnFastForwardSubmit`（`:178`） | Prefix/Skip | 缺口（营业内快进；与白天快进是不同方法） |
| NightScene/WorkSceneCookingSelectionPannelPatch.cs:23 | `WorkSceneCookingSelectionPannel.__c__DisplayClass79_0.Method_Internal_Void_PDM_0`（编译器生成；`_Pannels/WorkScene/WorkSceneCookingSelectionPannel.cs:662` 局部函数 `OnSubmit`） | Prefix/Skip | 缺口 |
| NightScene/GameTimeManagerPatch.cs:10 | `GameTimeManager.SetGameTimeMode`（`Common/GameTimeManager.cs:52`） | Prefix/Rewrite | 升级：`OnPreTimeModeSet(manager, ref mode, ref cancelInvocation)`；主动设制保留 `IWorkSceneTime.SetMode` |
| NightScene/NightSceneEventManagerPatch.cs:22 / :32 / :36 / :46 / :58 / :69 / :85 / :92 / :102 / :106 / :116 / :124 / :135 / :145 / :156 / :167 / :177 / :186 / :197 / :207 / :216 | `EventManager.Initialize` / `StartGuestInstantiateLoop`（×2）/ `StartChallengeGuestInstantiateLoop` / `Fever` / `StartGuestSpawningAndTiming`（×2）/ `ModifyTotalTime` / `StopInstantiationLoopAndCloseIzakaya` / `FundEdit`（×3）/ `TipEdit`（×3）/ `ExpEdit`（×3）/ `PassionEdit`（×3）（`Night/EventSystem/EventManager.cs:2885/3442/3464/5575/2926/854/3541/3663/3700/3834/3736`） | Prefix/Postfix/ReversePatch ×6 | 缺口（含字段写回与 6 个反向补丁） |
| NightScene/QTERewardManagerPatch.cs:18 / :35 | `QTERewardManager.OnQTESucceeded`（`Night/EventSystem/QTERewardManager/QTERewardManager.cs:46`） | Prefix/Skip + ReversePatch | 缺口（依赖主线程调度与反向补丁） |
| NightScene/NightSceneDirectorPatch.cs:18 / :22 / :28 / :36 | `NightSceneDirector.TryLeaveSession` / `SpawnManualControlledSpecialGuest` / `GetControlled`（`Night/NightSceneDirector.cs:347/222/210`） | Prefix + Postfix ×2 + Postfix | 缺口（幽幽子本体捕获与试炼返回标记） |
| NightScene/BuffPatch.cs:16 / :27 / :38 / :49 / :60 / :71 | `MystiaQTEBuffReward.Player_ThrowDeliver` / `Player_InstantEvaluation` / `Player_PatientFreeze` / `Player_Fever` / `Player_Fever_Infinite`（×2，含反向补丁）（`Night/EventSystem/QTERewardManager/MystiaQTEBuffReward.cs:179/154/160/195/211`） | Prefix ×5 + ReversePatch | 缺口（目标全为私有方法且以委托注册） |
| NightScene/UIManagerPatch.cs:13 | `NightScene.UI.UIManager.Initialize`（`Night/UI/HUD/UIManager.cs:125`） | Postfix | 缺口 |

### 2.3 数据 / 备菜 / 运行时 / 幽幽子（26 文件 / 54 补丁方法 / 1 个反向补丁）

| 文件:行 | 目标方法（游戏侧） | 种类 | 归宿 |
| --- | --- | --- | --- |
| DataBase/DataBaseCorePatch.cs:11 | `DataBaseCore.Initialize`（`Common/GameData/DataBase/DataBaseCore.cs:59`） | Postfix | 已迁：`OnInjectIngredients`/`Foods`/`Beverages`/`Recipes`/`Items`/`Badges`；三张映射表交框架 |
| DataBase/DataBaseCharacterPatch.cs:18 | `DataBaseCharacter.Initialize`（`Common/GameData/DataBase/DataBaseCharacter.cs:142`） | Postfix | 已迁：`OnInjectNormalGuests`/`OnInjectSpecialGuests` |
| DataBase/DataBaseCharacterPatch.cs:27 | `DataBaseCharacter.GetNPCLabel`（`:363`） | Prefix/Skip | 删除：框架写标签正反映射 |
| DataBase/DataBaseCharacterPatch.cs:44 | `DataBaseCharacter.SetupPortrayalVisual`（`:96`） | Prefix/Skip | 升级：`IPortraitProvider` + `OnInjectClothes` |
| DataBase/DataBaseDayPatch.cs:18 | `DataBaseDay.Initialize`（`Common/GameData/DataBase/DataBaseDay.cs:101`） | Postfix | 已迁：`OnInjectNpcs`/`OnInjectMerchants`/`OnInjectDialogs` |
| DataBase/DataBaseDayPatch.cs:26 | `DataBaseDay.IsMerchant`（`:356`） | Postfix/Rewrite | 删除：框架注入后原表可读 |
| DataBase/DataBaseDayPatch.cs:33 | `DataBaseDay.RefMerchant`（`:354`） | Prefix/Skip | 删除：缺键安全由框架承担 |
| DataBase/DataBaseNightPatch.cs:14 | `DataBaseNight.Initialize`（`Common/GameData/DataBase/DataBaseNight.cs:18`） | Postfix | 升级：`OnInjectSpells`（新增 seam） |
| DataBase/DataBaseSchedulerPatch.cs:11 | `DataBaseScheduler.Initialize`（`Common/GameData/DataBase/DataBaseScheduler.cs:31`） | Postfix | 升级：`OnInjectMissionNodes`/`OnInjectEventNodes`（新增 seam） |
| DataBase/DataBaseAchievementPatch.cs:11 | `DataBaseAchievement.Initialize`（`Common/GameData/DataBase/DataBaseAchievement.cs:360`） | Postfix | 删除（委托为空实现，无行为） |
| CoreLanguage/DataBaseLanguagePatch.cs:12 | `DataBaseLanguage.Initialize`（`Common/GameData/DataBase/DataBaseLanguage.cs:55`） | Postfix | 已迁：文本进代理字段；`OnInjectBuffs`/`OnInjectSpells` 覆盖 Buff 与符卡语言 |
| CoreLanguage/NightSceneLanguagePatch.cs:12 | `NightSceneLanguage.Initialize`（`Common/GameData/Language/NightSceneLanguage.cs:20`） | Postfix | 已迁：`OnInjectSpecialGuests`（评价与对话） |
| CoreLanguage/DaySceneLanguagePatch.cs:11 | `DaySceneLanguage.Initialize`（`Common/GameData/Language/DaySceneLanguage.cs:18`） | Postfix | 升级：`OnInjectDayMaps` + NPC 名（新增 seam） |
| PrepScene/IzakayaConfigPannelPatch.cs:25 / :29 | `IzakayaConfigPannel.OnPanelOpen`（`_Pannels/WorkPrepScene/IzakayaConfigPannel.cs:285`） | Prefix + Postfix | 已迁：`IPrepListener.OnPrepConfirmed`（面板实例随参数传入） |
| PrepScene/IzakayaConfigPannelPatch.cs:39 | `IzakayaConfigPannel.GoToSpecific`（`:348`） | Postfix | 升级：`OnConfigTabSelected` |
| PrepScene/IzakayaConfigPannelPatch.cs:50 | `IzakayaConfigPannel._SolveDailyCompletion_b__64_7`（编译器生成） | Prefix/Skip | 升级：`OnPrepConfirmed` + `IPrepNightSessionServices.Confirm()`（回调名不一致，见第 5 节） |
| PrepScene/IzakayaConfigPannelPatch.cs:74 | 同上 | ReversePatch | 删除（改走服务） |
| PrepScene/IzakayaConfigurePatch.cs:21 / :25 | `IzakayaConfigure.Initialize` / `UpdateValue`（`Common/GameData/RunTime/WorkCollections/IzakayaConfigure.cs:78/67`） | Postfix ×2 | 升级：`OnConfigureUpdated` |
| PrepScene/IzakayaConfigurePatch.cs:49 | `IzakayaConfigure.RegisterToDailyRecipes`（`:257`） | Prefix/Skip | 升级：`OnPreRecipeAdded(id, ref cancelInvocation)` |
| PrepScene/IzakayaConfigurePatch.cs:64 | `IzakayaConfigure.RegisterToDailyBeverages`（`:280`） | Prefix/Skip | 升级：`OnPreBeverageAdded` |
| PrepScene/IzakayaConfigurePatch.cs:79 | `IzakayaConfigure.RegisterToCookers`（`:294`） | Prefix/Skip | 升级：`OnPreCookerAssigned` |
| PrepScene/IzakayaConfigurePatch.cs:100 / :105 | `IzakayaConfigure.LogoffFromDailyRecipes` / `LogoffFromDailyBeverages`（`:265/288`） | Prefix ×2 | 已迁：`OnRecipeRemoved` / `OnBeverageRemoved` |
| PrepScene/IzakayaConfigurePatch.cs:118 | `IzakayaConfigure.StoreFood`（`:171`） | Prefix | 已迁：`OnFoodStored`（重入闩改走框架放行机制） |
| RunTime/RunTimeAlbumPatch.cs:13 | `RunTimeAlbum.ChangePlayerSkin`（`Common/GameData/RunTime/RunTimeAlbum.cs:339`） | Postfix | 待核对：皮肤切换通知；若 `IPortraitProvider` 已覆盖则删除 |
| RunTime/RunTimeDayScenePatch.cs:20 | `RunTimeDayScene.GetMerchantData`（`Common/GameData/RunTime/RunTimeDayScene.cs:560`） | Postfix/Rewrite | 删除：已拥有食谱过滤由框架商人链路承担 |
| RunTime/RunTimeSchedulerPatch.cs:36 / :57 / :72 / :76 / :92 / :100 | `RunTimeScheduler.ScheduleEvent` / `ProcessReward` / `OnDayEnd` / `OnAfterDayEnd` / `Method_Internal_Static_Void_Action_PDM_0`（×2）（`Common/GameData/RunTime/RunTimeScheduler.cs:2143/2912/1319/1335`，后者为编译器生成） | Prefix ×6 | 缺口 |
| RunTime/TrackedMissionDataPatch.cs:19 | `RunTimeScheduler.TrackedMissionData.UpdateFinishStates`（`Common/GameData/RunTime/RunTimeScheduler.DataModels.cs:119`） | Postfix/Rewrite | 待核对：需要运行时任务条件钩子，否则缺口 |
| NightScene/YuyukoBossDataPatch.cs:42 | `YuyukoBossData.MainChallengeLoop`（`Night/BossBattle/YuyukoBossData.cs:65`） | Postfix | 缺口 |
| NightScene/YuyukoChallengeContextPatch.cs:26 / :36 / :46 | `__c__DisplayClass16_0` 的两个闭包方法（编译器生成） | Prefix + Postfix + Prefix | 缺口 |
| NightScene/YuyukoLockCookersPatch.cs:25 / :37 | `__c__DisplayClass16_6` 状态机 `MoveNext`（编译器生成） | Prefix + Postfix | 缺口 |
| NightScene/YuyukoMainLoopPatch.cs:22 / :33 | `_MainChallengeLoop_d__16.MoveNext`（编译器生成） | Prefix + Postfix | 缺口 |
| NightScene/YuyukoOnFailPatch.cs:14 | `__c__DisplayClass16_0` 失败剧情协程 `MoveNext`（编译器生成） | Prefix | 缺口 |
| NightScene/YuyukoPhase2GuestSpawnPatch.cs:19 | `__c__DisplayClass16_0` 二阶段刷客协程（编译器生成） | Prefix | 缺口 |
| NightScene/YuyukoPhase3GuestSpawnPatch.cs:29 | `__c__DisplayClass16_6` 三阶段刷客协程（编译器生成） | Prefix | 缺口 |
| NightScene/YuyukoRetakeContextPatch.cs:24 / :34 / :43 | `__c__DisplayClass16_6` 的两个闭包方法（编译器生成） | Prefix + Postfix + Postfix | 缺口 |
| NightScene/YuyukoTimedNegativeSpellPatch.cs:20 | `__c__DisplayClass16_0` 限时负面符卡协程（编译器生成） | Prefix | 缺口 |
| NightScene/YuyukoTimingPatch.cs:23 / :39 | `__c__DisplayClass16_0` 计时协程（编译器生成） | Prefix + Postfix | 缺口 |
| NightScene/IncomeControllerYuyukoPatch.cs:24 / :35 | `IncomeControllerYuyuko.SetContext` / `SetTargetProgress`（`Night/UI/HUD/IncomeControllerYuyuko.cs:72/104`） | Postfix + Prefix/Rewrite | 缺口 |

### 2.4 计数与总体差异

- 文件 69 个（场景与白天 24、营业 19、数据与其余 26）；补丁类 68 个（其中 `DataBaseAchievementPatch` 未出现在 `PatchRegistry`，但仍参与编译），另有 1 个数据结构 `GeneratedOrderInfo`。
- 补丁方法 188 个：Prefix/Postfix 170、ReversePatch 18（白天 3、营业 14、数据 1）、无 Transpiler/Finalizer、1 个构造器补丁（`DaySceneMapProfile`）。
- 归宿分布（按表格行统计，部分行合并了同归宿的多个补丁方法，130 行覆盖 188 个方法）：已迁 34 行、升级 35 行、删除 16 行、缺口 32 行、待核对 7 行，其余 6 行写法为组合归宿未单列。缺口集中在幽幽子挑战（19 个方法）、事件管理器（21 个方法）、Buff（6 个方法）等游戏专有链路。
- 注意事项：
  - `Patches` 目录内多处 `[TracePatch(...)]` 是调用追踪属性（生成器产物），不计入上表，但迁移时必须一并删除（其生成器会产出 `HarmonyLib` 代码）。
  - 若干补丁的参数表与游戏源码签名不一致（Harmony 按名匹配子集），迁移时**以壳代码（interop）的实际签名为准**，见第 5 节。
  - 桥接侧对同几个游戏方法存在「门控 + 通知」双挂；按 HarmonyX 语义，前缀跳过原版时 Postfix 仍会执行，因此门控不会阻止通知派发——这是现有设计成立的前提，迁移后要逐条确认。



## 3. 中间件已有桥接面

### 3.1 现有 seam（82 个：特性式 81 + 手写 1）

| 位置 | 数量 | 触发/作用 |
| --- | --- | --- |
| `HarmonySeams.cs` SceneSeams | 13 | `ISceneListener`（7 个场景，Enter 由 Start/Awake 触发）、`IDayListener.OnDayEnded`/`OnDayMapEntered`/`OnDialogOpened`/`OnSceneChanging`、夜转场门控 |
| `HarmonySeams.cs` DatabaseSeams | 5 | 转发 `DatabaseInject` 的 5 个 Apply（见 3.3） |
| `HarmonySeams.cs` CookSeams | 4 | `ICookListener` 四项，全 Postfix 通知 |
| `HarmonySeams.cs` PrepSeams | 10 | `IPrepListener` 十项，全 Postfix 通知 |
| `HarmonySeams.cs` PrepPanelCache | 2 | 无接口，仅缓存面板实例 |
| `HarmonySeams.cs` WorkSeams | 6 | `IWorkListener` 六项 |
| `HarmonySeams.cs` DayInputSeams | 5 | `IDayInputListener` 五项（`OnMoveInput` 为 Postfix 且无来源实例） |
| `HarmonySeams.cs` GuestSeams | 15 | `IGuestGroupListener` 各通知；其中 `PostInitializeGuestGroup` 的 Prefix 会在有 `IGuestDirector` 驱动时跳过原版入座，`GenerateOrder` 的 Postfix 会写回 `ref` 订单与消息，`EvaluateOrder`/`EvaulateManualOrder` 用 Transpiler 插入评价改写 |
| `StockHolds.cs` | 13 | 门控实现：移动清零输入、冲刺、互动、离场 ×7、订单 ×2、托盘关闭（含待上菜退回与托盘面板关闭） |
| `GuestSpawnPipeline.cs` | 3 | `IGuestSpawnModifier` 三项，均写回 `ref` |
| `SeatAndOrder.cs` | 1 | 座位改写：`MoveToDesk` Prefix，命中时跳过原版并自行走位 |
| `PortraitSprites.cs` | 4 | `CharacterPortrayal` 四个 `Load*` 的 `ref __result` 写回（立绘注入落点） |
| `RuntimeInstall.cs` | 1（手写 `harmony.Patch`） | `MonoSingletonPersistant<UniversalGameManager>.Awake` Postfix → `OnSceneAwake(Splash)`，只派发一次 |

### 3.2 开关（`StockGate`，9 个开关 + 1 个放行计数）

| 开关 | Reset 置回 true 的场景 | 拦住的游戏方法 |
| --- | --- | --- |
| `DayEnd` | `Day` | `DayScene.SceneManager.OnDayOver` |
| `Move` | `Day` | `CharacterControllerInputGeneratorComponent.UpdateInputDirection`（**不是跳过，而是把输入清零**） |
| `Sprint` | `Day` | `DayScenePlayerInputGenerator.OnSprintPerformed` |
| `Interact` | `Day` | `DayScenePlayerInputGenerator.TryInteract` |
| `TransitionDialog` | `Day` | `UniversalGameManager.OpenDialogMenu` |
| `MapConfirm` | `PrepNight` | `IzakayaSelectorPanel_New._OnGuideMapInitialize_b__21_0` |
| `Leave` | `Night` | `PayAndLeave`、`ExBadLeave`、`RepellAndLeavePay`、`RepellAndLeaveNoPay`、`PlayerRepell`、`PatientDepletedLeave`、`LeaveFromDesk`（7 条） |
| `Order` | `Night` | `GuestsManager.GenerateOrderSession`、`GuestGroupController.GenerateOrder` |
| `ServeClose` | `Night` | `WorkSceneServePannel.OnPanelClose` |
| `_bypass`（`[ThreadStatic]` 计数，不是开关） | 不重置，靠 `Bypass` 的 try/finally 配平 | `Allow(open)` 即 `open || _bypass > 0`；服务内调用游戏方法时先 `Bypass` 放行 |

Reset 的唯一入口是 `SceneLoopHost.Enter(scene)`；**同一个场景再次 `Enter` 不会重置**。

### 3.3 数据写入（5 个 Apply）

| Apply | 目标表（字段级） | 数据来源 |
| --- | --- | --- |
| `ApplyCore` | `DataBaseCore.Ingredients`/`Foods`/`Beverages`/`Recipes`/`Cookers`/`Izakayas`，并把特殊客人的 `Spawns` 追加到 `Izakayas[].SpecialGuestPool` | 对应代理结构的 ID 与数值/标签/枚举字段 |
| `ApplyLanguage` | `DataBaseLanguage` 的 `Ingredients`/`Foods`/`Beverages`/`Cookers`/`Izakayas`/`Items`/`Badges`/`NormalGuest`/`SpecialGuest`/`SpecialGuestFoodRequest`/`SpecialGuestBevRequest` | 代理结构的 `Name`/`Description*`/`Picture`/请求行（`TagId`+`Line`） |
| `ApplyCharacters` | `DataBaseCharacter.NormalGuest`/`SpecialGuest`/`SpecialGuestVisual` | 代理结构字段（其余参数取模板实体） |
| `ApplyDay` | `DataBaseDay.allNPCs`（并回填 `DataBaseCharacter` 的正反标签映射）、`allDialogPackages`、`allMerchants` | `NpcData`（含 `Places`）、`DialogData`（含 `Lines`）、`MerchantData`（含 `Offers`） |
| `ApplyNightLanguage` | `NightSceneLanguage.SpecialEvaluation`/`SpecialConversation` | `SpecialGuestData.Evaluations`/`Conversations` |

### 3.4 桥接内部仍在用反射/字符串名的位置（迁移时应清理）

- `DatabaseInject.cs`：用 `Activator.CreateInstance` 构造值类型（特殊客人的 4 元组、请求字典值、对话数组元素），用 `AccessTools.Property/Field` + 索引器按**字符串名**写 `DataBase*` 静态字典。
- `GameRecords.cs`：反射取 `DialogPackage.dialogMeta` 与其数组元素、按最长构造器构造 Cooker/SpecialGuest、模板取值、字段名硬改写兼容。
- `StockHolds.cs`：反射取 `m_CurrentTray` 并调 `ClosePanel`、反射取 `willServeFood`/`willServeBeverage` 做退款、反射 `Duplicate`。
- `HarmonySeams.cs`：反射改写 `AllOrdersData` 订单栈（`Peek`/`Pop`/`Push`）。
- `GuestSpawnPipeline.cs`：反射取 `NormalGuestVisual`、统一配色与像素集 `Initialize`。
- `SceneServices.cs`：`GameMembers.Invoke/Set` 按字符串名调私有或编译器生成成员（`OnDayOver`、`ExBadLeave`、`PatientDepletedLeave`、`LeaveFromDesk`、`GenerateOrderSession`、`_OnGuideMapInitialize_b__21_0`、`_SolveDailyCompletion_b__61_7` 且回退 `SolveDailyCompletion`），并写 `m_CurrentSelectedSpot`/`m_CurrentSelectedIzakayaLevel`。
- `RuntimeInstall.cs`：`AccessTools.Method` 取泛型单例 `Awake` 以便手写 `harmony.Patch`。

迁移要点：壳代码（interop）已把游戏成员统一暴露为 `public`，上述反射多数可直接换成强类型调用；只有「按字符串名挂载的目标方法」必须先核对真实成员名。

### 3.5 已知风险

- **同方法双挂 11 处**，Harmony 未指定优先级，Prefix/Postfix 相对顺序未固定：`UpdateInputDirection`、`OnSprintPerformed`、`TryInteract`、`PayAndLeave`、`ExBadLeave`、`RepellAndLeavePay`、`RepellAndLeaveNoPay`、`PlayerRepell`、`PatientDepletedLeave`、`GuestGroupController.GenerateOrder`、`WorkSceneServePannel.OnPanelClose`。
- **前缀跳过原版时 Postfix 仍会执行**（HarmonyX 语义）：现有门控不会阻止对应通知派发，这是框架设计成立的前提。
- 桥接有 19 处目标是**字符串名**（`_OnGuideMapInitialize_b__21_0`、`_SolveDailyCompletion_b__61_7`、`OnPanelOpen`、`InvokeOrderUpdate`、`Send`、`Extract`、`OnPlayableDirectorPlayed`、`PostInitializeGuestGroup`、`EvaulateManualOrder`、`ExBadLeave`、`PatientDepletedLeave`、`LeaveFromDesk`、`GenerateOrderSession`、`OnPanelClose`、`MonoSingletonPersistant<UniversalGameManager>.Awake` 等）：游戏侧改名即静默失效，迁移时逐个核对。
- `GuestPipeline` 的评价改写用 Transpiler 依赖 `Ldarg_1`/`Ldloca` 指令形态，脆弱；迁移后若评价重放行为改变，优先复核这里。


## 4. 数据面字段对照（ResourceEx → 游戏表 → 框架代理）

口径：(a) 框架字段已覆盖，可直接迁移；(b) 框架缺字段（已确认要在本轮补全）；(c) 语义不同需转换；(d) 结构上不支持或需明确语义。

### 4.1 覆盖情况

| 表 | 情况 |
| --- | --- |
| 食材、食物、饮料、菜谱、道具、徽章 | (a) 覆盖；食物/饮料的 `Collab` 是框架多出的能力，源包无法表达 |
| 语言表（食材/食物/饮料/厨具/店家/道具/徽章/普通客人/特殊客人/请求行） | (a) 覆盖 |
| 普通客人 | (a) 覆盖（本仓库当前未追加，属框架冗余能力） |
| 特殊客人 | 部分：(a) 22 个字段覆盖；(b) 缺 `Kind`/`IsParticular`/`IsCollabCharacter`/`HideInAlbum`/`CommisionAreaLabel`/`SpawnMarker`/`Kizuna`（28 个子字段）/`GuestRequestLine.Enable`——本轮补全 |
| 对话 | 部分：(a) 名字、逐行文本、说话人身份与位置覆盖；(b) 缺逐行 `actions[]` 与 5 个行级 flag——本轮补全；(d) 框架当前让所有行复用模板第 0 行的 meta，补齐后需改为逐行写入 |
| 商人 | (a) 字段覆盖；(d) 注入路径不同：本仓库写运行时追踪表并拦截查询，框架直接写基础商人表——框架侧必须补齐运行时追踪条目、商品生成、缺键安全与原版等价的「已拥有食谱过滤」 |
| NPC | (a) 标签正映覆盖；(b) 缺 NPC 显示名（`DaySceneLanguage` 的 NPC 名表）——本轮补全，并需回填反向标签表 |
| 店家 | (a) 只用到「把特殊客人刷客池追加进店家」，已覆盖 |
| 符卡 | (b) 缺 `SpellLang`、`SpecialGuestSpell`、`SpecialGuestSpellPortrayal`、`CharacterHasSpell`——本轮补全（含新的初始化 seam） |
| Buff | (b) 缺 `BuffDescription`——本轮补全 |
| 服饰 / 皮肤 | (b) 缺 `Clothes`、`Items`、`Items` 语言、玩家像素集——本轮补全 `OnInjectClothes`；(d) 玩家像素集 `SelfSpriteSet.dlcs` 是**整数组替换已有表项**，实现时需明确语义 |
| 地图 | (b)/(d) 缺 `mapData`、`mapReference`、`allSpawnMarkerLabels`、`allCollectablesLabels`、`MapLanguageData`，且地图本体是运行时构建的 GameObject/Tilemap——本轮补 `OnInjectDayMaps`，引擎对象部分需在实现时确定表达方式 |
| 任务 / 事件节点 | (b)/(d) 缺 `DataBaseScheduler.allNodes`、`AllNodesMapping`、`Missions`，节点内含对话包/触发/奖励等引擎对象——本轮补 `OnInjectMissionNodes`/`OnInjectEventNodes`（含新 seam） |
| 成就 | 双方都不写表；对应补丁为空的委托，判为删除 |
| 映射表 | (b) 缺 `FoodsMapping`、`BeveragesMapping`、`RecipesMapping`——本轮由框架写入，值为**注入模组自己的 id** |

### 4.2 语义差异（需转换）

- **图片**：本仓库用包内 URI（`rex://…`）经 Addressables 与自建 Sprite 构建；框架用**相对模组目录的 PNG 路径**（`IModContext.LoadSprite`）。涉及 6 个源字段（食材/食物/饮料的 `spritePath`、特殊客人立绘 `portraitPath`、像素集 `mainSprite`/`eyeSprite`）。
- **像素图集**：本仓库把包内贴图**叠加到模板帧数组**（等长校验、64×64、指定 pivot）；框架把 `Body`/`Eyes` 当**整组帧**直接写入。迁移时必须核对两组语义是否等价。
- **值类型写入**：特殊客人的语言 4 元组与对话数组元素在 IL2CPP 侧是值类型，本仓库有专门的绕行实现；框架用 `Activator.CreateInstance` 构造（见 3.4），是否触发同一互操作缺陷未确认。
- **枚举对照**：需要逐值核对 `CookerKind` ↔ 游戏 cooker 类型、`GoodsKind` ↔ 商品类型。
- **立绘挂载**：本仓库在对话面板与说明器两处拦截；框架在 `CharacterPortrayal` 的四个 `Load*` 上注入（见 3.1）。

### 4.3 结构性不支持（记录，不作为本轮目标）

- 调度器的存档语义（任务/事件节点的存档恢复与运行时状态修正）。
- 逐行对话 `actions[]` 中引用 Addressables 资源的部分（立绘/音效）——第一版可只支持文本与分支选项。
- 地图本体的引擎对象（Tilemap/高度图/内存 GameObject 提供器）：框架只表达数据侧，引擎对象构建仍留在模组。


## 5. 未决与待核对项

跨文档的未决项集中在此，工单里只引用编号：

1. 备菜确认回调名：模组 `_SolveDailyCompletion_b__64_7` 与中间件 `_b__61_7` 不一致，需以游戏工程当前版本核对。
2. `OnGroupOrderGenerated` 需要的订单生成结果位于闭包局部函数内部，需对订单生成方法做一次定向核对。
3. 快进提交拦截（`DaySceneSustainedPannel.OnFastForwardSubmit`）与聊天菜单扩展（`UIManager.OpenAfterChatMenu` 回调数组）的接口形态未定，需定向核对后再定。
4. 三个编译器生成名（`GuestsManager.__c__DisplayClass174_0` 相关、队内耐心耗尽的 `Method_Private_Void_GuestGroupController_PDM_1`、`WorkSceneCookingSelectionPannel.__c__DisplayClass79_0`）在游戏源码中查不到，需以实际反编译清单核对。
5. 运行时任务条件改写（`TrackedMissionDataPatch`）是否有可用钩子未定。
6. `DaySceneMapPatch` 的 NPC 定位改写能否完全由 Place 数据表达未定。
7. `RunTimeAlbumPatch`（皮肤切换通知）归宿未定。
8. ResourceEx 的 `ForceAddOrUpdateValueTuple` 所绕过的互操作缺陷，在框架的注入路径下是否同样出现未定。
9. 立绘像素图集语义差异（模组「叠加模板」vs 框架「整组帧」）需在实施时核对。
10. 有 6 处补丁的参数表与游戏源码签名不一致（`DayScene.SceneManager.SwapMap`、`DialogPannel.GetSpeakerVisual`、`RunTimeScheduler.ScheduleEvent`/`OnDayEnd`/`OnAfterDayEnd`、`GuestsManager.SpawnSpecialGuestGroup`、`WorkSceneSustainedPannel.OpenServePanel`、`DataBaseCharacter.SetupPortrayalVisual`），Harmony 靠按名子集匹配；迁移以壳代码（interop）的实际签名为准，遇到无法表达的直接记缺口。
11. 客机「无主机评价结果时不本地评价」目前靠 `GuestsManager.EvaluateOrder` 前缀跳过；迁移优先复用框架的评价覆盖机制（`OnGroupEvaluated` + `IWorkSceneGuests.Evaluate`），确认不足再考虑新增开关。
12. `HarmonyX` 语义：前缀跳过原版时 Postfix 仍会执行——现有「门控 + 通知」组合能成立正依赖这一点，迁移后必须逐条确认（例如 `SetLeaveEnabled(false)` 之后 `OnGroupLeft` 是否仍应派发）。
13. 桥接侧 19 个按字符串名挂载的目标与 3.4 的反射清单需在迁移中核对并尽量替换为强类型调用（壳代码已公开成员）。
14. 服饰像素集 `SelfSpriteSet.dlcs` 是整数组替换已有表项，框架数据面需明确「只新增、不改已有」的边界后再实现。
15. 备菜 `StoreFood` 的重入闩（原为静态布尔）与框架放行机制的对应关系需在实施时确认，避免自我触发或漏发同步。
16. `Patches` 目录内的 `[TracePatch(...)]` 追踪属性与其生成器会产出 `HarmonyLib` 代码，迁移时必须一并删除（它们不计入补丁方法计数）。
