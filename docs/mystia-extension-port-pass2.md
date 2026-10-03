# 第二轮迁移：mod 去注入与代理化（只依赖 Mystia Extension）

配套：[`mystia-extension-port.md`](mystia-extension-port.md)（首轮迁法与验收）、[`mystia-extension-port-gaps.md`](mystia-extension-port-gaps.md)（缺口台账）、[`mystia-extension-port-report.md`](mystia-extension-port-report.md)（首轮交付）。中间件仓库是同级目录的 `MystiaExtensionFramework`。

两条目标：

1. **任何 mod（含 MetaMystia）都不再自带注入管线**。注入只允许存在于 MEFX：原生引导 + 桥接是唯一且长期允许的管线。
2. **互操作类型对 mod 作者不可见**。SDK 公开面只出现 MEFX 自己的代理、镜像值类型与句柄；mod 通过它们访问游戏。为此必须改造既有 SDK API。

结论标注口径：**实测**（命令输出）、**源码事实**（读代码）、**待审计**（须先读游戏源码确认，不得猜测）。

## 1. 禁令口径

### 1.1 允许

| 项 | 说明 |
| --- | --- |
| SDK 公开面 | `Mystia`、`Mystia.Scenes`、`Mystia.Listeners`、`Mystia.Data`、`Mystia.Spells` 下的接口、代理、镜像值类型与句柄 |
| 注入的存在 | MEFX 的原生引导、桥接 Harmony、`ClassInjector`、`AccessTools` 保留，但只允许出现在 MEFX 仓库 |

### 1.2 禁止（mod 侧，无例外）

| 项 | 判据 |
| --- | --- |
| Harmony 任何形式 | `HarmonyLib`/`0Harmony`、`HarmonyPatch`/`HarmonyPrefix`/`HarmonyPostfix`、`new Harmony(...)`、`PatchAll` |
| BepInEx 任何形式 | `BepInEx.*` 类型、插件类、`BasePlugin`、BepInEx 包引用 |
| 运行时注入 | `Il2CppInterop.Runtime.Injection`（`ClassInjector`）、`Il2CppInterop.Runtime.Startup`、`MonoMod.*` detour、自带原生 DLL/注入口 |
| 反射 | `System.Reflection`（含 `AccessTools`）、`System.Reflection.Emit`、`Assembly.Load*` |
| 编译器生成成员 | 编译期引用 `__c__DisplayClass*`、`*_d__*`、`Method_Internal_*`、`*_PDM_*`、`field_Public_*` |
| 游戏与 IL2CPP 类型 | 游戏命名空间类型、`Il2CppSystem.*`、`Il2CppInterop.*` |
| Unity 类型 | **`UnityEngine.*` 一律禁止**（含 `Vector2/3`、`Color`、`Rect` 等值类型）：改用 MEFX 自带的镜像类型与句柄，见 §3.2 |
| 自启动与裸指针 | `[ModuleInitializer]` 式补丁安装、`IL2CPP.il2cpp_*` 直接调用、手写对象头解析、`Marshal` 级委托构造 |

### 1.3 因禁令而需要 MEFX 补的能力（源码事实）

| 违例用法 | 现状位置 | 需要的替代 |
| --- | --- | --- |
| `ClassInjector` | `ResourceEx/Addressables/RuntimeAddressables.cs:295,315`、`ResourceEx/Registries/DayMapRegistry.cs:82`、3 个 `InMemory*Provider` | 派生注册 API + Addressables 位置注册（`IIl2CppComponentHost` 只覆盖 MonoBehaviour） |
| Unity 值类型 | 全仓库 ≈49 个文件（`Vector2` 15、`Vector3` 9、`Color` 9、`Vector3Int` 1…） | MEFX 镜像值类型 |
| Unity 资产/对象 | `Sprite` 16 文件、`GameObject` 10、`Transform` ≈10、`Texture2D` 8、`AudioClip` 5、`TextMeshPro`、`Tilemap`/`Grid`/渲染器/碰撞体、`ScriptableObject` | 句柄 + 资产工厂 + 受限组件操作 |
| 直接 `new` 游戏类型 | `GuestService.cs:54,85`、`YuyukoGuestSync.cs:276,278`、`Mappers.cs` 9 处、`SpecialGuestBuilder.cs:331`、`DialogRegistry.cs:86,200,201` | 数据面或服务面 API |
| `ScriptableObject.CreateInstance<游戏类型>` | `DialogRegistry.cs:189`、`Mappers.cs:40,154`、`DayMapRegistry.cs:221,233,310`、`NetSkinManager.cs:383,426`、`PixelSpriteFactory.cs:33,78`、`PlayerSkin.cs:246,271` | 资产工厂 API |
| `new GameObject`/`AddComponent`/`Instantiate` | `DayMapRegistry.cs`（9 处）、`VfxBundle.cs:74,101,110`、`FloatingTextHelper*.cs`、`PeerPlayer.cs:71`、`YuyukoGuestSync.Challenge.cs:172` | 资产工厂 + 视图挂载 |
| 裸 il2cpp/unsafe | `Utils/MetaMikuUtils.cs`、`Utils/Il2CppOutDelegate.cs`、`Utils/SgrYuki/ContainerExtensions.cs` | SDK 提供 out 委托适配与字符串读取 |
| 反射 | mod 侧未发现（`Multiplayer/Messages/MultiplayerMessage.cs` 的 `using System.Reflection` 用途待核） | 核定后替换 |
| 编译器生成成员引用 | `Listeners/ChatSync.cs`、`CookSelectionSync.cs`、`GuestSync.cs`、`Managers/YuyukoGuestSync.Challenge.cs`，以及 `Patches/Compat/` 16 个文件 | MEFX 内部承接；SDK 只暴露语义化类型 |

## 2. 现状（实测）

### 2.1 构建

| 仓库 | 命令 | 结果 |
| --- | --- | --- |
| 框架 | `dotnet build MystiaExtensionFramework.slnx -c Debug` | **失败，7 错**：`Game/ChatSeams.cs:29/30/44/45`、`Game/CookSelectionSeams.cs:23/26/40` 引用 `__c__DisplayClass17_0`、`__c__DisplayClass79_0`（CS0426/CS0117） |
| 模组 | `dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Debug -p:MystiaInteropDir=<框架 artifacts/interop>` | **失败，303 错**：214 × CS0246、52 × CS0426、18 × CS0117、18 × CS0234 |

模组错误分两类：**SDK 包过期**（`IIMGUIDrawer`(32)、`LogLevel`(24)、`ICoroutineDispatcher`(18)、`IGlobalServices`(12)、`ServePannelView`(10)……框架源码里存在，本地 nuget 源里是旧包）；**互操作命名**（`__c__DisplayClass16_0`(22)、`__c__DisplayClass16_5`(16)、`_MainChallengeLoop_d__16`(10)、`__c__DisplayClass4_0`(4)）。结论：**框架不修好 ⇒ SDK 无法重打包 ⇒ 模组必然失败**；重打包后必须清 `~/.nuget/packages/mystia.extension.sdk`。

### 2.2 门禁

`bash docs/port/static-check.sh` = **8/8 通过**（实测），但豁免了 `Patches/Compat/` 与 `CompatPatches.cs` 三处。

### 2.3 mod 侧注入残留（Phase 4 清空）

| 项 | 位置 | 规模 |
| --- | --- | --- |
| 兼容补丁 | `Patches/Compat/`（16 文件） | 30 个 hook / 76 个 Harmony 特性 |
| 应用入口 | `CompatPatches.cs` | 对自身程序集 `PatchAll` |
| Prefix 语义常量 | `Patches/HarmonyPrefixFlow.cs` | 被 30 个 hook 使用 |
| 旁路令牌 | `Patches/PatchBypassToken.cs` | 被 `RunTimeSchedulerGapsPatch` 使用 |
| 编译期依赖 | `MetaMystia.csproj` 的 `HarmonyX` | 只为兼容补丁存在 |
| 状态门 | `CompatPatches.Applied` 的 4 个消费者：`Listeners/SceneFlow.cs:94`、`ModEntry.cs:42/60`、`Multiplayer/GameSession.cs:87/89`、`UI/MultiplayerStatus.cs:35` | 换成框架装配状态信号 |
| 规范文档 | `docs/harmony-hook-style.md` | 退役，替换为 SDK-only 风格文档 |

## 3. 代理化：SDK 公开面不得出现互操作类型

### 3.1 实测暴露面

SDK 手写公开面共 **771 个公开成员 / 138 个公开类型**，其中 **98 个成员（12.7%）签名含非 SDK 类型**，涉及 **37 个非 SDK 类型**（游戏 23、UnityEngine 11、Il2Cpp/Il2CppInterop 3）：

| 文件 | 公开成员 | 含非 SDK 类型 | 典型暴露 |
| --- | --- | --- | --- |
| `GameApi/Listeners.cs` | 74 | 36 | `GuestGroupController`(25)、`Sellable`(17)、`GuestsManager.OrderBase`(7)、`CookController`(6)、`DialogPackage`(4)、`Recipe`(4)… |
| `GameApi/SceneLoops.cs` | 126 | 29 | 同上 + `GameTimeManager.TimeMode`、`PartnerManager.OrderChangeContext`、`NormalGuest`、`IzakayaLevel`、`IGuideMapSpot` |
| `GameApi/Views.cs` | 23 | 6 | `GuestGroupController`、`OrderBase`、`Sellable`、`IGuideMapSpot`、`Il2CppSystem.Threading.CancellationToken` |
| `Imgui.cs` | 21 | 13 | `Rect`、`GUIStyle`、`GUISkin`、`Event`、`Texture`、`Vector2`、`Color`、`Il2CppObjectBase` |
| `GameApi/SceneEconomy.cs` / `Listeners.Metrics.cs` | 5 / 8 | 4 / 5 | `EventManager.MathOperation`、`EventManager.ServeType` |
| `GameApi/Listeners.Mission.cs` | 1 | 1 | `RunTimeScheduler.TrackedMissionData` |
| `GameApi/Listeners.CookSelection.cs` | 4 | 1 | `Recipe` |
| `DialogCatalog.cs` / `ModSprites.cs` / `Coroutines.cs` | 2 / 1 / 17 | 1 / 1 / 1 | `DialogPackage`、`Sprite`、`UnityEngine.Component` |
| `GameApi/Database.cs` | 357 | **0** | 已是全代理（35 个 record struct + 7 个枚举） |
| 其余（`Listeners.Qte/Schedule/Session/Ui/Chat`、`SceneBuffs`、`Spells`、`ModStorage`、`IModContext`…） | — | **0** | 已是零暴露 |

数据面（`Database.cs`）与一半监听族已是零暴露的代理风格，可作为模板。

### 3.2 代理与镜像设计

| 主题 | 现状 | 方案 |
| --- | --- | --- |
| Unity 值类型 | `Vector2`（15 文件）、`Vector3`（9）、`Vector3Int`（1）、`Color`（9）、`Rect` | **MEFX 自带镜像值类型**（命名空间待定，建议 `Mystia.Numerics`），边界由桥接转换；mod 代码里不再出现 `UnityEngine` |
| Unity 资产/对象 | `Sprite`（16）、`GameObject`（10）、`Transform`（≈10）、`Texture2D`（8）、`AudioClip`（5）、`ScriptableObject`、`TextMeshPro`、`Tilemap`/`Grid`/渲染器/碰撞体、`Canvas`/`CanvasGroup`/`RawImage` | 不透明句柄（`SpriteHandle`、`AudioClipHandle`、`ObjectHandle`、`TextureHandle`…）+ 资产工厂 + 受限组件操作 |
| 实体 | `Sellable`、`Recipe`、`CookController`、`GuestGroupController`、`OrderBase`、`NormalGuest`、`GameTimeManager` | 代理类（沿用 `Views.cs` 的 `sealed class` + internal 构造 + 只读投影 + 显式动作方法）：`DishProxy`、`RecipeProxy`、`CookerProxy`、`GuestProxy`、`OrderProxy`、`TimeProxy`… |
| 身份与生命周期 | mod 用 `Controller.Pointer` 做实体同一性（`GuestsMap.cs:19,70,96`、`GuestFSM.cs:288,380,404`）；`GuestsMap`（static）只在顾客走到终态时单独移除，**没有夜/场景边界清理**（`GuestFSM.cs:1256`、`YuyukoGuestSync.cs:511`）；`GuestFSM._pending` 的 30s TTL 是**夜内**卡死检测（`GuestFSM.cs:66-136`），其 `IsManualGuest` 延长分支用于夜内长剧情/挑战（`GuestFSM.Manual.cs`、`YuyukoGuestSync.Reset()`），**不代表跨夜存活** | 句柄以**控制器指针为键** + 会话代际戳；**有效期 = 一次营业场景会话（进夜 → 离夜）**：夜内的挑战/剧情长流程仍是同一会话，句柄继续有效；离夜或换场景即全部作废（`GuestsMap` 与延迟队列随之清空，取代今天的"泄漏到终态 + 超时兜底"）。资产句柄是进程级，不受夜边界影响 |
| `ref` 写回 | `ref Sellable`、`ref Recipe`、`ref OrderBase`、`ref EvaluationResult`、`ref TimeMode`、`ref List<NormalGuest>` | `ref DishProxy?` / `ref OrderProxy?`（赋值即替换对象）；集合改为 SDK 自己的可变列表类型 |
| 枚举 | `GameTimeManager.TimeMode`、`EventManager.MathOperation/ServeType`、`GuestGroupController.LeaveType/EvaluationResult`、`IzakayaLevel` | SDK 镜像枚举（已有先例：`GuestLeaveKind`、`CharacterKind`、`RewardBuffKind`、`DialogActionKind`） |
| 场景 | `Scene` 出现在 `ICommonServices.LoadScene`、`IDayListener.OnSceneChanging` | 改为已有的 `SceneId`（必要时补场景句柄） |
| 字典/数组 | `Il2CppSystem.Collections.Generic.Dictionary<int,string>`、`Il2CppStructArray`、`Il2CppReferenceArray` | SDK 值类型/自有集合；网络消息里的游戏派生数据（`Multiplayer/Messages/WorkScene/SellableFood.cs:31`）同样改 SDK 值类型 |
| IMGUI | `Rect`、`GUIStyle`、`GUISkin`、`Event`、`Texture`、`Vector2`、`Color` | 值类型用镜像；样式/皮肤/纹理用句柄；`Event` 改为镜像的输入事件结构（字段集合见开放项） |
| 协程 | `ICoroutineDispatcher.StartOn(Component owner, …)`、`WaitForSeconds`/`WaitForEndOfFrame` 等等待 | SDK 自己的宿主（`ICoroutineOwner`）与不透明等待令牌；mod 的 `IEnumerator` 只 yield SDK 令牌 |
| 裸指针/委托 | `Utils/MetaMikuUtils.cs`、`Utils/Il2CppOutDelegate.cs`、`Utils/SgrYuki/ContainerExtensions.cs` | SDK 提供 out 委托适配与字符串读取 |

**游戏侧事实（审计，用于界定生命周期）**：挑战与剧情都在**同一个营业场景会话内**完成——

- 挑战是夜场景里启动的协程：`Assets/Scripts/Night/SceneManager.cs:337` 用 `EventManager.StartCoroutine(m_LoadedBossData.MainChallengeLoop(bossContext))`，幽幽子本体取自 `NightSceneDirector.GetControlled("Yuyuko")`（`Assets/Scripts/Night/BossBattle/YuyukoBossData.cs:65` 起）。
- 结束时离开夜场景：`Assets/Scripts/Night/GuestSystem/GuestsManager.cs:732/762` 与 `CloseIzakayaAndLeaveChallengeMode`（→ `RunTimeScheduler.OnChallengeEnd`）都走 `NightSceneDirector.TryLeaveSession`（`Assets/Scripts/Night/NightSceneDirector.cs:347-366`），它执行 `UniversalGameManager.LoadScene(Scene.DayScene, …)`——夜场景与其所有对象一起销毁。
- `NightSceneDirector.IsManualWorkSceneSession` 由白天侧设置（`Assets/Scripts/Day/DaySceneDirector.cs:59/88/174`），标记"本次营业来自剧情脚本"，不是跨夜状态。

结论：**不存在跨夜的实体引用需求**；实体句柄的有效期就是"进夜到离夜"，夜内的挑战/剧情长流程（`IsManualGuest` 的 TTL 延长）仍在同一会话内。今天真正的问题不是"要不要跨夜机制"，而是模组侧的注册表与队列**在场景边界没有清空点**（见 WP-4.5）。

### 3.2.1 API 形状（已定）

| 项 | 决定 |
| --- | --- |
| 镜像值类型 | `Mystia.Numerics`；用 C# 惯例的 `record struct` 位置参数形式（可变），如 `public record struct Vector3(float X, float Y, float Z)`；含 `Vector2`、`Vector3`、`Vector3Int`、`Vector4`、`Color`、`Rect`、`Bounds`、`BoundsInt`、`Vector2Int`；算术与比较运算符手写（record 不生成 `+ - *`） |
| 命名映射 | 不再保留 Unity 的字段名：`x/y/z` → `X/Y/Z`、`r/g/b/a` → `R/G/B/A`、`width/height` → `Width/Height`。**迁移不是纯 `using` 替换**，调用点要一起改 |
| 资产 API | **意图级**（不开低层 Unity 对象模型逃生口）：`IAssetFactory`（贴图/精灵/音频/PixelBuffer）、`IDayMapBuilder`、`IAudioPlayback`、`IFloatingLabel`、`IAssetLocator`；未预见的用法按"新增意图级 API"处理 |
| 游戏数据对象构造 | `MissionNode`/`EventNode`/`DialogPackage`/`Tile`/`CharacterSpriteSet*`/`SpecialGuest` 等的构造归 `Mystia.Assets`（builder），不放 `Mystia.Data` |
| 句柄/代理有效性 | 句柄 `readonly struct`（控制器指针 + 会话戳），代理 `sealed class`；**只在 `TryGet` 时校验**，之后不逐次校验；`ref` 换手用 `ref XHandle?` |
| IMGUI | `Mystia.Imgui`；样式/皮肤/纹理用句柄，能力面按审计表封死（`TextStyleHandle` 的 `font/fontSize/wordWrap/richText/alignment/fontStyle/normal/focused/padding/margin/CalcSize/CalcHeight`，`SkinHandle` 的 `label/textField/button/font`，`ImguiEvent` 的 `kind/keyCode/shift/character/mousePosition/Use`） |

### 3.2.2 上下文、存储与中断契约（已定）

| 项 | 决定 |
| --- | --- |
| `IModContext` → `IMod` | 只保留"我是谁 + 我的私有状态"：`Id`、`Version`、`Directory`、`Log`、`Storage`；`Paths`/`IGamePaths` 删除，`GameRoot` 删除 |
| `IModStorage` | 两部分：**Config（文本流、可读写；玩家可编辑的配置位置）** + **Cache（原始 `Stream`、可读写、不进存档）** |
| 存档分区 | 不进 `IModStorage`：由独立的 `[AutoWire] IModSaveHandler` 承担，读写**该 mod 自己的 `JsonObject`/`JsonDocument`**（MEFX 已切好片）：`void OnModLoad(JsonDocument? toRead)`（无记录时为 `null`）与 `void OnModSave(JsonObject current, JsonObject target)`；**不设 mod 侧校验层**。`current` = 本模块上次写入（或本次读档得到的）数据，`target` = MEFX 将要写入的对象；接口的默认实现把 `current` 深拷贝进 `target`（不覆写者即保持原样），覆写者自行决定是否先从 `current` 拷贝。保存完成后 MEFX 用 `target` 的内容更新 `current` |
| `IPostInitialize` → `IInitialization` | 语义不变（每实例一次、注册后立即、只交接 `IMod`）；命名与入口对齐 |
| `IPresentationServices` | 新增：承接原 `ICommonServices` 里被 `ServiceScope.Require()` 保护的五项（`ShakeCamera`/`PlayVfx`/`PlayAudio`/`PlayerPosition`/`TablePosition`），作为 `Presentation` 暴露在各场景服务上 |
| `IDatabaseExtension` | 保持数据进数据出；新增可选 `void OnInjecting(IDatabaseContext context)`，`IDatabaseContext = { Log, Storage, Assets, Locator }`（全为非作用域成员） |
| `IIl2CppComponentHost` | **删除**（含宿主 `UnavailableComponents` 桩）；常驻行为由 MEFX 内部持有 |
| 中断契约 | 所有 pre 监听**一律全部收到通知**，早先的 mod 取消不影响后续 mod 被通知；**任一** mod 取消 ⇒ 原方法与 post 不执行；通知型回调不受取消影响 |

存档分区约定（载体沿用 `MetaMystia` 仓库 `codex/metalib` 分支的调研结论，去掉其中与模块版本相关的那部分）：载体是存档里已有的 `schedulerPartialDLC["MystiaExtensionFramework"]`，数据放进其 `finishedEvents` 字符串数组，**每个字符串是一条"信封"记录**（见下）；MEFX 按 `module`（= `IMod.Id`）定位本模块条目并只替换它，其他条目逐字保留，无法解析的条目原样保留；未用集合写空、`dlcSaveDate` 跟随游戏当前进度。回调在 `LoadPlayerData` 之后、`WriteCurrentPlayerDataToSlotAsync` 之前于主线程同步调用（此时不会并发修改正在被后台保存线程读取的快照）。

信封（调研文档给的结构示意）：

```json
"schedulerPartialDLC": {
  "MystiaExtensionFramework": {
    "dlcSaveDate": 0,
    "scheduledEvents": {},
    "scheduledNews": {},
    "scheduledNewsReplaceContents": {},
    "allTrackingMissions": {},
    "finishedEvents": ["{\"module\":\"example.mod\",\"data\":{\"count\":3}}"],
    "finishedMissions": []
  }
}
```

**已定**：专用键取 `MystiaExtensionFramework`；信封**只含 `module` 与 `data`**（不含 `version`）；**mod 只接触自己那份切片**（MEFX 按 `IMod.Id` 定位并拆装信封），因此谈不上"mod 污染存档"：信封与载体结构由 MEFX 独占，mod 交出的 `JsonObject` 只是它自己的数据。MEFX 侧仍需保证的是载体健壮性——只替换本模块条目、其他条目逐字保留、无法解析的条目原样保留——这不是 mod 可见的校验层。

实现必须保持的条件（调研文档 §"实现时必须保持的条件"）：

1. 专用键在有/无模组时都保持"未启用"：不注册为 DLC、不进 `ActiveDLCLabel`、不改 `allActivatedDLC`。
2. 命名避开 `CORE`、官方 DLC 名、`UNDEFINED*`、`ResourceEx*`；不让事件/任务分类产生该键。
3. 保留原版外层结构与字段类型，未使用的集合写空集合；任意模组结构只放进编码字符串内部。
4. 只替换自己的条目，保留其他 DLC 与其他模组的数据；识别不了的内部格式原样保留，不得清空覆盖。
5. 跟随游戏当前运行数据；加载期间与主菜单拒绝操作；日期回溯保留运行数据，加载历史备份用备份里的数据。
6. 保存前固定本轮要写的数据：新版保存工作经 `RunOnThreadPool` 在后台执行，不得并发修改同一集合，也不得在后台读取 Unity 对象。



### 3.3 mod 侧影响面（实测）





- 220 个文件 / 26,460 行里，**101 个文件（45.9%）直接 `using` 游戏命名空间**（`GameData.*` 65、`NightScene.*` 34、`Common.*` 25、`DayScene.*` 6）；**≈49 个用 Unity 类型**；**≈40 个用 `Il2Cpp*`**；**5 个用 `ClassInjector`**。
- 重依赖区：`Managers/`（持有 `GuestGroupController`/`OrderBase`/`DialogPackage` 与游戏状态机，`GuestFSM.cs` 1,258 行）、`ResourceEx/`（50 文件：贴图/音频/预制体/Tilemap/Addressables provider 与 11+ 处静态资产缓存）、`Players/`（`Object.Instantiate` 角色 prefab、皮肤 Sprite 数组）、`UI/`（`TextMeshPro`、`GameObject`）、`Console/`（`SwapMap`、`Il2CppStructArray`）、`Utils/`（裸 il2cpp、unsafe）。
- 相对干净：`Multiplayer/`（52 文件里只有 2 个碰互操作类型）、`Data/`、根目录。

### 3.4 执行策略

- **不分级、不做兼容层**：分析器规则（注入 / 反射 / 生成类型 / 互操作类型 / UnityEngine）从第一版起全部 **error**；不用别名、shim、`#pragma warning disable`、临时豁免清单。做法是"先让它报错，再把代码改到能编译"。
- 直接后果：**在 Phase 4 完成前，MetaMystia 不可构建**；需要在专用分支上推进，CI 对该分支预期为红（或仅跑框架侧）。这一点在开工前确认。
- 加载期护栏仍是**警告并继续加载**（不拒绝加载）。
- `ResourceEx` 保持为 MetaMystia 侧的资源包使用者，MEFX 只补它需要的 backing 能力（资产工厂、位置注册、组件操作），不把 ResourceEx 收进 MEFX。

## 4. 互操作命名（方案 B：生成端重命名）

游戏是 IL2CPP 构建，托管侧没有游戏类型，编译前必须先有一份互操作程序集。编译器生成成员（闭包 `<>c__DisplayClass*`、状态机 `<X>d__*`、局部函数）在两种来源里名字不同：

| 来源 | 例 | 能否写进 C# |
| --- | --- | --- |
| IL2CPP 的 Managed 备份 | `<>c__DisplayClass79_0`、`<MainChallengeLoop>d__16` | 不能（`<` `>` `\|` 非法） |
| 由游戏二进制 dump 的 dummy | `__c__DisplayClass79_0`、`_MainChallengeLoop_d__16`、`Method_Internal_Void_PDM_0` | 能 |

`Mystia.InteropGen` 现在用 Managed 备份当 Source，产出第一种；桥接与 mod 的源码写的是第二种，本机也没有第二种可用 → 两仓库都构建不了。

**已定：方案 B** —— 生成端产出合法标识符。**落地方式**：`Mystia.InteropGen` 不再使用 `PassthroughNames`（原先为 `true`，会把源工程的 Roslyn 名字 `<>c__DisplayClass*`、`<X>d__16` 原样写出），改由 Il2CppInterop 自身的清洗产生合法名字；`--passthrough` 仅保留作对照。要求：

1. 命名必须与**同一代工具**一致：Il2CppInterop 的清洗名形如 `__c__DisplayClass79_0`、`_MainChallengeLoop_d__16`、`ObjectCompilerGenerated…Unique`、`Method_Internal_<返回类型>_<参数类型>_<序号>`；**`PDM` 段是 BepInEx 那份生成物的差异，不得写死**（本项目用 1.5.1，实测无 `PDM`）。
2. 运行期按名字定位的目标（如选菜提交回调、灵梦保护窗口）用字符串给出，启动时校验目标存在、缺失即明确报错。
3. 生成参数记入 `interop-manifest.json`（含 `passthroughNames`），换生成器版本后必须复验这些名字。

### Phase 0 执行记录（本轮）

- WP-0.1 完成：`Mystia.InteropGen` 改为默认清洗命名并重新生成 `artifacts/interop`；据此修正桥接 3 处引用（`ChatSeams` 两个聊天菜单回调名去掉 `PDM` 段、`CookSelectionSeams` 的选菜提交回调名同上，`ScheduleSeams` 的注释同步）。**框架 0 错**（`dotnet build MystiaExtensionFramework.slnx -c Debug`）。
- WP-0.2 完成：SDK 版本升到 `2.0.0`（nuspec、框架 README、三个样例、模组 csproj），`dotnet pack -c Release` 产出 `artifacts/nuget/Mystia.Extension.Sdk.2.0.0.nupkg`，清掉 `1.0.0` 的包缓存。
- WP-0.3 部分完成：本机 `MetaMystia.local.props` 已建（`MystiaInteropDir` 指向框架 `artifacts/interop`）；`AGENTS.local.md` 待补。
- WP-0.4 基线：模组错误从 303 降到 **24（12 个不同）**，且**全部**落在计划中已要删除/重写的两处——`Patches/Compat/` 11 项（`…_PDM_0` 方法名 4 项、`ObjectCompilerGenerated…Unique` 类型名 7 项）与 `Managers/YuyukoGuestSync.Challenge.cs` 1 项（同型）。SDK 与互操作层面已无错误。

## 5. 计划

### Phase 0 恢复可构建基线

| WP | 内容 | 验收 |
| --- | --- | --- |
| WP-0.1 | 实现 §4 的生成端重命名，产出新互操作 | 框架 0 错 |
| WP-0.2 | 重打包 SDK、清包缓存，跑通框架 → SDK → 模组三级构建 | 模组只剩行为性错误 |
| WP-0.3 | 恢复被忽略的本地配置（互操作目录、游戏工程路径） | 三个测试工程可跑 |
| WP-0.4 | 记录基线：四工程构建 + 三个测试 + `static-check.sh` | 失败项逐条列出 |

### Phase 1 护栏与规则

| WP | 内容 | 强度 |
| --- | --- | --- |
| WP-1.1 | SDK 内新增 Roslyn `DiagnosticAnalyzer`（随包 `analyzers/dotnet/cs` 发布），五组规则全部 **error**：注入、反射、生成类型、互操作类型、UnityEngine | 硬 |
| WP-1.2 | `ModLoader` 加载时扫描 `GetReferencedAssemblies()`，命中 Ban 清单（`0Harmony`/`HarmonyLib`/`BepInEx*` 等）**警告并继续加载** | 软 |
| WP-1.3 | `static-check.sh` 去掉豁免，新增 HarmonyX 引用、`ClassInjector`、`System.Reflection`、生成类型名、`UnityEngine`、`[ModuleInitializer]` 检查 | 软 |
| WP-1.4 | `AGENTS.md` 增禁令；`docs/harmony-hook-style.md` 退役，替换为 SDK-only 风格文档 | 软 |

### Phase 2 代理化（改 SDK API）

每项都是"先审计游戏侧真实用法 → 定 SDK 形状 → 在 MEFX 实现 → 加 SDK 单测"。

| WP | 内容 | 需要先审计的用法 |
| --- | --- | --- |
| WP-2.1 | 枚举与场景：`TimeMode`、`MathOperation`、`ServeType`、`LeaveType`、`EvaluationResult`、`IzakayaLevel`、`Scene`→`SceneId` | 各枚举在游戏侧的取值与模组用到的分支 |
| WP-2.2 | 实体代理与句柄：`DishProxy`、`RecipeProxy`、`CookerProxy`、`GuestProxy`、`OrderProxy`、`TimeProxy`、`GuestHandle` 等 | 模组在这些对象上**读什么、写什么**（如 `order.ServFood/ServBeverage`、`food.modifier`、`controller.OnMoodUpdateCallback`） |
| WP-2.3 | 集合与 `ref` 写回约定；`DialogPackage` → `DialogHandle`（`IDialogCatalog`/`OpenDialog` 一并改） | 模组对集合的元素级改写方式 |
| WP-2.4 | Unity 值类型镜像（`Vec2`/`Vec3`/`Vec3Int`/`Color`/`Rect`…） | 模组实际用到的成员（算术、插值、序列化） |
| WP-2.5 | 资产与运行时对象：贴图/精灵/音频/预制体/`ScriptableObject`/组件操作（`AddComponent`、`Transform`、`SetActive`）+ Addressables 位置注册与派生注册（接管 `ClassInjector` 用途） | ResourceEx 50 个文件的真实操作集合、`Players/` 的 prefab 与皮肤路径、`VfxBundle` |
| WP-2.6 | IMGUI 门面：镜像值类型 + 样式/皮肤/纹理句柄 + 镜像输入事件 | 模组自绘面板（`InGameConsole`、`PlayerListPanel`、`DaySceneSelectionMenu`）用到的成员 |
| WP-2.7 | 协程宿主与等待令牌；裸指针/out 委托适配、字符串读取（接管 `MetaMikuUtils`/`Il2CppOutDelegate`/`ContainerExtensions`） | 符卡与消息层的 out 委托、il2cpp 字符串读取场景 |
| WP-2.8 | 数据面 backing 扩展：让 ResourceEx 作为使用方继续工作（资产包 → 数据/资产注册） | ResourceEx 现有包格式与写表路径 |

### Phase 3 MEFX 承接缺口（全部功能保留）

| WP | 内容 | 备注 |
| --- | --- | --- |
| WP-3.1 | **挑战时间线模块（B2）**：幽幽子族 13 项（11 个 `Yuyuko*` + `IncomeControllerYuyukoPatch` + `NightSceneDirectorPatch`）挂点搬进 MEFX，SDK 暴露语义化监听与服务 | 承载：阶段推进与计时、挂起/恢复、阶段刷客门控、失败与重打、评价写回、HUD 数值、本体捕获 |
| WP-3.2 | 面板与入口：`WorkSceneSustainedPannelPatch`（订单回调包装 + 营业内快进）、`NoteBookProfilePannelPatch`（笔记本立绘） | 视图/监听扩展 |
| WP-3.3 | 日程：`RunTimeSchedulerGapsPatch`（奖励拦截 + **灵梦保护窗口**） | 窗口目标是编译器生成的局部函数；版本锁定下由 MEFX 按运行期名字定位，启动时校验存在、缺失即报错（**待审计**） |

**Phase 3 进展（挑战时间线，已落地第一片）**：MEFX 已实现 `IWorkSceneServices.Challenge`（`IWorkSceneChallengeServices`）+ `IChallengeListener`，覆盖 **阶段时钟**（`OnPreChallengeClockTick`/`OnChallengeClockElapsed`/`EndPhaseClock`/`SetPhaseSeconds`）、**阶段推进闸门**（`OnPreChallengeStep`/`OnChallengeStepRan`）、**阶段刷客闸门**（`OnPreChallengeGuestSpawn`/`OnChallengeGuestSpawned`），挂点是 `YuyukoBossData.MainChallengeLoop`、`_MainChallengeLoop_d__16.MoveNext`、`DC16_0` 的时钟状态机与 `DC16_0`/`DC16_6` 的刷客状态机、`IncomeControllerYuyuko.SetContext`（阶段来源）。SDK 不暴露任何游戏类型，字符串/编译期挂点在启动时校验存在性。

**审计后仍未搬入 MEFX 的幽幽子项**（按审计表编号）：`ifYuyukoCouldOrder` 写回（需要 `SetBossOrderEnabled` + 1 个 seam）、限时负面符卡协程（应落在 `ISpellHost`/`TriggerNegativeBuff` 而非状态机）、吞厨具目标重放（**interop 里 `remainedCookers`/`targetType`/`targets` 字段不存在**，只能改在厨具层：`CookSystemManager.GetCooker`/`LockedCookersRaw`/`TileManager.CookerDesks`）、失败剧情开始通知（1 个成员 + 1 个 seam）、宿主下发失败的整段重放（需要桥接内复用状态机构造与字段）、重打 `OnBuffEnd` 清理（1 个成员 + 1 个 seam）、阶段 3 本体生命值镜像（`BossLife` + 变更通知）、终局离开场景的闸门与本体捕获（`NightSceneDirector`）、以及"`OnGroupEvaluated`/`OnGroupPostEvaluated` 是否真的包住挑战的评价覆盖回调"这一条待运行期确认。

**审计发现的编号分歧（待实机确认后再改 mod 侧注释）**：本机互操作把 retake 闭包编为 `__c__DisplayClass16_6`、其立绘刷客循环为 `16_6.ObjectCompilerGenerated…InObWaVoObMoInVoBoOb0`，而 mod 现有注释写作 `16_5` 且把 `InObWaObUnique` 当作立绘循环（在互操作里它属于 `16_4` 的剧情 `Phase3OrderLoop`）。MEFX 的 seam 按结构（闭包归属 + 跨 yield 存活字段）定位目标，不依赖编号。

**资产面的一条硬限制（已记入行为差异）**：本作 `global-metadata.dat` 里**没有** `LoadImage`/`EncodeToPNG`（`ImageConversion` 只剩模块名出现），运行期无法解析 `ImageConversion.LoadImage`，因此 MEFX 自己实现了 PNG 解码（`TryCreateTexture`），**JPEG 不支持**（与现有"非 PNG 跳过并记警告"的移植行为一致）。改用编辑器 `Managed/UnityEngine` 作为 Unity base libs 重新生成互操作虽然能让该 API 在编译期出现，但那只是编辑器形状、玩家端并不存在，故不采用。

### Phase 4 MetaMystia 去注入与代理化

| WP | 内容 |
| --- | --- |
| WP-4.1 | 删 `Patches/`（16 个兼容补丁 + `HarmonyPrefixFlow.cs` + `PatchBypassToken.cs` + `CompatPatches.cs`）与 `HarmonyX` 引用 |
| WP-4.2 | `Managers/YuyukoGuestSync*.cs` 改走 WP-3.1 的监听，消掉生成类型引用 |
| WP-4.3 | `CompatPatches.Applied` 的 4 个消费者改读框架装配状态 |
| WP-4.4 | 按 Phase 2 的新 API 迁移 101 个文件的互操作用法（优先 `Managers/`、`ResourceEx/`、`Players/`、`UI/`、`Console/`、`Utils/`） |
| WP-4.5 | 在营业场景进入/离开时显式清空 `GuestsMap` 与各 `GuestFSM` 的延迟队列（今天没有任何清空点，靠"终态移除 + 30s 超时"兜底）；句柄改为夜会话作用域后这是必要清理，也是行为变更（未走到终态的顾客不再残留） |

**当前可见错误清单（截至波 5）**：声明级错误 12 个（`Patches/Compat/` 11 + `Managers/YuyukoGuestSync.Challenge.cs` 1，等框架 seam 收尾）。因为 Roslyn 在存在声明级错误时会跳过方法体分析，方法体级错误此前不可见；用同样的源码集去掉那两个错误簇后在临时工程里实测，**真实错误 32 个**（全部在下列文件，均属版本 4.0–4.4 与互操作成员布局的差异）：

| 文件 | 个数 | 性质与对策 |
| --- | --- | --- |
| `Utils/ExportUtils.cs` | 12 | `ImageConversion`、`Tilemap.CellToWorld`、`RenderTexture.GetTemporary` 重载、`Renderer.sortingLayerName` 无 getter——都要改走新 API 或放弃该调试导出功能（需给出控制台功能取舍结论） |
| `ResourceEx/SpellCollection/Spell_Mai.cs` | 8 | 3 处 `StartOn(EventManager.Instance, …)` + `PlayerPosition`/`TablePosition`/`ShakeCamera` 已移到 `Presentation`——改走 `scene.Presentation` 与新 `StartOn(owner, …)` |
| `ResourceEx/Registries/DayMapRegistry.cs` | 3 | `SortingLayer.NameToID/IDToName`、`LayerMask.NameToLayer` 在互操作里不存在——该文件整体改走 `ICommonServices.MapBuilder` |
| `Players/PeerPlayer.cs` | 3 | `Physics2D.IgnoreCollision` 不存在——需替代方案（同层/同碰撞矩阵或直接不忽略） |
| `ResourceEx/Vfx/VfxBundle.cs` | 2 | `AssetBundle.LoadAllAssets`/`LoadFromMemory` 只有 `…Async` 变体——改异步加载 |
| `ResourceEx/Mappers/Mappers.cs` | 1 | `Random.value` 不存在——改 `System.Random` |
| `Players/NetSkinManager.cs` | 1 | 待按资产 API 迁移 |
| `ResourceEx/AssetManagement/RexAssets.cs`、`Utils/Utils.cs` | 各 1 | `ImageConversion`——改走 `IAssetFactory.TryCreateTexture`（框架自研 PNG 解码器；JPEG 不支持） |

**波 7 里程碑（模组首次声明级清零）**：`Patches/Compat/` 从 16 个退役到 **7 个**，`YuyukoGuestSync.Challenge.cs` 重写为不再引用任何编译器生成成员，声明级错误 **12 → 0**。于是 **SDK 禁令分析器首次真正运行**，实测诊断（去重后）：`MYSTIA1004`（UnityEngine）658、`MYSTIA1001`（Harmony/BepInEx）43、`MYSTIA1002`（Il2CppInterop 注入）28、`MYSTIA1005`（生成成员名）24、`MYSTIA1003`（反射）17；另有 13 个此前被遮蔽的方法体 CS 错误（`PlayerSkin.cs`、`ResourceEx/Registries/*`、`Spell_Mai.cs`）与约 684 条可空性警告。`static-check.sh` 8/8。

留册的 7 个 Compat 补丁各有明文缺口，需要框架补面才能退役：

| 补丁 | 缺口 |
| --- | --- |
| `YuyukoChallengeContextPatch` / `YuyukoRetakeContextPatch` | `OnPreBossEvaluated` 不带回调的 `out string message`（覆盖台词）与闭包 `dmgMultiplier` |
| `YuyukoMainLoopPatch` | `ChallengeStep` 编号不可读（阶段数据只能靠恢复位置编号交换）；建议给 `ChallengeStep` 加语义常量或把阶段数据搬上服务 |
| `YuyukoBossDataPatch` | 失败**整段重放**（停主循环与子协程、销毁特效、构造失败状态机）无对应面 |
| `YuyukoExtraDialogData__c__DisplayClass4_0Patch` | 无「挑战确认回调」面（原回调先 `ScheduleEventExtern` 再 `StartChallengeSession`） |
| `YuyukoTimedNegativeSpellPatch` | 限时负面符卡是独立协程，`ISpellHost`/监听均无挂点 |
| `NightSceneDirectorPatch` | `AllowLeaveScene` 是「放行/拦下」，不是「本次离开来自最终试炼」；本体控制器捕获也无替代面（`Challenge.Boss` 只是不透明句柄） |

另有两处待框架收尾的细节：`SetPhaseSeconds` 需绝对秒数且要求时钟未启动，而基础时长只在闭包里（现由纯数值桥转出并在监听 `Update` 逐帧幂等下放，一阶段若与挑战启动同帧开始会漏掉拉伸）；`NoteBookSkinPortrait` 开关随笔记本补丁删除而空转（建议由 `ClothPortraitProvider` 按开关过滤，或删配置项）。

### Phase 5 验收与文档

- 合并门槛（每批）：框架 0 错 → SDK 单测 → 重打包 + 清缓存 → 模组 0 错 → `Network.Tests` 214 / `Flow.Tests` 103 → 扩展后的 `static-check.sh` 全绿。
- 专项证据：① 违规样例 mod（Harmony / UnityEngine / 生成类型各一个）构建失败；② 带 Ban 引用的 mod 被警告并加载；③ `src/MetaMystia.Mod` 零命中（Harmony / BepInEx / Injection / 反射 / 生成类型 / 游戏类型 / UnityEngine）。
- 台账与报告同步：`mystia-extension-port-gaps.md` 的 16 项按 §6 改判，行为变化记入报告。

## 6. 缺口归宿（16 项）

| 缺口 | 归宿 | 功能 |
| --- | --- | --- |
| `YuyukoBossDataPatch`、`YuyukoMainLoopPatch`、`YuyukoChallengeContextPatch`、`YuyukoRetakeContextPatch`、`YuyukoExtraDialogData__c__DisplayClass4_0Patch`、`YuyukoLockCookersPatch`、`YuyukoTimedNegativeSpellPatch`、`YuyukoTimingPatch`、`YuyukoOnFailPatch`、`YuyukoPhase2GuestSpawnPatch`、`YuyukoPhase3GuestSpawnPatch`、`IncomeControllerYuyukoPatch`、`NightSceneDirectorPatch`（13 项） | WP-3.1 | 全部保留 |
| `WorkSceneSustainedPannelPatch`、`NoteBookProfilePannelPatch` | WP-3.2 | 全部保留 |
| `RunTimeSchedulerGapsPatch` | WP-3.3 | 全部保留（含灵梦保护窗口） |

## 7. 已定决策

| 项 | 决定 |
| --- | --- |
| MEFX 的 Harmony 注入 | 唯一且长期允许的管线，不做零注入 |
| 禁令范围 | Harmony/BepInEx、`Il2CppInterop.Runtime.Injection`、`System.Reflection`、编译器生成类型、游戏与 IL2CPP 类型、**UnityEngine 全部类型** |
| Unity 值类型 | 由 MEFX 自带镜像类型替代，不例外放行 |
| 互操作命名 | 方案 B：生成端重命名为合法标识符 |
| 执行策略 | 不分级、不做兼容层：规则一次开成 error，再改到能编译（Phase 4 前模组不可构建） |
| 句柄身份 | 以控制器指针为键；有效期 = 一次营业场景会话（进夜 → 离夜），夜内的挑战/剧情长流程仍有效，离夜或换场景即全部作废；资产句柄是进程级、不受夜边界影响 |
| 幽幽子族形态 | B2：MEFX 内建"挑战时间线"模块 |
| 功能取舍 | 全部保留，不砍功能 |
| ResourceEx 归属 | 留在 MetaMystia，作为 MEFX 数据注入能力的普通使用方；MEFX 只补 backing 能力 |
| 护栏强度 | 编译期 error；加载期警告（不拒绝加载） |
| 镜像值类型命名空间 | `Mystia.Numerics` |
| 句柄访问形式 | 统一 `bool TryGetX([NotNullWhen(true)] out T? value)`；**SDK 工程启用 nullable 并作为公开契约** |
| 长分支 | 允许长期为红（"Not that long"，即允许但不宜久拖） |
| SDK 版本与兼容 | 直接升 `2.0`，**不提供迁移指南**（SDK 属 Day2，无生态包袱） |
| 协议冲突 | 必要时**允许加兼容 shim**；因此为值类型化而调整消息布局、按需 bump `NetworkProtocolVersion` 均可接受 |
| 本轮不动 | 网络协议、ResourceEx 包格式、控制台命令对外行为、游戏工程 |

## 8. 剩余待定（B 组：由执行者审计/实验后确定，已获授权）

1. IMGUI 需要暴露的输入事件字段（`type`、`keyCode`、`mousePosition`、`delta`、`shift`…）与样式/皮肤句柄的能力下限。
2. 资产 API 的最小集合：按 ResourceEx / `Players/` / `VfxBundle` / UI 的真实操作审计后确定。
3. `Multiplayer/Messages/MultiplayerMessage.cs` 的 `System.Reflection` 用途核定。
4. 枚举镜像与游戏枚举顺序的一致性校验方式；30s TTL 的最终形态；`IGuestDirector`/`IGuestDriver`/`IGuestControls` 是否保留；`ISpell` 允许 yield 的等待令牌集合；违规样例工程与分析器单测。

已在 A 组定案（不再列为开放项）：镜像命名空间（`Mystia.Numerics`）、句柄访问形式（`TryGetX` + nullable）、长分支允许为红、SDK 升 2.0 且无迁移指南、必要时允许兼容 shim。

5. `ICoroutineOwner` 的挂载位置：候选 A `IModContext.Coroutines`（每 mod 一个常驻调度器）；B `ICoroutineDispatcher.Owner` + `StartOn(owner, …)`（调度器在构造时就绑定宿主，场景调度器的宿主随场景销毁）；C 由服务暴露（`ICommonServices.CoroutineOwner`）。倾向 B+A，见讨论。
