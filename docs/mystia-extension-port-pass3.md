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
| 框架测试 `dotnet test src/Mystia.Net.Sdk.Tests` | **246/246** |
| SDK 打包 `dotnet pack sdk/Mystia.Extension.Sdk/Mystia.Extension.Sdk.Pack.csproj -c Release` | 成功（`artifacts/nuget/Mystia.Extension.Sdk.2.0.0.nupkg`） |
| 样例工程 ×3（`samples/SampleMod.{A,B,Skip}`） | 0 错 0 警告（每次重打 SDK 后需重建） |
| 模组构建 `dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Debug` | 见下表；**尚未全绿**，剩余工作见 §6 |
| `bash docs/port/static-check.sh` | 8/8（已去掉豁免） |
| `MetaMystia.Flow.Tests` | 构建 0 错；103 断言全通过 |
| `MetaMystia.Network.Tests` | 214 断言全通过 |

模组侧诊断（去重后，`dotnet build` 的计数是它的两倍）：

| 诊断 | 数量 |
| --- | --- |
| `MYSTIA1001` / `MYSTIA1002` / `MYSTIA1003` / `MYSTIA1004` / `MYSTIA1005` | **全部为 0** |
| 纯 CS | **0** |

即：模组侧不再出现 Harmony／BepInEx、裸 il2cpp、反射、`UnityEngine` 类型、编译器生成成员名，声明级与方法体级错误也都清零——**本轮第一次能构建出产物**（`-c Release` 产出 `MetaMystia.dll` + `MetaMystia.Network.dll` + `mod.json` + `Mystia.Net.Sdk.dll`，正是 CI 要打包给启动器的那份目录）。

**关键机制提醒（务必记住）**：Roslyn 在存在**声明级**错误时会跳过方法体分析，**分析器也不执行**。所以只要还有声明级错误，模组侧就看不到方法体级错误与 `MYSTIA100x` 诊断。反之，方法体错误清完后每减少一批声明级错误，都会"新暴露"一批此前不可见的错误——这是正常现象，不要用禁用注释或兼容层掩盖。

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
7. **资产/立绘面收尾（完成，声明级错误清零）**：`IPortraitProvider` 代理化（`int clothIndex` + `out SpriteHandle`）；`ClothRegistry`／`SpecialGuestRegistry.Visual`／`PlayerSkin` 立绘链改走句柄；框架新增 `IAssetFactory.TryWrapSprite`（把游戏自己持有的精灵包成句柄）与 `TryUnwrapCharacterSpriteSet`（把游戏自己的角色像素集拆成帧与样式）；`DialogRegistry` 的引擎资源引用按 `AssetReference.Address` 重建，`OnTransitionToNight` 改走框架 `IDialogCatalog.TryResolve`（原来是 `Resources.FindObjectsOfTypeAll`）。
8. **共享网络层不再引用游戏类型（完成）**：`MetaMystia.Network` 原来直接用游戏的 `Common.UI.Scene` 与 `CharacterSkinSets.SelectedType`，于是**凡是构建它的工程都需要互操作**（服务端、网络测试也在内）。现在协议有自己的词汇（`PlayerScene`、`SkinSelection`，与已有的 `GameStage` 同形），游戏枚举只在模组侧转换（`GameFlow.ToProtocolScene`/`ToGameScene`、`PlayerProfile` 的两处皮肤转换）。**实测**：`MystiaInteropDir` 置空时 Network、Network.Tests（214 断言全过）、Server 都能构建——需要互操作的只剩模组本体与 Flow.Tests。
9. **协议版本 0 → 1（`Versions.props`）**：场景字段的取值来源换了（游戏枚举 → 协议枚举），取值编号随之改变，因此新旧版本客户端会按协议号互相拒绝；皮肤选择字段的取值（0/1/2）不变。
10. **CI 整体改到新体系（完成）**：见 §6.6。
12. **框架面：模组自己的选择列表**（`IChatSelectionServices` + `ChatMenuEntry.Icon`）。模组侧删掉 `Il2CppOutDelegate.cs`（116 行，模组最后一条裸 il2cpp）与 `DaySceneSelectionMenu` 的游戏回调构造，`ChatSync` 的面板栈绕法删除；`Spell_Mai` 的调用点重排（投掷协程跑在框架协程泵上、**不在**服务作用域内，表现面调用因此排进场景循环——否则会抛异常并终止上酒流程）。
13. **框架面：模组的若干轮询**（`IClock`、`IInputServices` + `MystiaKey`、`UiNavigationEnabled`、`OpenUrl`、`IDayInputListener` 去引擎类型）。模组侧：热键配置改成 `MystiaKey`、控制台/玩家列表/插件热键改轮询、两处 `Time.unscaledTime` 改 `Clock.Now`、UI 导航开关走服务、外链走 `OpenUrl`、输入方向与坐标链改成 `Mystia.Numerics` 镜像（引擎只在 `NetPlayer` 的两个边界出现）。
14. **`VfxBundle` 迁到框架表现面**（`PlayVfx`/`PlayScreenOverlay`/`IVfxHandle`），保留 AssetBundle 加载与预制体读取（框架没有 AssetBundle 面）。
15. **`DialogRegistry` 迁到框架对话构建器**（`DialogSpec`/`IGameDataBuilder`），`DaySync` 的 `dialogContext` 回填删除（框架复制游戏模板，天然非空）。
16. **共享网络层去游戏类型** + **CI 整体改到新体系**（见 §6.6 与前面几节的记录）。
17. **死代码删除**：`Utils/ExportUtils.cs`（1101 行、无调用点，其 PNG 编码器已上移 MEFX）、`ResourceEx/Registries/PixelSpriteFactory.cs`（无调用点）、`ResourceEx/Mappers/**`（779 行，无调用点）、`ResourceEx/Addressables/**`（563 行自建 Addressables 注入，只被自己的初始化调用）、`Utils/MetaMikuUtils.cs`（装箱字典 workaround，无调用点）。

### 5.1 本轮引入的行为口径变化（实机验证重点）

1. **随机流**：符卡/刷客的抽签从 `UnityEngine.Random` 改为 `System.Random.Shared` → 分布不变，但**不再扰动游戏的全局随机流**（游戏自身的随机序列因此与迁移前不同，属改善但需知悉）；`GuestSync` 两处刷客间隔由闭区间 `[min,max]` 变为 `[min,max)`。
2. **对话 `goto` 落点**：`DialogRegistry` 把 `index` 按 1 基交给框架（旧路写 0 基，整体早一行）→ 这是**修正**旧错位；若实机发现比旧版晚一行，把 `ResolveLineNumber` 的返回值减 1。
3. **一次性特效的尾巴**：`VfxBundle.PlayOneShot` 到点由"立即销毁"变为"停止发射 + 排水 6 秒"（发射窗口不变，尾巴更长）。
4. **`Spell_Mai` 的精灵读取时机**：从"`Serve` 当下捕获"改为"投掷协程内读属性"（值不会变，但时机不同）。
5. **热键配置**：类型换成 `MystiaKey`；**数字型旧值**（本仓库早期按数字写盘）会解析成未定义值 → 已加"未定义即回落默认并告警"。
6. **控制台/列表的坐标显示**：镜像 `Vector2` 的 `ToString` 是 `Vector2 { X = .., Y = .. }`，不再是引擎的 `(x, y)`——纯显示差异。
7. **`Math.Clamp` 换成 `Min/Max`**：三处上界来自运行期窗口尺寸，`Math.Clamp` 在上界小于下界时会抛，`Min/Max` 不会（与 `Mathf.Clamp` 等价）。
8. **`DialogRegistry` 的应用路径**：模组的包仍同时经**数据面**注入（线-only 的桩）与**构建器**写入同一批名字，谁在表里取决于时机；需实机确认资源包对话保住了行内动作（若没有，修法是去掉数据面的对话注入）。

## 6. 未完成的迁移（只剩框架面与保留项）

模组侧现在剩 18 条诊断，全部是下面两块尚未补的面：

**"已声明的保留"并不等于可构建（务必先读这条）**

`MYSTIA1004` 是 **error 级**诊断，所以下面这 18 条会让 `dotnet build` 继续失败：**模组当前仍产不出 dll**，CI 的构建与产物步骤也就仍然红。要让模组真正可构建（从而让 CI 绿、能出产物），这 18 条必须消掉，只能靠框架再补两块面：

| 块 | 条数 | 需要的能力 |
| --- | --- | --- |
| `ResourceEx/Vfx/VfxBundle.cs` | 12 | **AssetBundle 入口**：从字节/流加载、枚举其中的预制件（现在只有 `LoadFromStream` + 异步 `LoadAllAssetsAsync`），并把预制件交给已有的 `TryRegisterPrefab`。B 的审计当时判断"不划算"（要把 AssetBundle 与 Il2Cpp 流拉进桥接），但那是在把保留当成可接受终态的前提下；既然 error 级不让过，这就是**必需项** |
| `Players/NetPlayer.cs` | 6 | **角色运动与层级面**：远端角色的 `Rigidbody2D`/`Collider2D`/`Transform` 读写（位置、速度、渲染层级、是否可见）→ 收到 `ICharacterServices` 上（按角色句柄操作，`Vector2/3` 用镜像） |

**当前保留（18 条，在补面之前的状态）**

**已完成的面**：`CallCommands`（行走命令）、`PeerPlayer`（生成/销毁/身高）、`GameFlow`（是否在播剧情）、`Panel`（快进对话）都已在 `a52ebdd`/`05a47c9` 落地；`PrepSync` 的客流倍率改用原地写法（它的唯一消费者被测试工程单独编译，不能依赖模组运行时），框架侧那个 `SetGuestFlowRate` 面因此撤掉。

