# 模组工单（MetaMystia）

读者：负责迁移模组的执行者。配套：[`conventions.md`](conventions.md)、[`audit-facts.md`](audit-facts.md)（逐补丁事实与归宿）、[`mefx-handoff.md`](mefx-handoff.md)（中间件侧接口与挂点）。

所有工作包共同前提：中间件新接口已可用（SDK 已打包并被本仓库 `nuget.config` 引用）。

## WP-A 工程与入口

自持文件：`src/MetaMystia.Mod/MetaMystia.csproj`、`src/MetaMystia.Mod/mod.json`、`src/MetaMystia.Network/Network.props`、`src/MetaMystia.Mod/Plugin.cs`（删）、`src/MetaMystia.Mod/PluginHost.cs`、`src/MetaMystia.Mod/Managers/PluginManager.cs`、`src/MetaMystia.Generators/LogGenerator.cs`、`src/MetaMystia.Generators/TraceGenerator.cs`（删）、`src/MetaMystia.Preloader/`（删）、`src/MetaMystia.Network.Tests/*.csproj`、`src/MetaMystia.Flow.Tests/*.csproj`。

要做的事：

1. `MetaMystia.csproj` 改为 `Sdk="Mystia.Extension.Sdk/1.0.0"`，`TargetFramework` 为 `net10.0`，保留 `AssemblyName` 与 `RootNamespace`，`MystiaInteropDir` 由本地属性提供。
2. 删除：`BepInEx.*` 与 `BepInEx.PluginInfoProps` 包引用、`Costura.Fody`、`Fody`、`FodyWeavers.*`、输出改名到 `plugins` 的 `OutputPath` 与 `RenameBuildOutput` 目标、所有 `$(BepInExPath)` 下的 `HintPath`（互操作改由 SDK 引入）。
3. 保留：对 `MetaMystia.Network` 与 `MetaMystia.Generators`（Analyzer）的引用、嵌入式资源（`public.pem`、两份 locale json）。`MemoryPack` 与 `System.CommandLine` 改为普通包引用。
4. 只要 `Patches/Compat/` 存在（WP-F 建立），`MetaMystia.csproj` 保留一个仅编译用的 `HarmonyX` 引用（`PrivateAssets=all`、`Private=false`），供缺口文件使用；除缺口文件外不得新增 Harmony 代码。
5. 新增 `mod.json` 并复制到输出目录：`id` 取原 `MyPluginInfo.PLUGIN_GUID`（`MetaMystia`），`version` 取 `Versions.props` 的 `Version`，`loadAfter` 为空。
6. `Network.props`：`TargetFramework` 升 `net10.0`；游戏程序集引用改走 `MystiaInteropDir`；`Il2CppInterop.Runtime` 改为 NuGet 包引用（不再指向 BepInEx core），不再 `Import` `MetaMystia.local.props` 里的 `BepInExPath`。
7. `MetaMystia.Server` 与两个测试工程随 `Network` 升 `net10.0`；`MetaMystia.Preloader` 从解决方案删除。
8. `Flow.Tests` 的 `Compile Include` 列表要跟着迁移结果调整（原先链接的 `Patches/HarmonyPrefixFlow.cs`、`Patches/Common/IzakayaSelectorPanelPatch.cs` 等会被删除或改写），测试必须继续起实际断言作用。
9. `LogGenerator` 改为生成挂在框架 `ILog` 上的 `Log`（保持 `[AutoLog]` 类的使用方式不变，162 处调用点不动）；`TraceGenerator` 与 `TracePatchAttribute` 删除。
10. 入口：删 `Plugin.cs`；新增一个 `[AutoWire]` 的 `IPostInitialize` 实现，做原来 `Load()` 的全部初始化（配置、本地化、消息格式化器、Addressables、`PluginHost` 的创建）；场景唤醒/开始改用 `ISceneListener`；每帧/每物理帧逻辑改用 `IGlobalGameLoop`；宿主组件改用 `IIl2CppComponentHost.CreatePersistent`；`PluginManager.RunOnMainThread` 转发到 `IMainThreadScheduler`；配置读写改用 `ICommonServices.Config`/`Caching`；`Plugin.Instance.Log` 一类静态入口改为模组内的静态上下文。

验收：全解决方案编译通过；`dotnet run --project src/MetaMystia.Network.Tests`、`dotnet run --project src/MetaMystia.Flow.Tests` 通过（与迁移前基线对比）；静态检查中「无 BepInEx」一项此时尚不成立（缺口文件还未归位，见 WP-F）。

## WP-B 场景进入与白天

自持范围：`Patches/SceneManager/*`、`Patches/DayScene/*`、`Patches/Common/UniversalGameManagerPatch.cs`、`Patches/Common/CharacterControllerInputGeneratorComponentPatch.cs`。

对应表（逐项细节见 `audit-facts.md`）：

| 原补丁 | 归宿 |
| --- | --- |
| `MainSceneManagerPatch` | `ISceneListener.OnSceneAwake(Main)` |
| `DaySceneManagerPatch` | `OnSceneAwake(Day)`；快进入夜用 `IDaySceneScheduleServices.SetEndEnabled/End`；地图切换用 `IDaySceneMapServices.Swap(..., onFinished)`；删除反向补丁 |
| `PrepNightSceneManagerPatch` / `StaffSceneManagerPatch` / `ResultSceneManagerPatch` | 各自 `ISceneListener.OnSceneStart(...)`；Result 场景**删除** `GuestsManager.Initialize` 调用（行为变更） |
| `NightSceneManagerPatch` | `OnSceneStart(Night)` + 清理改到 `IWorkSceneGameLoop.Shutdown` |
| `UniversalGameManagerPatch` | 对话打开通知走 `IDayListener.OnDialogOpened(package)`（空 `dialogContext` 回填与「最近阅读」记录在模组侧完成，模板包用 `ICommonServices.Dialogs.TryResolve`）；夜转场开关走 `ICommonServices.SetNightTransitionEnabled`；主动加载走 `ICommonServices.LoadScene`；场景变化通知走 `IDayListener.OnSceneChanging` |
| `StatusTrackerPatch` | 邀请记录改走 `IGuestRecords.RecordInvited`（作用域无关）；删除反向补丁，调用点改走同一 API |
| `CharacterControllerInputGeneratorComponentPatch` | `IDayInputListener.OnMoveInput(unit, direction)`（带来源实例，模组自行判断是否本地玩家） |
| `DayScenePlayerInputPatch` | `IDayInputListener` 的冲刺与互动通知 + `IDaySceneInputServices.SetSprintEnabled/SetInteractEnabled`；注意原补丁按「控制台是否打开」条件跳过，改用开关时要在开关里表达同一条件 |
| `DaySceneSustainedPannelPatch` | 快进提交走 `IDayListener` 的 `OnPreDayFastForward`（中间件 WP-M4 待补，见 `mefx-handoff.md` 未决疑点 5） |
| `CollabBehaviourComponentPatch` + `DaySceneUIManagerPatch` | 故事回放入口改走聊天菜单扩展接口（中间件 WP-M4 待补，同上未决疑点 5） |
| `SaveManagementPatch` | 缺口 |
| `NoteBookProfilePannelPatch` | 缺口（笔记本立绘开关） |
| `DaySceneShopPannelPatch`、`DaySceneMapProfilePatch`、`DaySceneMapPatch` | 见 WP-E（地图与 NPC 注入相关）与缺口清单 |
| `DaySceneChatSelectionPannel__c__DisplayClass17_0Patch` | 先删（商人链路做对后应不再需要），若实测缺失再回填为缺口 |
| `YuyukoExtraDialogData__c__DisplayClass4_0Patch` | 缺口 |

验收：编译通过；静态检查（无新增 Harmony、无作用域外服务调用、`ref` 不进 lambda）；白天/备份场景与原有同步发送点的调用链在源码上仍能找到。

## WP-C 备菜

自持范围：`Patches/PrepScene/*`。

| 原补丁 | 归宿 |
| --- | --- |
| `IzakayaSelectorPanelPatch` | `IPrepListener.OnGuideMapConfirmed/OnGuideSpotSelected` + `IPrepNightMapServices.SetConfirmEnabled(false)` 拦确认 + `IPrepNightMapServices.Confirm` 主动确认；删除反向补丁与 `m_CurrentSelectedSpot` 写回 |
| `IzakayaConfigPannelPatch` | 面板确认走 `IPrepListener.OnPrepConfirmed` 或 `IPrepNightSessionServices.Confirm`；页签切换走 `OnConfigTabSelected`；确认出餐/进入营业走 `IPrepNightSessionServices.Confirm/ToWork`；删除反向补丁与静态 `instanceRef` |
| `IzakayaConfigurePatch` | 菜谱/饮料/厨具注册走 `OnPreRecipeAdded`/`OnPreBeverageAdded`/`OnPreCookerAssigned`（可取消，用于对端缺 DLC 时不同步）；移除通知走对应通知；存放走 `IPrepListener.OnFoodStored` 或取出通知（先确认为备菜存放）；作弊流速写回 `IzakayaConfigure.NormalGuestInterval` 一类字段改走 `OnConfigureUpdated` |

验收：编译通过；静态检查；备菜改菜单的发送点在源码上仍能找到；`StoreFood` 的重入闩改为框架的放行机制，不得保留 `PatchBypassToken`。

## WP-D 营业

自持范围：`Patches/NightScene/*`（不含 `Yuyuko*` 与 `IncomeControllerYuyukoPatch.cs`）。

| 原补丁 | 归宿 |
| --- | --- |
| `CookControllerPatch` | 拦截改 `OnPreCookStarted`/`OnPreCookCountdownStarted`（可取消、可改写参数）；通知走 `ICookListener` 四项；删除全部反向补丁，主动开煮/取出/存放/倒计时改走 `IWorkSceneCook` |
| `CookSystemManagerPatch` | `IWorkSceneCook.SetCallEnabled(false)` 阻止原版开厨 |
| `GameTimeManagerPatch` | 强制时制改 `OnPreTimeModeSet(manager, ref mode, ref cancelInvocation)`；主动设制保留 `IWorkSceneTime.SetMode`；`OnTimeModeChanged` 仍为通知 |
| `WorkSceneServePannelPatch` | 面板打开走 `OnServePanelOpened`；关闭前同步改 `OnPreServePanelClosed`（前缀时机必须在原版结算之前）；`Send`/`Cancel` 走 `OnPreDishServed`/`OnPreDishCancelled`；已有的 `IWorkListener` 通知照旧；待上菜字段由模组直接读写游戏对象（壳代码已公开），`SetServedVisualOnUI` 直接调用；删除静态 `instanceRef` 与 `PatchSkipPermit` |
| `WorkSceneStoragePannelPatch` | 取出改 `OnPreStorageExtracted`；若整类只剩静态缓存则删除 |
| `GuestsManagerPatch` | 刷客：`SetSpawnEnabled(false)` + `SpawnNormal/SpawnSpecial`；入座相关：`SetSeatingEnabled(false)` + `IWorkSceneGuests.Seat` + `OnGroupSeated`；订单：`BeginOrderSession` + `OnGroupOrderGenerated`（原 `__c__DisplayClass174_0Patch` 一并处理）；离场/赶客/耐心耗尽/关店：`SetLeaveEnabled`/`SetCloseEnabled` 与 `IWorkSceneGuests.Leave`/`IWorkSceneIzakaya.Close`；删除反向补丁；队内耐心耗尽那条编译器生成的局部方法留缺口 |
| `GuestGroupControllerPatch` | 到达/移动/排队走 `OnGroupArrived`/`OnGroupMovingToDesk`/`OnGroupQueued`；评价覆写走 `OnGroupEvaluated(ref result)`；幽幽子分支留缺口 |
| `SellablePatch` | 缺口（目标为每次读标签都会走的高频私有方法） |
| `NormalGuestsControllerPatch`、`SpecialGuestsControllerPatch` | 缺口（评价后回调无对应挂点） |
| `WorkSceneSustainedPannelPatch`、`WorkSceneCookingSelectionPannel__c__DisplayClass79_0Patch` | 缺口 |
| `NightSceneEventManagerPatch`、`QTERewardManagerPatch`、`NightSceneDirectorPatch`、`BuffPatch`、`UIManagerPatch` | 缺口 |
| `Patches/NightScene/Yuyuko*`、`IncomeControllerYuyukoPatch`、`NightSceneDirectorPatch` | 缺口（保持原行为，不得改写） |

验收：编译通过；静态检查；客人生成/入座/点单/评价/离店的多人发送点在源码上仍能找到；备菜与营业的烹饪、上菜发送点仍在。

## WP-E 数据注入

自持范围：`Patches/DataBase/*`、`Patches/CoreLanguage/*`、`Patches/RunTime/*`、`ResourceEx/Registries/*`、`ResourceEx/Core.cs`。可再分四路：E1 食材/食物/饮料/菜谱/道具/徽章；E2 特殊客人/NPC/对话；E3 商人；E4 地图/任务/事件/符卡/服饰/Buff。

| 原补丁 | 归宿 |
| --- | --- |
| `DataBaseCorePatch` | 删除，改 `OnInjectIngredients`/`OnInjectFoods`/`OnInjectBeverages`/`OnInjectRecipes`/`OnInjectItems`/`OnInjectBadges`；映射表由框架按模组 id 写入 |
| `DataBaseLanguagePatch` | 文本写进对应代理字段；`BuffDescription`/`SpellLang` 走 `OnInjectBuffs`/`OnInjectSpells`；`Missions` 与地图语言随任务/地图注入 |
| `DataBaseCharacterPatch` | 删除，改 `OnInjectSpecialGuests`（含立绘/身体/眼睛路径）与 `OnInjectNormalGuests`；`GetNPCLabel` 前缀删除（框架写标签映射与 NPC 名）；`SetupPortrayalVisual` 前缀删除（`OnInjectClothes` + `IPortraitProvider`） |
| `DataBaseDayPatch`、`RunTimeDayScenePatch` | 删除，改 `OnInjectNpcs`（含 `Places` 与显示名）、`OnInjectMerchants`（商人完整链路交框架，含已拥有食谱过滤）、`OnInjectDialogs`；`IsMerchant`/`RefMerchant` 兜底与 `trackedMerchants` 写入由框架承担 |
| `DataBaseNightPatch` | 删除，改 `OnInjectSpells`（含新 seam） |
| `DataBaseSchedulerPatch` | 删除，改 `OnInjectMissionNodes`/`OnInjectEventNodes`（含新 seam） |
| `DaySceneLanguagePatch` | 删除，改 `OnInjectDayMaps`（地图名与描述） |
| `DialogPannelPatch`、`SpecialGuestDescriberPatch` | 删除（立绘走路径注入，框架负责挂载） |
| `DaySceneMapPatch`、`SpawnMarkerRegistry`、运行时 NPC 挂载与对话重置 | 一并由 `OnInjectNpcs`/`OnInjectDayMaps` 表达；无法表达的部分（例如按地图改写 NPC 位置）在实施时核对后决定是否留缺口 |
| `TrackedMissionDataPatch` | 需要运行时任务条件钩子（中间件待补），否则留缺口 |
| `RunTimeAlbumPatch` | 待定（皮肤切换通知；若无可挂点则留缺口） |
| `DataBaseAchievementPatch` | 删除（空实现，无行为） |

ResourceEx 保留部分（不迁）：包加载与签名校验、Addressables 运行时桥、VFX、符卡实现、地图构建、礼物信箱、控制台命令对外行为。立绘来源统一改为相对模组目录的 PNG 路径，经 `IModContext.LoadSprite` 加载；像素图集（身体/眼睛）与「叠加模板」语义的差异必须在实施时核对。

验收：编译通过；静态检查（立绘字段是路径、不改原版角色立绘数组）；ResourceEx 追加的食材、菜谱、客人、NPC、商人确实经 `IDatabaseExtension` 进入而非 Harmony 后缀。

## WP-F 清理与交付

自持文件：`Patches/PatchRegistry.cs`、`Patches/HarmonyPrefixFlow.cs`（删）、`Patches/PatchBypassToken.cs`（删）、`Patches/Compat/`（新）、`docs/mystia-extension-port-gaps.md`（新）。

1. `PatchRegistry.cs` 只保留缺口台账用途（若 `AllPatched` 状态仍被 UI 使用，保留一个精简实现；否则删除并同步改 UI 引用点）。
2. 缺口文件搬到 `Patches/Compat/`，保持原行为；逐个登记：文件名、原方法、前缀是观察还是跳过、缺哪类回调、是否仍依赖反向补丁。
3. 交付：三列对照表（原补丁类型／归宿／是否编译通过）、缺口清单、行为变更清单。
4. 全量验收命令与静态检查（见 `conventions.md`）。

## 未决疑点（施工中若遇到先报告）

1. 备菜确认回调名：模组 `_SolveDailyCompletion_b__64_7` vs 中间件 `_b__61_7`，以游戏工程当前版本核对为准。
2. 故事回放的聊天菜单入口接口形态未定（见 `mefx-handoff.md` 未决疑点 5）。
3. 快进提交（`DaySceneSustainedPannel.OnFastForwardSubmit`）的钩子未定，未定期间该补丁留缺口。
4. `TrackedMissionDataPatch` 是否有可用钩子未定。
5. `DaySceneMapPatch` 的 NPC 定位改写能否完全由 Place 数据表达未定。
6. `RunTimeAlbumPatch` 的归宿未定。
7. 日志可见性变化（框架 `ILog` 落点与原 BepInEx／游戏控制台不同）。
8. `Flow.Tests` 调整包含列表后，断言的覆盖范围会缩小，需在交付说明里写清。
