# 符卡代码

阅读限制见 [README.md](README.md)。

## 数据与行为分离

一张符卡由两部分组成：

- **资源包声明数据**（`ResourceEx.json`，格式见 [`resourceex-package-contract.md`](../resourceex-package-contract.md)）：
  - `spells`：符卡的所属角色 ID、代码实现名（`implementation`）、两张卡的名称、说明、立绘，以及可选的 `portrayalPivot`、`vfxBundle`；
  - `buffs`：buff 的 ID、标题、说明、图标，与符卡解耦，由 `BuffRegistry` 注册；
  - `assetBundles`：需要预加载的 AssetBundle，由 `AssetBundleRegistry` 在启动时加载。
- **代码实现行为**：`Spell_Xxx.cs` 继承 `SpellBaseEx`，实现 `PositiveBuffRoutine` / `NegativeBuffRoutine`，并按需提供静态依赖检查。buff 按 ID 引用（`(EventManager.BuffType)id`）；特效通过实例的 `Vfx` 按预制件名称播放，包路径由配置提供。

符卡类须实现纯托管接口 `ISpellDependencies` 的静态方法 `CheckDependencies(VfxBundle)`，无依赖时明确返回 `null`。在手工维护的 `SpellRegistry.Implementations` 中登记 `["Xxx"] = Create<Spell_Xxx>`，然后在资源包中声明。同一个实现可以被多个角色复用。

`SpellBaseEx` 的 `SpellId`、`OwnerIdentifier`、`Vfx` 由注册器赋值。归属角色标识取自 `characters` 中同 ID 角色的 `label`；`vfxBundle` 按包标识归一化为 URI，再查询已加载的包。未配置包时 `Vfx` 为 `null`，是否必需由具体实现检查。

仅供托管侧使用的成员标注 `[HideFromIl2Cpp]`，避免被注入 il2cpp。**`SpellBaseEx` 等被注入的抽象类，其抽象成员必须全部标注 `[HideFromIl2Cpp]`**：Il2CppInterop 注入抽象类时，会为每个抽象方法越界写一个虚表项，损坏进程堆并导致随机崩溃，详见 [`il2cppinterop-defects.md`](../il2cppinterop-defects.md)。给基类新增抽象成员时同样适用。

## 注册流程

游戏各数据库初始化时机不同，因此分步写入：

| 步骤 | 方法 | 时机 | 写入内容 |
|---|---|---|---|
| 0 | `AssetBundleRegistry.LoadAll` | `DataBaseCore` 初始化后，最先执行 | 同步加载全部声明的 AssetBundle |
| 1 | `SpellRegistry.InitializeAll` | 同上 | 检查配置、角色、依赖与立绘，通过后按 `implementation` 注入类型、创建实例并构造句柄 |
| 2 | `SpellRegistry.RegisterAllLanguages` | 语言数据库初始化后 | `DataBaseLanguage.SpellLang[id]` |
| 3 | `BuffRegistry.RegisterAllBuffLanguages` | 语言数据库初始化后 | `DataBaseLanguage.BuffDescription[buffType]` |
| 4 | `SpellRegistry.RegisterAllCharacterHasSpell` | `DataBaseCharacter` 初始化后 | `DataBaseCharacter.CharacterHasSpell[id]` |
| 5 | `SpellRegistry.RegisterAllInstances` | `DataBaseNight.Initialize` 之后 | `DataBaseNight.SpecialGuestSpell[id]`、`SpecialGuestSpellPortrayal[id]` |

注意事项：

- 未安装资源包或未声明 `spells` 时，不注入对应类型、不创建实例。缺少必要字段或依赖时记录原因并跳过该符卡；后续登记只处理有效实例。
- 泛型工厂约束 `T : SpellBaseEx, ISpellDependencies`，先调用 `T.CheckDependencies`，通过后才注入类型、创建实例。检查返回 `null` 表示通过，返回字符串说明缺失原因，不访问夜间场景对象。接口不注册到 IL2CPP，实现方法标注 `[HideFromIl2Cpp]`。
- Mai 要求特效包包含本类六个预制件常量对应的资源，并要求 buff 11002/11003 的名称、说明和图标齐全。无特效实现不强制依赖 AssetBundle。
- 注入必须先于 `CreateInstance`，否则会抛出 `TypeInitializationException`。
- `DataBaseNight.Initialize` 会重建符卡字典，所以第 5 步放在 `DataBaseNightPatch` 的 Postfix 中执行。
- 立绘字典的值是 `ValueTuple`，写入时必须用 `ForceAddOrUpdateValueTuple`，原因见 [`il2cppinterop-defects.md`](../il2cppinterop-defects.md)。
- 立绘句柄为 `SceneDirector.RuntimeHandle<Sprite>(sprite).Cast<IAssetHandle<Sprite>>()`，符卡句柄同理。
- 注册阶段全部同步执行。不要在这里引入异步加载，也不要使用会触发泛型方法 unstripping 的接口，见 [assetbundle.md](assetbundle.md)。

## 协程语义

游戏执行符卡时，会等待 `OnPositiveBuffExecute` / `OnNegativeBuffExecute` 返回的协程结束，再调用 `OnFinishCallback` 并继续处理符卡队列（`SpellBase.ExecuteNegativeBuff`）。因此：

- 协程只负责开场演出，演出完毕立即结束。
- 不要在协程中等待整个 buff 时长，否则会阻塞符卡队列。
- buff 结束时的清理放进 buff 结束回调，不要依赖协程执行到末尾。

参考原版 `Spell_Kagerou`：先播放演出，再注册 buff，随后结束协程。

## buff

- 自定义 buff 在资源包 `buffs` 中声明，ID 位于资源包的 ID 段内，由资源包的 ID 校验保证合法；使用方用 `(EventManager.BuffType)id` 引用。
- `BuffRegistry` 写入 `BuffDescription` 的值为 `ObjectLanguageBase(name, description, icon)`；图标规格见 [art-pipeline.md](art-pipeline.md)。
- 描述中的占位符由回调替换。`RegisterTimedBuff` 的默认回调只替换 `$a`，使用 `$b`、`$c` 时必须传入 `currentBuffContextOverride` 自行替换。
- `SpellBase.RegisterTimedBuff` 调用的是 `TryOverrideTimedBuff`：buff 已存在时只延长时长，**不调用**注册回调。依赖回调创建的对象要在回调内创建，否则重复触发会泄漏。

## 常用游戏接口

| 接口 | 用途与注意事项 |
|---|---|
| `EventManager.RegisterTimedBuff(duration, buffType, out interrupt, onEnd, contextOverride)` | 计时 buff；清理放在 `onEnd` |
| `EventManager.MaxEvalLevelSet(duration, maxEval, tags, out _, onBuffEnd, buffType, isFood, overrideDescription, ..., containsOrNot)` | 按 tag 限制评价上限。`containsOrNot: false` 表示缺少 tag 时生效；评价值 Exbad=0、Bad=1、Norm=2 |
| `SpellBase.SetCameraShake(amplitude, duration, fadeTime)` | 迭代器，必须用 `EventCoroutineDelegation.Schedule(...)` 启动。单参数重载设置后不会恢复，不要使用 |
| `UIManager.Instance.ExecuteThrowDeliver(sprite, target, origin)` | 投掷动画，返回 Il2Cpp 协程，可在托管协程中直接 `yield return` |
| `SpellBase.GetPlayerPosition()`、`GetGuestTable(desk)` | 演出起点与目标位置 |
| `GuestsManager.Instance.AllGuestInDeskController` | 在座客人；是 Il2Cpp 集合，需先转成托管列表再遍历 |

## 协程宿主

| 场景 | 宿主 |
|---|---|
| 数据库初始化阶段 | 同步执行，或挂在 `PluginHost` 上。此时 `EventManager` 还不存在，访问 `MonoSingleton.Instance` 会在当前场景中隐式创建空对象 |
| 夜间符卡逻辑、周期任务、遮罩渐变 | 夜间场景的 `EventManager`（符卡内用 `Manager`），离开场景时协程随之停止 |

常驻的 `PluginHost` 不适合运行夜间逻辑：离开场景后协程仍在运行，会访问已销毁的对象。

## 代替玩家操作时的竞态

符卡代替玩家上菜、上酒或修改订单时，要复用原版流程，并逐一核对玩家与伙伴的并发路径。以原版上菜流程为例：

1. **写入"在空中"状态**（`ServedFoodInAir` / `ServedBeverageInAir`），让其他参与者跳过该订单。
2. **发射时通知伙伴**：`PartnerManager.OnOrderBaseStatusUpdate(order, FoodDelivered/BeverageDelivered, -1)`。伙伴只会被这类通知打断；`OnThrowDeliverStart` 发出的通知类型是 `Null`，伙伴会忽略。
3. **跳过玩家正在操作的桌子**：上菜面板放入物品时会检查订单状态，但关闭提交时**不复查**，会直接覆盖并结算。项目中用 `WorkSceneServePannelPatch.PanelDeskCode` 判断。
4. **落地复查**：满足以下全部条件才写入并结算，否则放弃：`GetInDeskGuest(desk)` 仍是原客人，`PeekOrders()` 仍是原订单，"在空中"字段仍是本次投出的物品。
5. **筛选条件**：部分 Boss 战会给稀客生成 `NormalOrder`，需求里写"普通订单"还是"普通客人"，要和用户确认后再选择按订单类型还是 `ControllType` 筛选。

## 联机

- 符卡由 `SpecialGuestsController.PostEvaluation` 触发。客机会重放主机的评价，所以符卡在两端各自运行。
- 项目的业务同步挂在原版操作入口上（例如上菜面板调用 `GuestFSM.OnServe` / `OnConfirmServe`）。符卡直接改字段不会同步。
- 订单结算由主机的 `EvaluateOrder` 发出 `EvaluateOrderMessage`，其中带有料理、酒水和结果；客机本地发起的 `EvaluateOrder` 会被拦截。
- 没有专门做联机适配时的做法：客机（`!GameFlow.ShouldSkipAction && GameSession.HasRoomPeers && GameSession.IsRoomClient`）只播放演出、不改业务状态，由主机的结算同步结果。客机按对象原生指针记录已播放的目标，避免重复演出。
- 限制：主机的中间状态（例如已上酒但未结算）不会同步到客机。需要完整一致时，要改走现有同步消息，或新增消息。
