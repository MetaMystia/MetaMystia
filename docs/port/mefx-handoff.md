# 中间件工单（MystiaExtensionFramework）

读者：负责扩展中间件的执行者。配套：[`conventions.md`](conventions.md)、[`audit-facts.md`](audit-facts.md)、[`extension-api-plan.md`](../../../MystiaExtensionFramework/docs/extension-api-plan.md)（位于中间件仓库）。

通用规则：

- 观察与可拦截点是 `Mystia.Listeners` 里的 `[AutoWire]` 接口，桥接按玩家顺序跑管线。
- 命名约定：`OnPreXxx` = 可取消（`ref bool cancelInvocation`，`true` 表示不取消）；`OnXxx` = 通知，可能带 `ref` 写回，但没有取消。
- 服务只用于「模组主动发起的动作」与「宿主能力」；不引入联机、主客机等概念。
- 游戏私有成员经 Il2CppInterop 壳代码已是 `public`，桥接不接受模组传入 `Sprite`／引擎对象作为数据注入（数据面统一用相对路径字符串）。
- 桥接内已有 `StockGate`（每场景 `Reset` 的布尔开关 + `[ThreadStatic]` 放行计数 `Bypass`）与 `ServiceScope`（`[ThreadStatic]` 深度计数，只在 `Setup`/`Update`/`Shutdown` 内为真）。新增能力要复用这两套机制，不要另造。

## WP-M0 工具链

产出：`src/Mystia.InteropGen/Program.cs`（改）。

- `FindManaged` 除 `<game-project>/Build/Symbols/**/Managed` 外，也接受 `<game-project>/Library/ScriptAssemblies`（该目录须含 `Assembly-CSharp.dll`）。
- `UnityBaseLibsDir` 与 Source 分离：命令行可选参数或自动探测 Unity 编辑器 `Data/Managed/UnityEngine`，回退到 BepInEx `unity-libs`。
- 保持现有的 `GameAssembly.dll` 哈希校验与 `interop-manifest.json` 输出；输出目录默认 `artifacts/interop`。

验收：`dotnet run --project src/Mystia.InteropGen -- <game-project-dir> <game-install-dir>` 生成成功；`artifacts/interop` 里同时存在游戏程序集与 `UnityEngine.*` 代理，`Mystia.Net.Sdk` 与 `Mystia.Modding.Bridge` 能因 `GameApi/**`、`Game/**` 被纳入编译而构建通过（即 `Exists('$(MystiaInteropDir)Assembly-CSharp.dll')` 为真）。

## WP-M1 全局宿主与 IMGUI

产出：SDK `GlobalLoops.cs`、`Imgui.cs`；桥接 `GlobalHost.cs`。

```csharp
namespace Mystia;
[AutoWire]
public interface IGlobalGameLoop
{
    void Setup(IGlobalServices services) { }
    void Update(IGlobalServices services, float delta) { }
    void FixedUpdate(IGlobalServices services, float delta) { }
    void Shutdown(IGlobalServices services) { }
}
public interface IGlobalServices { ICommonServices Common { get; } }

[AutoWire]
public interface IIMGUIProvider { void OnGui(IIMGUIDrawer drawer); }

public interface IIMGUIDrawer
{
    Vector2 ScreenSize { get; }
    Color Color { get; set; }
    GUIStyle Skin { get; }
    Event Current { get; }
    // 逐成员转发 GUI / GUILayout / GUIUtility，至少包含 Label / Button / TextField /
    // DrawTexture / BeginScrollView / EndScrollView / BeginHorizontal / EndHorizontal /
    // BeginVertical / EndVertical / SetNextControlName / FocusControl / FocusedControl /
    // keyboardControl / GetStateObject<T>
}
```

挂点：常驻宿主组件 `MainThreadPump`（`src/Mystia.Modding.Bridge/Game/RuntimeInstall.cs`）的 `Update` 里追加全局循环调度；`FixedUpdate` 需要在同一宿主组件上新增方法。`IGlobalGameLoop` 的生命周期为「模组加载后 Setup，进程退出前 Shutdown」，跨场景常驻，且**不在** `ServiceScope` 内。

验收：中间件解决方案编译通过；`samples/` 下新增或改一个样例实现 `IGlobalGameLoop` 与 `IIMGUIProvider`，能被宿主发现（不要求游戏内运行）。

## WP-M2 协程

产出：SDK `Coroutines.cs`；桥接 `CoroutinePump.cs`。

```csharp
namespace Mystia;
public readonly struct CoroutineHandle : IEquatable<CoroutineHandle> { }  // 不透明，无公共构造
public readonly struct CoroutineAwait { }                                 // 不透明，可直接 yield

public interface ICoroutineDispatcher
{
    CoroutineHandle Start(Func<ICoroutineDispatcher, IEnumerator> routine);
    CoroutineHandle StartOn(Component owner, Func<ICoroutineDispatcher, IEnumerator> routine);
    void Stop(CoroutineHandle handle);
    void StopAll();
    CoroutineAwait NextFrame { get; }
    CoroutineAwait AfterSeconds(float seconds);
    CoroutineAwait AfterSecondsRealtime(float seconds);
    CoroutineAwait AfterFixedUpdate { get; }
    CoroutineAwait AtEndOfFrame { get; }
    CoroutineAwait Until(Func<bool> condition);
    CoroutineAwait While(Func<bool> condition);
    CoroutineAwait Nested(Func<ICoroutineDispatcher, IEnumerator> routine);
}
```

实现要点：

- 自建托管泵，挂在常驻宿主组件上（与 WP-M1 同一个 `MainThreadPump`，首次协程启动或首次全局循环时确保宿主存在）。
- 泵需解释的 yield 值：`null`、`CoroutineAwait`、`WaitForSeconds`、`WaitForSecondsRealtime`、`WaitForEndOfFrame`、`WaitForFixedUpdate`、嵌套 `System.Collections.IEnumerator`、`Il2CppSystem.Collections.IEnumerator`。不要依赖把托管枚举器转成 Il2Cpp 委托。
- `StartOn(owner)` 的存活检查由泵承担：owner 已销毁则停止该协程。`StopAll` 只停本模组自己的协程。
- 协程异常不得吞掉：泵里捕获后记录到 `ILog`，并停止该协程。

验收：编译通过；样例模组里用 `Start`/`StartOn`/`Stop` 与四种等待令牌各跑一条（源码层面即可）。

## WP-M3 宿主能力

产出：SDK `ModStorage.cs`、`DialogCatalog.cs`、`GuestRecords.cs`、`ILog` 扩展；桥接 `DialogCatalogHost.cs`。

```csharp
namespace Mystia;
public enum LogLevel { Debug, Info, Message, Warning, Error, Fatal }
public interface ILog
{
    // 现有成员保留
    void Message(string message);
    void Fatal(string message);
    void Log(LogLevel level, string message);
    string Id { get; }
    string Version { get; }
}
public interface IModCache      // 读写，位置对模组隐藏
{
    bool Exists(string relativePath);
    Stream OpenRead(string relativePath);
    Stream OpenWrite(string relativePath);
    TextReader OpenText(string relativePath);
    TextWriter CreateText(string relativePath);
    void Delete(string relativePath);
}
public interface IModConfigSource   // 只读
{
    bool Exists(string relativePath);
    Stream OpenRead(string relativePath);
    TextReader OpenText(string relativePath);
}
public interface IDialogCatalog
{
    IReadOnlyList<string> Names { get; }
    bool TryResolve(string name, out DialogPackage package);
}
public interface IGuestRecords      // 作用域无关
{
    void RecordInvited(int guestId);
    bool HasInvited(int guestId);
    bool IsIgnored(int guestId);
    void Reset();
}
```

`ICommonServices` 追加（这些成员**不得**调用 `ServiceScope.Require()`）：

```csharp
ICoroutineDispatcher Coroutines { get; }
IModCache Caching { get; }
IModConfigSource Config { get; }
ILog Log { get; }
IDialogCatalog Dialogs { get; }
IGuestRecords Records { get; }
void OpenDialog(DialogPackage dialog, Action onFinished, Action<IDictionary<int, string>>? replaceText = null);
```

实现要点：

- `Caching` 与 `Config` 的根目录由宿主决定（模组目录下的固定子目录），实现用 `FileStream`；`relativePath` 必须做规范化并拒绝越出根目录的路径。
- `Dialogs.Names` 来自 `DataBaseDay.allDialogPackages` 的键；`TryResolve` 用同一张表取实例（桥接内部已有等价查找，暴露即可）。
- `Records` 包装 `StatusTracker`（纯 C# 单例，无场景依赖）：`RecordInvited` → `RecordInvitedGuest`，`HasInvited` → `HasNPCInvited`，`IsIgnored` → 忽略名单。
- `OpenDialog` 的 `replaceText` 由桥接转成原版的 `Il2CppSystem.Action<Dictionary<int,string>>`（与 `DialogScripts.Fill` 同一机制）。
- `ILog.Id`/`Version` 来自宿主加载该模组时的 `mod.json`。

验收：编译通过；`ICommonServices` 新成员在 `CommonServices.Shared` 上可用且不触发作用域检查。

## WP-M4 监听管线

产出：SDK `Listeners.cs`（改）；桥接新增 `ListenerSeams.cs`（不要改 `HarmonySeams.cs`）。

```csharp
public interface IDayListener
{
    void OnDayFirstEntered() { }
    void OnDialogOpened(DialogPackage package) { }
}
public interface IDayInputListener
{
    void OnMoveInput(CharacterControllerUnit unit, Vector2 direction) { }
}
public interface IWorkListener
{
    void OnServePanelOpened(WorkSceneServePannel panel) { }
    void OnPreServePanelClosed(WorkSceneServePannel panel, ref bool cancelInvocation) { }
    void OnPreDishServed(WorkSceneServePannel panel, ref Sellable dish, ref bool cancelInvocation) { }
    void OnPreDishCancelled(WorkSceneServePannel panel, ref Sellable dish, ref bool cancelInvocation) { }
    void OnPreStorageExtracted(ref Sellable sellable, ref bool cancelInvocation) { }
    void OnPreTimeModeSet(GameTimeManager manager, ref GameTimeManager.TimeMode mode, ref bool cancelInvocation) { }
}
public interface ICookListener
{
    void OnPreCookStarted(CookController controller, ref Sellable result, ref Recipe recipe, ref bool cancelInvocation) { }
    void OnPreCookCountdownStarted(CookController controller, ref float qteScore, ref bool cancelInvocation) { }
}
public interface IPrepListener
{
    void OnPreRecipeAdded(int id, ref bool cancelInvocation) { }
    void OnPreBeverageAdded(int id, ref bool cancelInvocation) { }
    void OnPreCookerAssigned(int id, int index, ref bool cancelInvocation) { }
    void OnConfigTabSelected(IzakayaConfigPannel panel) { }
    void OnConfigureUpdated(IzakayaConfigure configure) { }
}
public interface IGuestGroupListener
{
    void OnGroupOrderGenerated(GuestGroupController group, GuestsManager.OrderGenerationResult result, ref GuestsManager.OrderBase order) { }
    void OnGroupArrived(GuestGroupController group) { }
    void OnGroupMovingToDesk(GuestGroupController group, int desk) { }
    void OnGroupQueued(GuestGroupController group) { }
}
[AutoWire]
public interface IPortraitProvider
{
    bool TryResolvePortrait(GameData.Profile.ClothesProfile.Clothes clothes, out Sprite sprite);
}
```

挂点表（游戏侧均为源码事实，行号见 `audit-facts.md`）：

| 接口成员 | 目标方法 | 种类 | 要点 |
| --- | --- | --- | --- |
| `OnDayFirstEntered` | `DayScene.SceneManager.OnFirstEnterDaySceneFinish` | Postfix | 与现有 `IDayListener.OnDayMapEntered`（`RunTimeScheduler.OnEnterDaySceneMap`）语义不同，不可合并 |
| `OnDialogOpened` | `UniversalGameManager.OpenDialogMenu` | Postfix | 传实例；同方法上已有桥接 Prefix（处理夜转场与文案替换），不要冲突 |
| `OnMoveInput` | `CharacterControllerInputGeneratorComponent.UpdateInputDirection` | Postfix | 必须带来源实例；同方法上已有 `InputHolds.Move` Prefix 做输入冻结 |
| `OnServePanelOpened` | `WorkSceneServePannel.OnPanelOpen` | Postfix | 原版在此清空它自己的待上菜字段，通知必须在其之后 |
| `OnPreServePanelClosed` | `WorkSceneServePannel.OnPanelClose` | Prefix | 原版此处即结算入口；同方法上已有 `ServeHolds.Close` Prefix（托盘关闭开关），两者顺序需固定并记录 |
| `OnPreDishServed` | `WorkSceneServePannel.Send` | Prefix | 可取消 |
| `OnPreDishCancelled` | `WorkSceneServePannel.Cancel` | Prefix | 可取消 |
| `OnPreStorageExtracted` | `WorkSceneStoragePannel.Extract` | Prefix | 可取消 |
| `OnPreTimeModeSet` | `GameTimeManager.SetGameTimeMode` | Prefix | 可改写 `mode`；现有 `IWorkListener.OnTimeModeChanged` 是 Postfix 通知，两者并存 |
| `OnPreCookStarted` | `CookController.SetCook` | Prefix | 可改写 `result`/`recipe`；现有 `ICookListener.OnCookStarted` 是 Postfix 通知 |
| `OnPreCookCountdownStarted` | `CookController.StartCookCountDown` | Prefix | 可改写 `qteScore` |
| `OnPreRecipeAdded` 等三项 | `IzakayaConfigure.RegisterToDailyRecipes` / `RegisterToDailyBeverages` / `RegisterToCookers` | Prefix | 现有对应监听是 Postfix 通知，两者并存 |
| `OnConfigTabSelected` | `IzakayaConfigPannel.GoToSpecific` | Postfix | 新增 seam，桥接现无此挂点 |
| `OnConfigureUpdated` | `IzakayaConfigure.Initialize`、`IzakayaConfigure.UpdateValue` | Postfix | 新增 seam |
| `OnGroupOrderGenerated` | `GuestGroupController.GenerateOrder` 与订单生成结果 | — | 与现有 `OnGroupOrdered(ref OrderBase, ref string)` 并存；`OrderGenerationResult` 必须可读。现有实现打在 `GuestGroupController.GenerateOrder`，本项要覆盖闭包局部函数处的 `OrderGenerationResult`（见未决疑点 2） |
| `OnGroupArrived` | `GuestGroupController.RefreshCurrentFundAndOrder` | Postfix | 通知 |
| `OnGroupMovingToDesk` | `GuestGroupController.MoveToDesk` | Prefix | 通知；同方法上已有 `SeatAndOrder.SeatSeams.Move` 改座位号 |
| `OnGroupQueued` | `GuestGroupController.MoveToQueue` | Postfix | 通知 |
| `IPortraitProvider` | `DataBaseCharacter.SetupPortrayalVisual` | Prefix | 按玩家顺序询问，命中则写 `overrideSprite` 并放行原版（保留原版动画立绘协程）；clothes 取当前已加载的服装数据 |
| （待定）`OnPreDayFastForward` | `DayScene.UI.DaySceneSustainedPannel.OnFastForwardSubmit` | Prefix | 候选形状：`void OnPreDayFastForward(DaySceneSustainedPannel panel, ref bool cancelInvocation)`。语义是「快进到营业」，任何模组都可能需要拦截；**施工前需先做一次该方法的源码核对**，确认取消后游戏仍停在白天（见未决疑点 5） |
| （待定）聊天菜单扩展 | `DayScene.UI.UIManager.OpenAfterChatMenu` 的回调数组 | Prefix | 目标是让模组向「白天地图交互后的聊天菜单」追加可选项。候选形状：`[AutoWire] IChatMenuProvider { IEnumerable<ChatMenuEntry> ProvideEntries(); }`，由桥接把条目转成原版的 `GetSelectionConfigurationCallback` 并追加。**接口形态未定**，需先核对 `OpenAfterChatMenu` 与 `DaySceneChatSelectionPannel` 的构造方式（见未决疑点 5） |

验收：编译通过；`OnPre*` 的取消语义在一个样例里验证（取消后原版不执行）；`ref` 参数不进 lambda。

## WP-M5 服务与开关

产出：SDK `SceneLoops.cs`（改）；桥接 `SceneServices.cs`（改）。

```csharp
IDaySceneMapServices : void Swap(string mapLabel, string markerName, int travelCount, Action onFinished = null);
IWorkSceneGuests     : void SetSeatingEnabled(bool enabled);
                       void BeginOrderSession(GuestGroupController group, GuestsManager.OrderGenerationResult result, GuestsManager.OrderBase order);
IWorkSceneCook       : void SetCallEnabled(bool enabled);
IWorkSceneIzakaya    : void SetCloseEnabled(bool enabled);
```

实现要点：

- `SetSeatingEnabled(false)` 覆盖 `GuestsManager.TrySendToSeat` 与 `GuestsManager.CheckAndSendFromQueue` 两条自动入座路径；模组自己的 `Seat(...)` 经 `StockGate.Bypass` 自动放行。
- `SetCloseEnabled(false)` 覆盖 `GuestsManager.TryCloseIzakaya`；`Close()` 主动关店同样经 `Bypass`。
- `SetCallEnabled(false)` 覆盖 `CookSystemManager.CallCooker`；`IWorkSceneCook.Start` 等主动调用不受影响。
- 新开关要进 `StockGate.Reset(scene)` 的对应场景分支（入座、关店、开厨属营业场景）。
- `Swap` 的 `onFinished` 由桥接合并进原版 `SwapMap` 的 `onSwapFinish` 回调，不得吞掉原回调。
- `BeginOrderSession` 走桥接既有的待处理订单机制（`OrderHolds` 的 `PendingOrder`）：注入后下一次订单生成直接采用给定订单与结果。
- `OpenDialog` 重载见 WP-M3。

不加的开关（依据源码事实，写进代码注释以免后人重复提案）：`SetLeaveEnabled` 已覆盖全部离场路径（`PayAndLeave`、`ExBadLeave`、`RepellAndLeavePay`、`RepellAndLeaveNoPay`、`PlayerRepell`、`PatientDepletedLeave`、`LeaveFromDesk`）；`SetOrderingEnabled` 已覆盖 `GenerateOrderSession` 与 `GenerateOrder`，而 `MainOrderCycle` 只是转调 `GenerateOrderSession`。

验收：编译通过；每个新开关在样例里至少验证「关掉后原版路径不执行、服务主动调用仍可执行」。

## WP-M6 数据注入

产出：SDK `Database.cs`（改）、`Portraits.cs`；桥接 `DatabaseInject.cs`（改）、`MerchantPipeline.cs`、`PortraitProviders.cs`。

新增 `IDatabaseExtension` 钩子：

```csharp
void OnInjectClothes(List<ClothesData> clothes) { }
void OnInjectSpells(List<SpellData> spells) { }
void OnInjectBuffs(List<BuffData> buffs) { }
void OnInjectMissionNodes(List<MissionNodeData> nodes) { }
void OnInjectEventNodes(List<EventNodeData> nodes) { }
void OnInjectDayMaps(List<DayMapData> maps) { }
```

新增 seam：`DataBaseNight.Initialize`、`DataBaseScheduler.Initialize`、`DaySceneLanguage.Initialize`（都在 Postfix 注入，因为这些初始化会整体重建目标表）。

结构补充（字段级清单见 `audit-facts.md` 第 4 节）：`NpcData.Name`/`Description`、`SpecialGuestData` 的 `Kind`/`IsParticular`/`IsCollabCharacter`/`HideInAlbum`/`CommisionAreaLabel`/`SpawnMarker`/`Kizuna`、`GuestRequestLine.Enable`、`DialogLine.Actions` 与 5 个行级 flag，以及 `ClothesData`/`SpellData`/`BuffData`/`MissionNodeData`/`EventNodeData`/`DayMapData`/`SpecialGuestKizunaData`/`DialogActionData`/`DialogBranchOptionData`/`SpawnMarkerData`。

实现要点：

- 3 张映射表 `FoodsMapping`/`BeveragesMapping`/`RecipesMapping` 写入**注入模组自己的 id**（宿主从 `mod.json` 的 `id` 提供），不得硬编码任何模组名。
- NPC 名：注入时除现有标签映射外，还要写日间场景语言的 NPC 名表与反向标签表，否则标签反查会抛异常或回落成占位文本（事实见 `audit-facts.md`）。
- 商人：除写入基础商人表外，必须建立运行时追踪条目并生成商品，缺键查询安全返回，并按原版等价规则过滤玩家已拥有的食谱（原版由商人专属行为组件完成，注入的商人没有该脚本）。
- 立绘：`OnInjectClothes` 只接受相对路径；运行时立绘由 `IPortraitProvider`（WP-M4）承担。
- 注入时机沿用现有 `Apply*` 分批方式，新增 seam 各自对应一批。

验收：编译通过；样例模组注入一条食材、一条特殊客人（含立绘路径）、一个商人、一个地图，均能编译；映射表在源码层面确实被写入。

## 未决疑点（施工前确认或带回主线）

1. 备菜确认回调：模组打 `_SolveDailyCompletion_b__64_7`，桥接打 `_SolveDailyCompletion_b__61_7`。以游戏工程当前版本核对后再定，可能是编译器生成名差异。
2. `OnGroupOrderGenerated` 需要的 `OrderGenerationResult` 位于闭包局部函数内部，桥接如何在既有落点读到它，需要对 `GuestsManager.GenerateOrderSession` 做一次 IL/源码核对。
3. `IIMGUIDrawer` 的成员范围：按模组实际使用面与 `GUI`/`GUILayout` 公开成员对齐，最终清单实现时冻结。
4. 注入商人的「孤儿清理」是否由框架提供每日钩子承担——建议不提供，继续由模组用场景循环 `Setup` 自行处理，保持框架通用。
5. 两个待定挂点需要一次定向源码核对后再定形状：**快进提交拦截**（`DaySceneSustainedPannel.OnFastForwardSubmit`）与**聊天菜单扩展**（`UIManager.OpenAfterChatMenu` 的回调数组 / `DaySceneChatSelectionPannel` 的菜单构造）。核对前不得自行定型，也不得用 Harmony 顶替。
