# 下一轮中间件扩展工单（W0–W7 / WV / WY）

本轮目标：把 `Patches/Compat/` 里除幽幽子族与地图本体以外的补丁全部退役，改为中间件监听/服务/数据面表达。

约定（沿用 `conventions.md`）：

- 可拦截钩子：`void OnPreXxx(..., ref bool cancelInvocation)`（`true` = 取消原版调用）。
- 监听/上下文结构一律 `readonly record struct`；可能被模组改写的请求/奖励一律可变 `record struct`；不透明句柄（`CoroutineHandle`/`CoroutineAwait`）保持手写。
- 公开 API 不传游戏面板：需要"读 + 操作"时由桥接实现一个视图类（见 WV）。
- 服务只用于模组主动发起的动作；数据一律走 `IDatabaseExtension`。
- 符卡行为由模组实现 `ISpell`；场景能力不够时**扩展 `IWorkSceneServices`**，不开符卡专用便利方法。

## W1 通知类（低风险）

```csharp
namespace Mystia.Listeners;

[AutoWire] public interface ISessionListener
{
    void OnPlayerDataLoading() { }
    void OnGameStatusResetting(bool rewindingDay) { }
    void OnSceneLeaving(Scene target) { }
}

[AutoWire] public interface IStatusListener
{
    void OnPlayerSkinChanged(int skinId) { }
    void OnIzakayaDecorationChanged(int decorationId) { }
}

[AutoWire] public interface IMissionListener
{
    void OnMissionFinishStatesUpdating(RunTimeScheduler.TrackedMissionData mission, ref bool[] finishStates) { }
}

[AutoWire] public interface IDayUiListener
{
    void OnShopPannelOpened(ShopPannelView view) { }
    void OnPreFastForward(ref bool cancelInvocation) { }
}

[AutoWire] public interface IWorkUiListener
{
    void OnHudOpened() { }
}

// 评价之后（补 IGuestGroupListener；挂 NormalGuestsController/SpecialGuestsController.PostEvaluation 的 Postfix）
// 由 WV 一并改签名时加入 IGuestGroupListener；本轮先作为独立接口避免文件冲突：
[AutoWire] public interface IGuestPostEvaluationListener
{
    void OnGroupPostEvaluated(GuestGroupController group, GuestGroupController.EvaluationResult result) { }
}

// LeaveFromDesk 通知派发（补现有 GuestSeams 的缺口，使 GuestLeaveKind.Other 可收到）
// 无新接口；改桥接 HarmonySeams 的离场重入计数内新增派发。

// DLC 查询（查询式，无监听）：IModContext 增加
public interface IPlatformInfo
{
    bool KeysResolved { get; }
    IReadOnlyList<string> ActiveDlcKeys { get; }
}
```

## W2 数值与时间

```csharp
namespace Mystia.Listeners;

[AutoWire] public interface IWorkMetricsListener
{
    void OnPreFundEdit(ref int amount, ref bool cancelInvocation) { }
    void OnFundEdited(int amount) { }
    void OnPreTipEdit(ref int amount, ref bool cancelInvocation) { }
    void OnTipEdited(int amount) { }
    void OnPreExperienceEdit(ref int amount, ref bool cancelInvocation) { }
    void OnExperienceEdited(int amount) { }
    void OnPrePassionEdit(ref int amount, ref bool cancelInvocation) { }
    void OnPassionEdited(int amount) { }
}
```

```csharp
namespace Mystia.Scenes;

public interface IWorkSceneEconomyServices
{
    void EditFund(int amount);
    void EditTip(int amount);
    void EditExperience(int amount);
    void EditPassion(int amount);
    void SetPopularityTagsEnabled(bool enabled);
}

// 扩展 IWorkSceneTime
public interface IWorkSceneTime
{
    int WholeNightSeconds { get; set; }      // 写穿游戏 GetWholeNightTime
    void SetTimingEnabled(bool enabled);     // 门控 StartGuestSpawningAndTiming
    void BeginTiming();                      // 用原参数启动一次（Bypass）
    void SetMode(GameTimeManager.TimeMode mode);
}
```
`SetSpawnEnabled(false)` 语义扩展：覆盖普通/挑战/造物者之盒三类自动刷客协程（实现修复，不新增成员）。

## W3 QTE 与奖励 buff

```csharp
namespace Mystia.Listeners;

public enum RewardBuffKind { ThrowDeliver, InstantEvaluation, PatientFreeze, Fever, InfiniteFever }

public readonly record struct QteReward(int Index, bool MustSuccess, RewardBuffKind? Buff);

[AutoWire] public interface IQteListener
{
    void OnPreQteSucceeded(ref QteReward reward, ref bool cancelInvocation) { }
    void OnQteSucceeded(in QteReward reward) { }
    void OnRewardBuffTriggered(RewardBuffKind kind) { }
}
```

```csharp
namespace Mystia.Scenes;

public interface IQteServices
{
    void ApplyQteReward(in QteReward reward);   // Buff 非空 → 触发对应 buff；否则重放 OnQTESucceeded(Index, MustSuccess)
    void TriggerRewardBuff(RewardBuffKind kind, bool infinite = false);
}
```
（`SetPopularityTagsEnabled` 已并入 W2 的 `IWorkSceneEconomyServices`。）

## W4 日程与聊天

```csharp
namespace Mystia.Listeners;

[AutoWire] public interface IScheduleListener
{
    void OnPreScheduleEvent(ref string eventLabel, ref bool cancelInvocation) { }
    void OnSchedulerEventEntered(string label) { }
    void OnSchedulerEventExited(string label) { }
    void OnPreRewardProcessed(ref bool cancelInvocation) { }
    void OnPreDayEnd(ref Action onFinished, ref bool cancelInvocation) { }
    void OnPreAfterDayEnd(ref Action onFinished, ref bool cancelInvocation) { }
}

public enum ChatOption { FreeChat, Shop, Mission, Invite, RequestIngredient, RequestBeverage, Commission }

public readonly record struct ChatOptionContext(
    ChatOption Option, string? CharacterLabel, CharacterKind Kind, int CharacterId,
    bool IsMerchant, bool HasMerchantData, int ProductCount, bool HasChatData, bool IsIgnored);

[AutoWire] public interface IChatOptionListener
{
    void OnChatOptionAvailability(in ChatOptionContext context, ref bool available) { }
}

public enum ChatMenuSource { Interaction, DirectChat }

public readonly record struct ChatMenuContext(
    ChatMenuSource Source, string? CharacterLabel, CharacterKind Kind, int CharacterId, bool IsMerchant);

public readonly record struct ChatMenuEntry(string Label, bool Available, Action OnSelected);

[AutoWire] public interface IChatMenuProvider
{
    void ProvideChatMenuEntries(in ChatMenuContext context, IList<ChatMenuEntry> entries);
}
```

## W5 选菜提交与符卡

```csharp
namespace Mystia.Listeners;

public record struct CookingRequest(Recipe Recipe, int[]? IngredientIds, bool FromSelectionPanel);

[AutoWire] public interface ICookSelectionListener
{
    void OnPreCookingSubmit(ref CookingRequest request, ref bool cancelInvocation) { }
}
```

```csharp
namespace Mystia.Spells;

[AutoWire] public interface ISpell
{
    int SpellId { get; }
    IEnumerator? Positive(IWorkSceneServices scene, ICoroutineDispatcher coroutines, ILog log) => null;
    IEnumerator? Negative(IWorkSceneServices scene, ICoroutineDispatcher coroutines, ILog log) => null;
}
```
数据面：`SpellData` 去掉 `Type`，补 `PortraitPivotX/PortraitPivotY`。桥接 `BridgeSpell : SpellBase` 持有 `ISpell`，用 `CoroutinePump` 驱动托管协程，返回桥接实现的 `Il2CppSystem.Collections.IEnumerator` 适配器（把模组 `SpellBaseEx` 里移植的 `ManagedEnumerator` 收进中间件成为公共能力）；执行期间桥接 `ServiceScope.Enter()/Exit()` 包住，使 `scene` 可用。

## W6 点位注入

- 数据面已存在：`DayMapData.SpawnMarkers`、`SpecialGuestData.SpawnMarker`（桥接目前未消费）。
- 桥接新增 `SpawnMarkerPipeline`：Postfix `DaySceneMap.GenerateSpawnMarkerData` 建缺失的 `SpawnMarker` 引擎对象（挂 `spawnMarkerField`、写 `AllSpawnMarkers`/`allSpawnMarkerLabels`）；Prefix `DaySceneMap.SolveAndUpdateCharacterPositionInternal` 按 `SpawnMarker.Label` 定位注入的 NPC/稀客（含缺键跳过）。
- 服务：`IDaySceneMapServices.RefreshSpawnMarkers()`；换图后由桥接自动调用一次。

## WV 面板 → 代理视图

```csharp
namespace Mystia.Scenes;

public sealed class ServePannelView
{
    public GuestGroupController Guest { get; }
    public GuestsManager.OrderBase Order { get; }
    public int DeskCode { get; }
    public Sellable? PendingFood { get; set; }
    public Sellable? PendingBeverage { get; set; }
    public void RefreshPendingVisual();
}

public sealed class GuideMapView
{
    public string? SelectedMapLabel { get; }
    public int SelectedLevel { get; }
    public void RefreshSelection();
}

public sealed class PrepConfigView
{
    public int SelectedTab { get; }
    public void Refresh();
}

public sealed class ShopPannelView
{
    public bool HasProducts { get; }
    public int ProductCount { get; }
    public void ClearCustomSpacing();
}
```

签名替换：`IWorkListener` 的 7 个 serve-panel 成员、`IPrepListener` 的 4 个 prep-panel 成员、`IDayUiListener.OnShopPannelOpened` 全部换成对应视图。模组侧同步改 `Listeners/WorkSync.cs`、`Listeners/PrepSync.cs`、`Managers/GuestFSM.cs` 里的面板引用。

## 批次的合并点

每批：框架 0 错 → SDK 测试 21/21 → 重打包 SDK 并清缓存 → 模组 0 错 → Network.Tests 214 / Flow.Tests 103 → 静态检查 8/8 → 删除对应 Compat 文件 → 复验。

---

# 修订 v5（按挂点核对结果，覆盖上文冲突处）

以下条目**以本节为准**，上文对应声明作废。

## W2 数值：游戏是 float + 运算类型，且是"前缀返回 false 取消"而非 ref cancel 形参

```csharp
[AutoWire] public interface IWorkMetricsListener
{
    void OnPreFundEdit(ref float value, ref EventManager.MathOperation operation, ref bool cancelInvocation) { }
    void OnFundEdited(float value) { }
    void OnPreTipEdit(ref int value, ref EventManager.ServeType serveType, ref float comboBuff,
                      ref float moodBuff, ref float extraBuff, ref bool cancelInvocation) { }
    void OnTipEdited(int value, EventManager.ServeType serveType, float comboBuff, float moodBuff, float extraBuff) { }
    void OnPreExperienceEdit(ref float value, ref EventManager.MathOperation operation, ref bool cancelInvocation) { }
    void OnExperienceEdited(float value) { }
    void OnPrePassionEdit(ref float value, ref EventManager.MathOperation operation, ref bool cancelInvocation) { }
    void OnPassionEdited(float value) { }
}
```
服务（重放侧）同步改成：
```csharp
public interface IWorkSceneEconomyServices
{
    void EditFund(float value, EventManager.MathOperation operation = EventManager.MathOperation.Add);
    void EditTip(int value, EventManager.ServeType serveType, float comboBuff = 0f, float moodBuff = 0f, float extraBuff = 0f);
    void EditExperience(float value, EventManager.MathOperation operation = EventManager.MathOperation.Add);
    void EditPassion(float value, EventManager.MathOperation operation = EventManager.MathOperation.Add);
    void SetPopularityTagsEnabled(bool enabled);
}
```
`SetSpawnEnabled` 的语义扩展需注意：现有 `NightSceneEventManagerPatch` 的循环前缀还承担"作弊流速=0 时跳过刷客"，这与"联机抑制刷客"不是同一件事——桥接扩展 `SetSpawnEnabled` 覆盖三类循环时**不得**把作弊语义一起吞掉，模组侧对作弊场景另行处理（W2 实现时在模组监听里按 `ConfigManager.CheatFlowRate == 0` 自行调 `SetSpawnEnabled(false)`，保持等价）。

新增硬缺口（必须补接口，否则 `ModifyTotalTime_Prefix` 无法退役）：
```csharp
public interface IWorkSceneIzakaya
{
    void SetCloseEnabled(bool enabled);          // 语义扩展：覆盖 TryCloseIzakaya **与** 倒计时归零触发的自动打烊
    void SetTimeCloseEnabled(bool enabled);      // 若语义扩展不可行，则单列此项门控 ModifyTotalTime 路径
    void Close();
}
```

## W1 修订

- **`OnSceneLeaving` 取消**：它与既有 `IDayListener.OnSceneChanging` 同方法同参数，桥接**并入同一个 Postfix** 派发，不新建 patch class。`ISessionListener` 只保留 `OnPlayerDataLoading` / `OnGameStatusResetting(bool rewindingDay)`。
- **`IStatusListener` v2**：装饰切换分增删两条真实入口，故加 `bool`：
```csharp
[AutoWire] public interface IStatusListener
{
    void OnPlayerSkinChanged(int skinId) { }                     // RunTimeAlbum.ChangePlayerSkin Postfix
    void OnIzakayaDecorationChanged(int decorationId, bool added) { } // TryRecordUsedDecoration / TryRemoveUsedDecoration Postfix
}
```
- **`IDayUiListener.OnShopPannelOpened`**：视图类 `ShopPannelView` 是**中间件自己实现的类型**（游戏里不存在），桥接在 `DaySceneShopPannel.OnPanelOpen` 的 Postfix 构造它；`HasProducts`/`ProductCount` 由桥接读面板得到，`ClearCustomSpacing()` 内部转调 `ReceivedObjectDisplayerController.TryRemoveCustomSpacing<DaySceneShopPannel>()`。
- **`IWorkUiListener.OnHudOpened`**：桥接必须用 `typeof(NightScene.UI.UIManager)` 全名（`DayScene.UI.UIManager` 同名）。
- **DLC 查询**：`IPlatformInfo` 挂在 `IModContext` 上（成员名 `Platform`），模组在 `IPostInitialize` 之后读；`KeysResolved` 为假时按现有 `Core.cs:106` 的兜底路径处理。

## W3 修订

- `QteReward(int Index, bool MustSuccess, RewardBuffKind? Buff)` 保留；`Buff` 由桥接在可判定时填入，不可判定则为 `null`，**模组不应依赖它**——缓冲触发一律用独立的 `OnRewardBuffTriggered(RewardBuffKind)`（桥接对 5 个奖励方法各挂 Postfix）。
- `TriggerRewardBuff` 由桥接实现（内部可用 `AccessTools` 调私有方法；`InfiniteFever` 是唯一 public）。

## W4 修订

- `IScheduleListener` 保留：`OnPreScheduleEvent(ref string eventLabel, ref bool cancelInvocation)`、`OnPreRewardProcessed(ref bool cancelInvocation)`、`OnPreDayEnd(ref Action onFinished, ref bool cancelInvocation)`、`OnPreAfterDayEnd(ref Action onFinished, ref bool cancelInvocation)`。
- **取消** `OnSchedulerEventEntered/Exited`（落点无法确证）。
- 新增重放能力（否则 `ProcessReward_Prefix` 的重放路径无法表达）：
```csharp
public interface IDaySceneScheduleServices
{
    void SetEndEnabled(bool enabled);
    void End();
    void Chat(string characterLabel);
    void ReplayReward(in SchedulerNode.Reward reward);   // 形状在 W4 实现前按模组用法最终确认
}
```
- **灵梦保护窗口（`ReimuProtection` 局部函数）没有可用落点**：目标 `RunTimeScheduler.Method_Internal_Static_Void_Action_PDM_0` 是编译器生成的局部函数，`ScheduleEvent` 语义也不等价。该项**保持 Compat 缺口**，直到有明确设计（见待确认第 3 点）。
- `IChatOptionListener`：`PDM_0/1`（FreeChat/Shop）已确证；`Mission..Commission` 的序号未确认，W4 实现时先探明再挂，未探明的不挂。

## W5 修订

- 选菜提交目标是编译器生成闭包 `__c__DisplayClass79_0`（`g__OnSubmit`），桥接按字符串名挂 Prefix；`CookingRequest` 的 `IngredientIds` 由桥接从闭包 `selectedIngredients` 读取（`List<int>`），与 `solved.Modifiers` 的关系在实现时确认。
- 符卡：`SpellBase` 必覆写 `OnGettingSpellOwnerIdentifier`/`OnPositiveBuffExecute`/`OnNegativeBuffExecute`，可选 `OnLeaveBuffExecute`/`Has*Spell`/`ShouldProtectedByShield`/`ShouldCallSpellDeclarationAuto`。桥接 `BridgeSpell` 持有 `ISpell`，在执行期用 `CoroutinePump` 驱动托管协程，并返回桥接实现的 `Il2CppSystem.Collections.IEnumerator` 适配器（原模组 `ManagedEnumerator` 上移为中间件公共能力）。

## W6 修订（必须同时满足的三条语义）

1. 稀客点位名保持**角色 label 原文**（不能套用 `DayMapData` 的"地图label+点位名"拼接）；
2. 补写 `DataBaseDay.allSpawnMarkerLabels[map.mapLabel]` 的键；
3. 保留"缺键 → `isNPCOnMap=false` 并跳过原方法"与"`npcs` 不含键时使用假字典"两条保护。
   `SolveAndUpdateCharacterPositionInternal` 的 `npcs` 是普通参数（非 out/ref），`isNPCOnMap` 才是 out。

## WV 修订（视图成员补齐）

- `ServePannelView` 增加：`Sellable? PendingFood/PendingBeverage { get; set; }`、`void RefreshPendingVisual()`、`void ResetPendingVisual()`（对应 `ResetServedVisualOnUI` 分支）、`void Close()`（`CloseExternPanel`）。
- 新增 `StoragePannelView`（保温箱面板）：`void RefreshFoodField()`；`WorkSync.RefreshStoragePanel` 与两条消息改用它。
- `PrepConfigView` 增加：`void UpdateGroups()`、`void UpdateCookers()`、`void UpdateUi()`（替代现 `PrepSceneManager.UpdateUI` 对面板的直取）。
- `IDayUiListener.OnShopPannelOpened` 用 `ShopPannelView`；`IPrepListener` 的 4 个成员用 `GuideMapView`/`PrepConfigView`。

## W7 追加

- `CompatPatches.Applied` 的 5 处消费者（`SceneFlow.cs:95`、`ModEntry.cs:42/60`、`GameSession.cs:87/89`、`MultiplayerStatus.cs:14`）在 Compat 清空后改成"中间件装配状态"或删除。
- 随补丁退役变为死代码清单（照删）：`GuestReentryPermits` 整类、`PatchBypassToken`/`PatchSkipPermit` 整类、`QTERewardManagerPatch.BuffLocalTrigger/OnQTESucceededExecuting`、`CollabBehaviourComponentPatch.PendingCollabMenu`、`NightSceneEventManagerPatch.HostCloseReplay`、`SpawnMarkerRegistry` 整文件、`SpecialGuestRegistry` 的 `GetSpawnMarkerConfig`/`RegisterAllSpawnMarkers`/`RegisterSpawnMarker`/`IsResourceExSpecialGuest(int)`（**保留 `IsResourceExSpecialGuest(string)`**）、`WorkSync` 的面板静态字段。
