# 把 MetaMystia 迁到 Mystia Extension

在分支 `mystia-extension-fx-port` 上做。中间件仓库是独立项目，包名 `Mystia.Extension.Sdk`，版本 `1.0.0`。本文只规定迁法和验收，不包含本机路径。

目标：`src/MetaMystia.Mod` 不再引用 BepInEx，也不再调用 Harmony。行为通过 `[AutoWire]` 接口表达。网络、ResourceEx 数据、多人同步逻辑留在本仓库。中间件没有的能力记入文末缺口，不要用新的 Harmony 补上。

## 约束

- 每个实现类一个实例。生成器会发出 `ModEntrance.Register`。不要写 `IMod`，不要写 BepInEx 插件类。
- 模组工程目标框架改为 `net10.0`。`MetaMystia.Network` 若仍被模组引用，一并升到能被 `net10.0` 引用的目标框架。
- 公开接口在四个命名空间：`Mystia`、`Mystia.Scenes`、`Mystia.Listeners`、`Mystia.Data`。
- `mod.json` 的 `id` 使用现有 `MyPluginInfo.PLUGIN_GUID`，不要另起名字。`version` 使用现有模组版本。`loadAfter` 不是加载顺序。
- 玩家顺序只来自启动器的 `modOrder`。本模组不要自己排序别的模组。
- 产生结果的监听使用 `ref`。桥接按玩家顺序用同一个变量调用每个实现。`ref` 参数不能被 lambda 捕获。
- 不要实现 `IGuestDirector`。`Claim` 会让第一位返回非空驱动的模组独占该客人组，并跳过原版入座、排队和路过。MetaMystia 要观察并同步原版夜晚，不能关掉宿主的原版夜晚。
- 跳过原版刷客用 `IWorkSceneServices.Guests.SetSpawnEnabled(false)`，然后在 `Update` 里调用 `SpawnNormal` 或 `SpawnSpecial`。`IGuestSpawnModifier` 只改仍会执行的原版刷客，不是跳过刷客的方法。
- 场景服务只在对应循环的 `Setup`、`Update`、`Shutdown` 内有效。放到外面调用会抛出 `InvalidOperationException`。进入场景时，该场景的暂停开关恢复为允许；要关掉原版行为，在 `Setup` 里关。
- 暂停开关是 `SetXEnabled(false)`，不是新的监听回调。已有的开关：白天结束 `IDaySceneScheduleServices.SetEndEnabled`，白天移动、冲刺、互动 `IDaySceneInputServices`，备菜确认地图 `IPrepNightMapServices.SetConfirmEnabled`，夜转场对话 `ICommonServices.SetNightTransitionEnabled`，离店、点单 `IWorkSceneGuests.SetLeaveEnabled` 与 `SetOrderingEnabled`，托盘关闭 `IWorkSceneTray.SetCloseEnabled`。
- 通过服务再次调用游戏方法时，桥接会放行这次调用，已有监听仍会执行。不要再写反向补丁去调用原方法。
- 数据扩展实现 `IDatabaseExtension`。传入的是代理结构，不要传入游戏的 `Ingredient`、`Sellable`、`Recipe`、`Sprite` 等对象。图片、立绘、身体、眼睛是相对本模组目录的文件路径。新的特殊客人是新增一条空的 `CharacterPortrayal`，不要替换已有角色的立绘。读盘上的 PNG 用 `IModContext.LoadSprite`。
- NPC 显示名走 `OnInjectNpcs`。桥接会写标签映射，不要再为 `GetNPCLabel` 单独打补丁。
- 日志改走 `IModContext.Log`。不要使用 `UnityEngine.Debug`，也不要再依赖 `BepInEx.Logging.ManualLogSource`。
- 需要 IL2CPP 行为组件时，用 `IIl2CppComponentHost.RegisterBehaviour` 或 `CreatePersistent`。构造函数保持公有，参数类型用 `nint`。
- 回到主线程用 `IMainThreadScheduler.RunOnMainThread`。`PluginManager.RunOnMainThread` 改为转发到这里。
- 游戏逻辑仍以本仓库已有调用和逆向审计为准。本文不授权猜测未读过的游戏方法。

## 工程

`src/MetaMystia.Mod/MetaMystia.csproj` 改为：

```xml
<Project Sdk="Mystia.Extension.Sdk/1.0.0">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>MetaMystia</AssemblyName>
    <RootNamespace>MetaMystia</RootNamespace>
    <MystiaInteropDir>$(MystiaInteropDir)</MystiaInteropDir>
  </PropertyGroup>
</Project>
```

`MystiaInteropDir` 只放在已被忽略的本地属性里，指向本机生成的互操作目录。不要把本机路径写进提交。

删除这些引用和目标：`BepInEx.*`、`Costura.Fody`、`Fody`、输出改名到 `plugins`、`BepInExPath` 下的互操作 `HintPath`。互操作由 SDK 的 `MystiaInteropDir` 引入。

保留对本仓库 `MetaMystia.Network` 和 `MetaMystia.Generators` 的引用。`MemoryPack` 与 `System.CommandLine` 改为普通包引用，不再经 BepInEx 目录加载。

在模组输出目录放置 `mod.json`：

```json
{
  "id": "<现有 PLUGIN_GUID>",
  "version": "<现有 PLUGIN_VERSION>",
  "loadAfter": []
}
```

`src/MetaMystia.Preloader` 不再作为运行入口。它只为了在 BepInEx 发现插件之前跑 Costura 的模块构造函数。迁完后由宿主的 `ModLoader` 加载模组程序集。若嵌入依赖在加载时没有初始化，把依赖改成普通程序集引用，不要恢复 Preloader。

`Plugin` 删除。初始化放到一个 `[AutoWire]` 的 `IPostInitialize` 实现里：读配置、初始化本地化、注册消息格式化器、初始化 Addressables、注册 `PluginHost`。场景唤醒和开始放到 `ISceneListener`。

## 补丁怎么迁

`Patches/PatchRegistry.cs` 里的类型按下面处理。迁完一个，就从 `Patches` 数组删除，并删除对应的 Harmony 类型。一个补丁里只有一部分能迁时，只删已迁的方法，剩下的留在缺口清单。

### 场景进入

用 `ISceneListener`。`SceneId` 为 `Splash`、`Main`、`Day`、`PrepNight`、`Night`、`Staff`、`Result`。`Night` 是营业场景。

| 现有类型 | 迁到 |
| --- | --- |
| `MainSceneManagerPatch` | `OnSceneAwake(SceneId.Main)` |
| `DaySceneManagerPatch` | `OnSceneAwake(SceneId.Day)` |
| `NightSceneManagerPatch` 的 `Start` | `OnSceneStart(SceneId.Night)` |
| `PrepNightSceneManagerPatch` | `OnSceneStart(SceneId.PrepNight)` |
| `StaffSceneManagerPatch` | `OnSceneStart(SceneId.Staff)` |
| `ResultSceneManagerPatch` | `OnSceneStart(SceneId.Result)` |

`NightSceneManagerPatch.Dispose` 没有对应的场景停止回调。离开营业场景的清理由 `IWorkSceneGameLoop.Shutdown` 承担。对一下现有 `Dispose` 里做的事是否都能在 `Shutdown` 完成；不能的记入缺口。

### 监听

这些是通知，不要在里面替换原版流程，除非该补丁现在就是 `Prefix` 并且返回跳过。跳过改用上一节的 `SetXEnabled`。

| 现有类型 | 迁到 |
| --- | --- |
| `DayScenePlayerInputPatch` | `IDayInputListener` 的冲刺与互动 |
| `CharacterControllerInputGeneratorComponentPatch` | `IDayInputListener.OnMoveInput`、`OnCharacterReady`。先核对现有前缀是观察还是拦截 |
| `StatusTrackerPatch` | 邀请记录用 `IDaySceneGuestServices.RecordInvited`。若现有补丁是在原版记录之后再同步，用调用点附近的现有逻辑，不要重复记录 |
| `CookControllerPatch`、`CookSystemManagerPatch` | `ICookListener`。主动开煮、取出、存放、倒计时用 `IWorkSceneCook` |
| `IzakayaSelectorPanelPatch` | `IPrepListener.OnGuideMapConfirmed`、`OnGuideSpotSelected`。拦住确认用 `SetConfirmEnabled(false)` |
| `IzakayaConfigPannelPatch`、`IzakayaConfigurePatch` | `IPrepListener` 的确认、菜谱、饮料、厨具。主动改菜单用 `IPrepNightMenuServices`。确认出餐、进入营业用 `IPrepNightSessionServices.Confirm` 与 `ToWork` |
| `WorkSceneServePannelPatch` | `IWorkListener` 的上菜、刷新、送出。拦住关面板用 `SetCloseEnabled(false)` |
| `WorkSceneStoragePannelPatch` 的取出 | `IWorkListener.OnStorageExtracted` 或 `IPrepListener.OnFoodStored`。先读方法是备菜存放还是营业取出，不要两个都挂 |
| `GameTimeManagerPatch` | `IWorkListener.OnTimeModeChanged`、`OnTimelineDirectorPlayed`。主动改时制用 `IWorkSceneTime.SetMode` |
| `GuestsManagerPatch`、`GuestGroupControllerPatch`、`NormalGuestsControllerPatch`、`SpecialGuestsControllerPatch` | `IGuestGroupListener`。改仍会生成的客人用 `IGuestSpawnModifier` 的 `ref` 参数。改订单和评价用 `OnGroupOrdered`、`OnGroupEvaluated` 的 `ref` |
| `SellablePatch` | 只有在它改的是即将送出或存放的 `Sellable` 时，才挂到对应监听的 `ref` 或参数。没有 `ref` 的通知里不能把替换写回去 |

`GuestsManager__c__DisplayClass174_0Patch` 是订单生成。能放进 `OnGroupOrdered` 的放进去。它依赖编译器生成方法、而 `OrderBase` 与 `message` 不够表达时，整段留在缺口。

### 数据

ResourceEx 往游戏表里追加内容时，改成 `IDatabaseExtension` 的对应 `OnInject*`。代理字段与现有资源包字段逐项对照，缺的字段不要用反射补。

| 现有类型 | 迁到 |
| --- | --- |
| `DataBaseCorePatch` | 食材、食物、饮料、菜谱、厨具、道具、徽章中实际被追加的那些列表 |
| `DataBaseNightPatch` | `OnInjectNormalGuests`、`OnInjectSpecialGuests`、`OnInjectIzakayas` |
| `DataBaseDayPatch`、`RunTimeDayScenePatch` | `OnInjectNpcs`、`OnInjectMerchants`。`IsMerchant` 与 `RefMerchant` 若只是为了读刚注入的商人，注入后应能经游戏原表读到 |
| `DataBaseCharacterPatch` | 特殊客人与 NPC。`GetNPCLabel` 删除。`SetupPortrayalVisual` 改为立绘路径，不在这里换原版角色的图 |
| `DataBaseLanguagePatch`、`DaySceneLanguagePatch`、`NightSceneLanguagePatch` | 名称和说明写进对应代理的文本字段。语言表没有单独钩子 |
| `DialogPannelPatch`、`SpecialGuestDescriberPatch` | 先确认是不是只为了新注入的对话或描述。是，则数据放进 `OnInjectDialogs` 或客人描述字段，删除补丁 |

`DataBaseSchedulerPatch`、`DataBaseAchievementPatch`、`TrackedMissionDataPatch`、`DaySceneMapProfilePatch`、`CollabBehaviourComponentPatch` 不在 `IDatabaseExtension` 里。留在缺口。

### 营业循环

需要每帧驱动、或需要在营业开始时关开关的逻辑，放进 `IWorkSceneGameLoop`。白天地图切换用 `IDaySceneMapServices.Swap`。结束一天的拦截用 `SetEndEnabled`。主菜单进入白天用 `IMainSceneSessionServices.GotoDay`。关店用 `IWorkSceneIzakaya.Close`。

`UniversalGameManagerPatch` 的读档转场和加载场景：观察用 `IDayListener`；主动开关转场用 `SetNightTransitionEnabled`；主动加载用 `ICommonServices.LoadScene`。前缀里除了这些以外的逻辑留在缺口。

### 明确留在缺口

这些类型不要迁，也不要删：

- `SteamPlatformProfilePatch`
- `SaveManagementPatch`
- `YuyukoTimedNegativeSpellPatch`、`YuyukoPhase2GuestSpawnPatch`、`YuyukoPhase3GuestSpawnPatch`、`YuyukoChallengeContextPatch`、`YuyukoBossDataPatch`、`YuyukoMainLoopPatch`、`YuyukoTimingPatch`、`YuyukoLockCookersPatch`、`YuyukoRetakeContextPatch`、`YuyukoOnFailPatch`、`YuyukoExtraDialogData__c__DisplayClass4_0Patch`、`IncomeControllerYuyukoPatch`、`NightSceneDirectorPatch`
- `QTERewardManagerPatch`、`MystiaQTEBuffRewardPatch`
- `WorkSceneSustainedPannelPatch`、`WorkSceneCookingSelectionPannel__c__DisplayClass79_0Patch`、`UIManagerPatch`、`NightSceneEventManagerPatch`
- `DaySceneSustainedPannelPatch`、`DaySceneUIManagerPatch`、`DaySceneShopPannelPatch`、`DaySceneChatSelectionPannel__c__DisplayClass17_0Patch`、`NoteBookProfilePannelPatch`、`DaySceneMapPatch`
- `RunTimeAlbumPatch`、`RunTimeSchedulerPatch`
- `Plugin.cs` 里对 `CanvasScaler.Handle` 的补丁

每留一个，在缺口清单写：文件名、原方法、现有前缀是观察还是跳过、缺的是哪一种回调。不要写替代实现。

`HarmonyReversePatch` 全部删除，包括 `IzakayaConfigPannelPatch` 和 `MystiaQTEBuffRewardPatch` 里的反向补丁。需要调用原版时改走服务。服务不存在的，该项留在缺口，反向补丁也保留，并在清单标明“仍依赖反向补丁”。

## 实施顺序

1. 改工程和 `mod.json`，让解决方案在不改补丁行为的情况下先能编译。这一步允许暂时保留 Harmony，但新代码不准再加 Harmony。
2. 换入口：`IPostInitialize`、`ISceneListener`、主线程和 `PluginHost` 注册。
3. 按上一节的表迁监听、数据和服务。每迁完一组就编译。
4. 删掉已经没有引用的 Harmony 类型和 `PatchRegistry` 中的对应项。
5. 缺口清单写进 `docs/mystia-extension-port-gaps.md`。没有缺口就不要创建这个文件。
6. 全解决方案编译，并跑已有的 `MetaMystia.Network.Tests` 与 `MetaMystia.Flow.Tests`。

不要顺手重写网络协议、ResourceEx 包格式或控制台命令的对外行为。

## 测试

编译是必须的。游戏内操作不是本任务的默认验收；没有玩家实测时，结论写成“源码推断”。

必须通过：

- `dotnet test src/MetaMystia.Network.Tests/MetaMystia.Network.Tests.csproj`
- `dotnet test src/MetaMystia.Flow.Tests/MetaMystia.Flow.Tests.csproj`
- `dotnet build src/MetaMystia.Mod/MetaMystia.csproj`

静态检查：

- `src/MetaMystia.Mod` 中不再出现 `BepInEx`、`HarmonyLib`、`HarmonyPatch`、`BasePlugin`，缺口文件除外。缺口文件必须出现在缺口清单里。
- 不存在 `IGuestDirector` 的实现。
- 不存在在 `Setup`、`Update`、`Shutdown` 之外保存或调用场景服务的代码。
- `OnGroupOrdered` 与 `OnGroupEvaluated` 没有把 `ref` 参数传进 lambda。
- 新增特殊客人的立绘字段是路径，不是 `Sprite`，也没有改原版角色的立绘数组。

## 验收

对照 `Patches/PatchRegistry.cs` 的原列表。每一项只能是下面三种之一：

- 已迁移：写出新的接口和方法，旧类型已删除。
- 缺口：旧类型还在，且 `docs/mystia-extension-port-gaps.md` 有对应条目。
- 删除且无行为：仅当原补丁没有副作用，并在提交说明里指出原方法名。

以下行为在源码上必须仍能找到调用链。没有玩家实测时不要写成已在游戏里验证：

- 模组加载后会初始化配置、本地化和多人消息格式。
- 进入白天、备菜、营业、结算时，原先进场景补丁里的同步仍会执行。
- ResourceEx 追加的食材、菜谱、客人、NPC、商人通过 `IDatabaseExtension` 进入，而不是 Harmony 的 `Initialize` 后缀。
- 客人生成、入座、点单、评价、离店仍通知到现有多人发送点。
- 备菜改菜单、营业烹饪和上菜的发送点还在。
- 幽幽子挑战相关补丁仍按原文件存在，或缺口清单逐项列出。
- 直接启动游戏时本模组不会装上。这一点由中间件保证，本仓库不把文件写进游戏目录。

做完后给出一张三列对照：原补丁类型、归宿、是否已编译通过。不要把未跑的游戏流程写成通过。
