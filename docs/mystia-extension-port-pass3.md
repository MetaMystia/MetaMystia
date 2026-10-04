# MetaMystia 迁移 Phase 3：交接文档

本文接手用。上位文档：[`mystia-extension-port.md`](mystia-extension-port.md)（迁法规范）、[`mystia-extension-port-pass2.md`](mystia-extension-port-pass2.md)（第二轮计划与 API 形状，**当前事实来源**）。框架侧另见同级仓库的 `docs/extension-api-plan.md`。本机路径写在同目录的 `mystia-extension-port-pass3.local.md`（不提交）。

## 1. 目标（已定，不再讨论）

1. **所有 mod（含 MetaMystia）不得自带任何注入管线**。注入只允许存在于 MEFX（`MystiaExtensionFramework`）：原生引导 + 桥接 Harmony 是唯一管线。
2. **mod 不得出现**：Harmony/BepInEx、`Il2CppInterop.Runtime.Injection`、`System.Reflection`、`UnityEngine`、编译器生成成员名（`__c__DisplayClass*`/`*_d__*`/`ObjectCompilerGenerated*`/`field_Public_*`/`*_Method_Internal_*`）、`[ModuleInitializer]`。SDK 分析器（`Mystia.Net.Sdk.Analyzers`）把这些报成 `MYSTIA1001`–`MYSTIA1006` 错误。
3. **SDK 公开面不得出现游戏/Unity/Il2Cpp 类型**：实体走不透明句柄 + 代理，值类型走 `Mystia.Numerics` 镜像，资产走句柄 + 意图级工厂，游戏数据对象走 `Mystia.Assets` 构建器。
4. 已定的形状（细节见 pass2 §3.2.1/§3.2.2）：`IMod`（身份+存储）、`IInitialization`、`IModStorage`（Config 文本流可读写 / Cache 原始流）、`[AutoWire] IModSaveHandler`（`OnModLoad(JsonDocument?)` / `OnModSave(JsonObject current, JsonObject target)`，默认把 `current` 拷进 `target`）、`ISaveListener` 无关的存档载体（键 `MystiaExtensionFramework`，信封只有 `module`+`data`，**无 mod 侧校验层**）、句柄以"控制器指针 + 会话代际戳"为键且**有效期 = 一次营业场景会话**、`TryGet([NotNullWhen(true)] out …)` 只在 `TryGet` 校验、`ref` 换手用 `ref XHandle?`、中断契约（所有监听都收到通知；任一取消只影响原方法与 post；通知型不受取消影响）。

## 2. 仓库与提交现状

| 仓库 | 分支 | HEAD | 状态 |
| --- | --- | --- | --- |
| `MetaMystia`（本仓库） | `mystia-extension-fx-port` | `787a34d` | 工作树干净（`.spinney/`、`*.local.props`、`*.local.md` 已忽略） |
| `MystiaExtensionFramework`（同名目录） | `main` | `43b36e3` | 工作树干净 |

两侧都**未推送**。`MetaMystia` 领先 `origin/mystia-extension-fx-port` 若干提交，需要时自行 push。

最近提交序列（框架）：`1210ba0`（互操作清洗命名 + SDK 2.0.0）→ `d796b5b`（禁令分析器、`Mystia.Numerics`、协程宿主、`IMod`/存储/存档载体）→ `8c596b2`（能力归位、`IPresentationServices`、IMGUI 去 Unity）→ `0c2686a`（资产 API、挑战时间线第一片）→ `f815339`（挑战/面板/日程/灵梦 seam）→ `7ccb9f0`（挑战评价 seam、白天地图构建）→ `d3c2229`（特效/音频/浮字真实现）→ `065d58f`（角色精灵集 + 贴图回读）→ `6feb2be`→`c36099b`（实体句柄/代理、数据构造器，含中断工作流的收尾）。

## 3. 当前验收基线（实测）

| 项 | 结果 |
| --- | --- |
| 框架构建 `dotnet build MystiaExtensionFramework.slnx -c Debug` | **0 错 0 警告** |
| 框架测试 `dotnet test src/Mystia.Net.Sdk.Tests` | **228/228** |
| SDK 打包 `dotnet pack sdk/Mystia.Extension.Sdk/Mystia.Extension.Sdk.Pack.csproj -c Release` | 成功（`artifacts/nuget/Mystia.Extension.Sdk.2.0.0.nupkg`） |
| 样例工程 ×3（`samples/SampleMod.{A,B,Skip}`） | 0 错 0 警告（每次重打 SDK 后需重建） |
| 模组构建 `dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Debug` | 见下表；**尚未全绿**，剩余工作见 §6 |
| `bash docs/port/static-check.sh` | 8/8（已去掉豁免） |
| `MetaMystia.Flow.Tests` | 构建 0 错；103 断言全通过 |
| `MetaMystia.Network.Tests` | 214 断言全通过 |

模组侧诊断（去重后，`dotnet build` 的计数是它的两倍）：

| 诊断 | 数量 | 含义 |
| --- | --- | --- |
| `MYSTIA1004` | 583 | 仍在用 `UnityEngine` 类型（几乎全在 `ResourceEx/**`） |
| `MYSTIA1002` | 28 | `Il2CppInterop` 注入/启动（`ResourceEx` 的 provider 与地址表） |
| `MYSTIA1003` | 17 | `System.Reflection`（`Utils/MetaMikuUtils.cs` 等裸指针工具） |
| 纯 CS | 6 | 全部是 `ResourceEx/Registries` 的资产形状落差（见 §6） |
| **去重总错误** | **634** | |

`MYSTIA1001`（Harmony/BepInEx）与 `MYSTIA1005`（编译器生成成员名）已清零：`Patches/`、`CompatPatches`、`HarmonyPrefixFlow` 与 `HarmonyX` 引用全部删除。

**关键机制提醒（务必记住）**：Roslyn 在存在**声明级**错误时会跳过方法体分析，**分析器也不执行**。所以只要还有声明级错误（现在的 6 个纯 CS），模组侧就看不到方法体级错误与 `MYSTIA100x` 诊断。反之，方法体错误清完后每减少一批声明级错误，都会"新暴露"一批此前不可见的错误——这是正常现象，不要用禁用注释或兼容层掩盖。

## 4. 框架已落地的能力（mod 可直接用）

- **生命周期**：`[AutoWire] IPostInitialize`→`IInitialization`（`Initialize(IMod)`）；`IGlobalGameLoop`/`ISceneListener`/各场景 `IXSceneGameLoop`。
- **上下文与存储**：`IMod`（`Id/Version/Directory/Log/Storage`）；`IModStorage`（Config 文本流、Cache 原始流）；`[AutoWire] IModSaveHandler` + 存档载体（`schedulerPartialDLC["MystiaExtensionFramework"]`）；`ICommonServices`（`MainThread`/`Coroutines`/`Platform`/`Assets`/`Locator`/`MapBuilder`/`DataObjects`/`Dialogs`/`Records`/`LoadScene`/`OpenDialog`/`FadeIn|Out`/`SetInputEnabled`/`SetNightTransitionEnabled`/`FoodTagText`/`EvaluationText`）。
- **场景能力**：`IPresentationServices`（`ShakeCamera`/`PlayVfx`/`PlayScreenOverlay`/`PlayAudio`/`PlayerPosition`/`TablePosition`/`TryRegisterPrefab`/`Bind`/`SpawnLabel`/`AttachLabel`），各场景服务上的 `Presentation`；`ICoroutineDispatcher`（`Owner` + `StartOn(ICoroutineOwner, …)` + `CoroutineAwait` 等待令牌）。
- **实体**（`Mystia.Scenes`）：`GuestHandle/GuestProxy`、`OrderHandle/OrderProxy`、`DishHandle/DishProxy`、`GuestDescription`、`GuestKind/GuestLeaveType/GuestEvaluation/OrderKind/OrderGenerationOutcome/DishKind`、`EntitySession`（会话轮换由 `SceneLoopHost.Shutdown` 驱动）。
- **视图与面板**：`ServePannelView`（含 `TryGetGuest` 类入口与 `PendingFood/PendingBeverage` 代理）、`ServeCallbackView`/`ServeCallbackKind`（按每次开面板包装四个回调）、`GuideMapView`、`PrepConfigView`、`ShopPannelView`。
- **聊天确认**：`IChatConfirmationListener` + `ChatConfirmationView`（`Kind`/`Confirmed`/`Confirm`）：确认动作随通知交给监听器，取消即扣住它，稍后运行它就是游戏本来要做的调用（幽幽子挑战开始确认在册）。
- **挑战时间线**：`IWorkSceneServices.Challenge`（阶段/时钟 `EndPhaseClock`/`SetPhaseSeconds`/`BasePhaseSeconds`、刷客闸门、`BossOrderEnabled`、`BossLife`、`Boss` 句柄、`AllowLeaveScene`、`SwallowCooker`、停机与失败重放 `StopRun`/`ReplayFailure`）+ `IChallengeListener`（`OnPreChallengeStep`/`OnChallengeStepRan` 的语义步骤 `ChallengeStep`、阶段、时钟、刷客、失败开始、buff 结束、本体生命值、吞厨具、`OnPreBossEvaluated`/`OnBossEvaluated`）。
- **资产/数据**：`IAssetFactory`（贴图/精灵/音频/PixelBuffer/纯色贴图/`TryGetTextureSize`/`TryReadPixels`/`TryCreateCharacterSpriteSet`）、`IAssetLocator`、`IDayMapBuilder` + `DayMapSpec` 家族、`IGameDataBuilder`（对话包 + 任务/事件节点）、`IPortraitProvider`。
- **IMGUI**：`Mystia.Imgui`（`IIMGUIDrawer`/`ImguiEvent`/`TextStyleHandle`（含 `Clone`）/`SkinHandle`/`FontHandle`/`TextureHandle`/`TextInputState`/`CreateFontFromOsFont`/`WhiteTexture`）。
- **护栏**：`Mystia.Net.Sdk.Analyzers`（`MYSTIA1001`–`1006`）+ 宿主加载期引用清单告警（告警不拒绝加载）。

## 5. 本会话（波 9）做了什么

1. **模组实体迁移（完成）**：`GuestsMap` 以 `GuestHandle` 为键；`GuestFSM` 全入口/重放走 `GuestProxy`/`OrderProxy`；`GuestSync` 的 9 个监听器改 `GuestHandle` 并写成显式接口实现；`GuestService` 重放走 `IWorkSceneGuests.SpawnNormal/SpawnSpecial`；`YuyukoGuestSync` 与 `WorkSync`、`Spell_Mai`、五个顾客消息 DTO 对齐。
2. **挑战主循环（完成）**：删 `YuyukoMainLoopPatch`。阶段数据交换改用 `IChallengeListener.OnPreChallengeStep/OnChallengeStepRan` 与框架的语义步骤常量；挑战服务只在场景作用域内可用，所以「某位置的数据还没就绪」用挂起该步表达，读写在该步被挂起的那一帧由营业场景循环完成（主机在那里读营业额/符卡数/生命值并广播，客机把主机依据写入本机闭包再放行；一阶段的结账数据在步后广播）。
3. **挑战确认回调（完成）**：删 `YuyukoExtraDialogData__c__DisplayClass4_0Patch`。框架新增 `IChatConfirmationListener` + `ChatConfirmationView`：确认动作随通知交给监听器，取消即扣住它，稍后运行它就是游戏本来要做的调用。
4. **挑战数据两项纯数值（完成）**：`SingleRoundSeconds` 由框架的 `IWorkSceneChallengeServices.BasePhaseSeconds` 给出；`DamageMultiplier` 由 `IChallengeBossEvaluation` 带来。
5. **失败整段重放（完成）**：删 `YuyukoBossDataPatch`。框架新增 `IWorkSceneChallengeServices.StopRun()`/`ReplayFailure()`：先停主循环与它启动的协程、收回重打 buff（并释放框架自己的厨具锁），等调用方的剧情与准备面板收尾后再清场并启动游戏的失败剧情。模组侧由 `YuyukoFailedMessage` 排队进营业场景循环（原实现在收包线程上直接跑）。
6. **兼容层清零（完成）**：`Patches/`（含 `HarmonyPrefixFlow.cs`）、`CompatPatches.cs`、`HarmonyX` 引用全部删除；`CompatPatches.Applied` 的 4 处门控改读 `ModRuntime.Ready`（失败原因 `ModRuntime.Failure`），提示文案由「补丁注入失败」改为「初始化失败」（`TextId.ModInitFailure`）。`static-check.sh` 的豁免全部去掉。

## 6. 未完成的迁移（按优先级）

1. **`ResourceEx` 资产四件（6 个纯 CS 错误，也是 `MYSTIA1002/1003/1004` 的大头）**：`ClothRegistry`/`PixelSpriteFactory`/`SpecialGuestRegistry.Visual` 的 `SpriteHandle ↔ Sprite`、`DialogRegistry` 的 `AssetReference` 与 `Utils.FindAndProcessResources`。**卡在 §11 第 4 项**（`IPortraitProvider` 是否继续代理化、是否需要"包装游戏自带精灵集"的入口）。
2. **`Utils/ExportUtils.cs` 去留**（全仓库无调用点；为救活它已自研 PNG 编码器与坐标公式反推）：建议删除。
3. **收尾**：模组全绿 → 三套测试 + `static-check.sh` + 样例工程全量验收；`docs/harmony-hook-style.md` 退役（本会话已删，`AGENTS.md` 的引用一并移除）；`docs/multiplayer-architecture.md` 的"构建与验证"仍写 Costura/BepInEx plugins/Preloader（过时）；`docs/mystia-extension-port-gaps.md` 为首轮口径。

## 7. 波 9 之后的顺序

1. 定 §11 第 4 项 → 收 `ResourceEx` 资产四件（目标：模组编译全绿）。
2. `ExportUtils` 裁决（§11 第 1 项）。
3. 全量验收与文档收尾。

## 8. 环境搭建（换一台机器要做的四件事）

1. 两个仓库放**同级目录**（本仓库的 `nuget.config` 用相对路径 `../MystiaExtensionFramework/artifacts/nuget` 指向本地 SDK 源）。
2. 复制 `MetaMystia.local.props.example` 为 `MetaMystia.local.props`，填 `MystiaInteropDir`（指向框架 `artifacts/interop`，注意结尾带分隔符）；框架侧默认已指向自己的 `artifacts/interop`。
3. 生成互操作并把 SDK 打包到本地源：
   ```text
   # 在框架仓库
   dotnet run --project src/Mystia.InteropGen -- <游戏工程目录> <游戏安装目录>
   dotnet pack sdk/Mystia.Extension.Sdk/Mystia.Extension.Sdk.Pack.csproj -c Release
   ```
   `Mystia.InteropGen` 会校验 `GameAssembly.dll` 的 SHA256 必须是 pin 住的那个（见框架 README），否则拒绝生成；它**确定性**地优先选择 `Build/Symbols/**/Managed` 这份备份，并**读该备份的成员表**确认 `ResourceProviderBase.Release` 存在——缺它就拒绝生成（`--allow-stripped-backup` 才会继续），因为桥接要 override 该成员。`--managed <dir>` 显式指定备份、`--output <dir>` 指定产物目录、`--symbols-backup` 强制要求 Symbols 备份；工具拒绝把产物写进任何"有 `Assembly-CSharp.dll` 但没有 `interop-manifest.json`"的目录（那是源，不是产物）。互操作产物在 `artifacts/interop`（被忽略，不入库）。
   **互操作不入库，所以换机时它不会跟着 git 走**：如果新机器的托管备份里没有 `ResourceProviderBase.Release`（不同构建的裁剪口味不同），最省事的做法是**把已有机器上的 `artifacts/interop` 目录整份拷过去**（两台机器 pin 的是同一个 `GameAssembly.dll`/`global-metadata.dat`，产物通用）。`Library/ScriptAssemblies` **不是**可用退路：未裁剪的项目程序集与裁剪过的引擎模块混用会让生成器抛 `NullReferenceException`（两台机器都复现了）。
4. 每次重打 SDK 后清缓存：`rm -rf ~/.nuget/packages/mystia.extension.sdk`，然后重建样例工程（`samples/SampleMod.*`），因为框架测试会加载它们的**预编译产物**。

## 9. 常用命令

```text
# 框架
dotnet build MystiaExtensionFramework.slnx -c Debug
dotnet test  src/Mystia.Net.Sdk.Tests/Mystia.Net.Sdk.Tests.csproj -c Debug
dotnet pack  sdk/Mystia.Extension.Sdk/Mystia.Extension.Sdk.Pack.csproj -c Release

# 模组
dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Debug
dotnet build src/MetaMystia.Server/MetaMystia.Server.csproj -c Debug
dotnet run   --project src/MetaMystia.Network.Tests -c Debug
dotnet run   --project src/MetaMystia.Flow.Tests -c Debug
bash docs/port/static-check.sh
```

坑：在 Git Bash 里用 `-p:MystiaInteropDir=<Windows 路径>` 会被转义弄坏（出现 `MetaMystia.Network` 侧的假错误），优先靠 `MetaMystia.local.props`；确要传就用正斜杠形式。

## 10. 本作（pin 住的 4.4.0e）实测的硬限制

这些是**互操作/玩家二进制本身没有**的能力，任何工作流都无法实现，只能降级或改设计：

- **`ImageConversion` 整体缺失**（`LoadImage`/`EncodeToPNG` 在 shipped metadata 里连字符串都没有）：框架自带 PNG 解码器（`PngImage`），**JPEG 不支持**；需要编码的地方自研（现状仅 `ExportUtils`）。
- **`AssetBundle` 只剩 `LoadFromStream` 与异步 API**（无 `LoadFromMemory`/`LoadAllAssets`/同步 `LoadFromFile`）：特效包改为流式 + `allAssets`（依赖 Unity "访问未完成的 allAssets 会 stall" 的语义，**待实机确认**）；`IPresentationServices.TryRegisterPrefab` 是当前的模板入口。
- **`Physics2D.IgnoreCollision`** 不存在（`Collider2D` 也只剩 `attachedRigidbody/isTrigger/offset`）：远端玩家改为 `isTrigger`，**不再阻挡任何东西**（含地图障碍与其他客人）。
- **`SortingLayer.NameToID/IDToName`、`LayerMask.NameToLayer`、可读的 `Renderer.sortingLayerName`** 都没有：白天地图的层检查改走 `SortingGroup` 往返与内置层槽位。
- **`Tilemap.CellToWorld`** 缺失：导出工具用 `GetCellCenterWorld − cellSize/2` 反推（待抽查）。
- `LoopedBGMPackage` **没有音量字段**；`CharacterSpriteSetFull.BaseSprite` 是静态数组（框架无法供图，依赖游戏填好）。
- 互操作命名：本机生成器的清洗名**不带 `PDM` 段**（`__c__DisplayClass16_0`、`_MainChallengeLoop_d__16`、`Method_Internal_…_0`），编译器生成类型的编号与 mod 旧注释有分歧（例如 retake 闭包在本机是 `__c__DisplayClass16_6`）。按名定位的挂点必须在启动时校验存在性（已有 `NamedSeams`/`ChallengeTargets`/`AssetBuilderTargets` 三个校验器）。
- **互操作可编译面 ≠ 运行期存在性**：权威是 pin 住的那份 shipped `global-metadata.dat`。互操作里可见、但 shipped metadata 没有的成员（`EncodeToPNG`/`LoadImage` 是已知例子）在运行期会解析失败，**绝不可调用**；反过来 shipped 有、互操作没有的（`NameToID`/`NameToLayer`/`CellToWorld` 只在未裁剪构建产物里）只能降级。
- **成员是否存在必须读成员表**：对托管程序集用 `strings`/`grep` 判成员会给出**假阴性**（已证：`Library/ScriptAssemblies/Unity.ResourceManager.dll` 明确声明 `Release`，而 `grep -cx Release` 返回 0）。判断符号存在性一律用元数据表（`PEReader`/`MetadataReader`，或 Il2CppDumper/反编译器的成员表）。
- **`ResourceProviderBase.Release` 属于"运行时真的有"那一侧，但它是否出现在互操作里由托管备份的口味决定**：本机 `Build/Symbols/.../Managed/Unity.ResourceManager.dll` 用成员表读**有**该成员（互操作也有），而另一台机器的备份里被 UnityLinker 裁掉了（互操作因此没有 → `AssetProviders.cs` 三处 override 报 `CS0115`）。所以缺它时应换备份或跨机拷贝产物，**不要删那三处 override**。

## 11. 待用户裁决

1. `Utils/ExportUtils.cs`：删除还是把 PNG 编码器上移 MEFX？（无调用点）
2. `PeerPlayer` 的触发器方案是否接受（另一选择是照游戏剧情角色直接销毁碰撞体）。
3. `NoteBookSkinPortrait` 配置项在笔记本补丁删除后空转：由 `ClothPortraitProvider` 按开关过滤，还是删配置项？
4. `IPortraitProvider` 是否需要代理化（现在仍 `ClothesProfile.Clothes` → `Sprite`），以及是否需要"包装游戏自带精灵集"的入口（`/skin set` 目前直接调游戏方法）。
5. 是否需要把阶段时钟的写入时机做成"时钟启动前"的钩子（现在的每帧幂等下放会让一阶段在"与挑战启动同帧"时漏掉拉伸）。

## 12. 必须实机验证的清单（全部结论目前都是源码/编译级）

幽幽子挑战：阶段推进与时钟、两段刷客闸门、吞厨具重放（主机原版吞食的过滤时序）、评价 seam（改写结果/台词/倍率）、失败与重打、本体生命值镜像、终局离开场景闸门；存档载体：带模组写入 → 完全卸载 → 原版读档推进保存 → 重装校验一致（含中文/嵌套/大字符串）；资产：PNG 解码与精灵集上屏、白天地图构建与切图、Addressables 位置注册、特效/遮罩/音频、浮字与名牌、联网皮肤（含旋转覆盖）；挑战时间线挂点的启动校验；派发契约在真实 Harmony 下的表现；`AssetBundle` 的 stall 语义。

## 13. 文档地图

- **当前事实来源**：本文件、`mystia-extension-port-pass2.md`（API 形状与计划）、框架 `docs/extension-api-plan.md`（框架侧已实现面）。
- **历史记录**（不要当现状读）：`mystia-extension-port.md`、`mystia-extension-port-plan.md`、`mystia-extension-port-report.md`、`docs/port/**` 的 handoff/audit 文档。
- **需要校正**：`docs/multiplayer-architecture.md`（构建与部署段仍写 Costura/BepInEx plugins/Preloader）、`docs/mystia-extension-port-gaps.md`（首轮口径，16 个缺口已被 pass2/pass3 全面覆盖）。
