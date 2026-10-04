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

| 诊断 | 数量 | 含义 |
| --- | --- | --- |
| `MYSTIA1004` | 207 | 仍在用 `UnityEngine` 类型（`VfxBundle`、`InGameConsole`/面板 IMGUI 层、`DialogRegistry`、`Spell_Mai`、网络层） |
| `MYSTIA1003` | 5 | `System.Reflection`（`UI/L10n.cs` 4 条读自带嵌资、`Utils/Il2CppOutDelegate.cs` 1 条 `Marshal`） |
| `MYSTIA1002` | 2 | `Il2CppInterop` 裸 il2cpp（`Utils/Il2CppOutDelegate.cs`） |
| 纯 CS | 0 | 声明级错误已清零（这正是分析器能全量报告的原因） |
| **去重总错误** | **214** | |

`MYSTIA1001`（Harmony/BepInEx）与 `MYSTIA1005`（编译器生成成员名）保持 0；`ResourceEx` 的自建注入管线（`ResourceEx/Addressables/**` 的 `ClassInjector` provider 与 `RuntimeAddressables`）已作为死代码删除，模组的 `MYSTIA1002` 因此从 28 降到 14。

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
8. **死代码删除**：`Utils/ExportUtils.cs`（1101 行、无调用点，其 PNG 编码器已上移 MEFX）、`ResourceEx/Registries/PixelSpriteFactory.cs`（无调用点）、`ResourceEx/Mappers/**`（779 行，无调用点）、`ResourceEx/Addressables/**`（563 行自建 Addressables 注入，只被自己的初始化调用）、`Utils/MetaMikuUtils.cs`（装箱字典 workaround，无调用点）。

## 6. 未完成的迁移（按优先级，逐项已查清）

> 这一段是本轮把剩余诊断逐条查清后的结果：下面每项都写了"它是什么 / 为什么被禁 / 正解 / 代价"，可直接照做。

### 6.1 `Il2CppOutDelegate.cs`（`MYSTIA1002` ×2，另 1 条反射）

- **它是什么**：把带 `out` 参数的托管 lambda 包成游戏的 `DaySceneChatSelectionPannel.GetSelectionConfigurationCallback`（IL2CPP 委托无法用 C# 委托直接表达 `out` 参数，所以要在运行期造代理）。
- **谁在用**：唯一调用方是 `UI/DaySceneSelectionMenu.BuildSelectionItems`，而它服务于**模组自己打开的列表菜单** —— 礼物信箱（`GiftMailboxManager.OpenMailboxMenu`/`OpenGiftMenu`）与剧情回放（`StoryReplayManager` 三处），做法是自建回调数组交给游戏的 `UIManager.OpenAfterChatMenu`，外加一个结束按钮。
- **正解**：框架给一条"模组自开列表菜单"的面（条目形状同 `ChatMenuEntry`：标题 + 可用性 + 选中回调，再加结束按钮），out 参数与委托转换留在桥接 —— 桥接里已经有 `ChatMenuPipeline` 那套机器，正是干这个的。模组侧 `DaySceneSelectionMenu` 与 `Il2CppOutDelegate` 一起删，两个 Manager 改成提供条目。
- **代价**：SDK 一个成员 + 桥接复用既有管线；改动面是 3 个调用点。需实机点一遍信箱与回放列表（菜单是纯 UI 路径，离线测不到）。

### 6.2 `Utils/SgrYuki/Functional.cs`、`NativeDllExtractor.cs`（已完成）

`Functional` 的四个方法里，`CheckStacktraceContains`（补丁时代的栈扫描）与 `ModifyReadonlyField`（反射写字段）**全仓无调用方**；`GetCallerName` 换成 `[CallerMemberName]`（它读的栈帧本来就解析成同一个直接调用者，日志文本一字不变）；`LogStacktrace` 并入 `LogWrapper`。文件删除。`NativeDllExtractor`（把内嵌原生 DLL 释放到基目录、无调用方）一并删除。合计 −53 行。

### 6.3 `IdRangeValidator.cs` 的公钥（已完成）

公钥是公开信息，直接内联成常量（`PublicKeyPem`），删掉 `LoadEmbeddedPublicKey` 与 `public.pem` 及其 csproj 条目；导入改走跨平台的 `RSA.Create()`，顺手去掉 Windows-only 的 CSP 与 `#pragma CA1416`。

### 6.4 `MYSTIA1004` 残余（207 条，按被禁类型统计）

| 类别 | 数量 | 内容与对策 |
| --- | --- | --- |
| 值类型与静态 API | ≈110 | `Vector2` 26、`Mathf` 19、`Time` 14、`KeyCode` 13、`Input` 10、`Vector3` 9、`Object` 8（`UnityEngine.Object`）、`Random` 5、`PlayState` 2、`WaitForSeconds` 2 —— 换 `Mystia.Numerics` 镜像 / `System.Math` / 场景循环给的 `delta` / `Mystia.Imgui` 的 `ImguiEvent`。多数是机械替换，不需要新面。 |
| 引擎对象 | ≈90 | `GameObject` 21、`AssetReferenceSprite` 9 + `AssetReferenceT` 7、`CanvasGroup` 5、`AudioClip` 5、`EventSystem` 4、`AssetBundle` 3、`SpriteRenderer`/`Rigidbody2D`/`RawImage`/`ParticleSystem`/`Canvas` 各 2 —— 逐项判断"框架补面"还是"保留在模组引擎层"。`VfxBundle.cs`（52 条）是集中地，此前已明确 VFX 层直接用 Unity 对象。 |

- **`DialogRegistry` 的 `AssetReferenceSprite`/`AssetReferenceT`（16 条）**：模组自建 `DialogPackage` 并往游戏对话行的引擎引用字段里写 `new AssetReferenceSprite(reference.Address)`。框架的 `DialogActionSpec` 已经用 `SpriteHandle` 表达这些字段（`IGameDataBuilder` 那条路），因此正解是把 `DialogRegistry` 迁到框架的对话构建器；代价中等（模组对话还带 `OverrideReplaceTextCallback` 之类的自定义行为，需要先确认框架面能覆盖）。
- 建议次序：**先做值类型那 ≈110 条**（机械、无新面、可离线计分），再按文件处置引擎对象段。

### 6.6 `UI/L10n.cs`（`MYSTIA1003` 4 条）—— 待定

`L10n.Initialize` 用 `Assembly.GetExecutingAssembly().GetManifestResourceStream` 读自己内嵌的 `UI/Locales/{en,zh-CN}.json`（`MergeJson` 里的 `Enum.TryParse<TextId>` 不触发禁令，反射只为读嵌资）。覆盖路径 `LoadLocaleOverride` 本来就按 `ModRuntime.Directory` 读文件。

三个选项：

1. **改自带文件**：csproj 把两个 JSON 从 `EmbeddedResource` 改成 `Content … CopyToOutputDirectory`（`mod.json` 已是这种做法），`Initialize` 直接调现成的 `LoadLanguageFromFile` → −10 行、无新面。**代价**：模组目录从"一份 dll + mod.json"变成多两个文件，部署/发布产物要跟着改（CI 的 artifact 也要带上）。
2. **生成器内联**：用仓库里现成的 `MetaMystia.Generators`（Roslyn 生成器，已经负责 `[AutoLog]`）在编译期把两份 JSON 生成常量 → 无反射、无额外文件、部署形态不变；代价是生成器约 60–100 行。
3. **框架给"读本模组自带资源"的面**（如 `IMod.TryOpenResource`）：保持单份 dll，但为一件小事扩 SDK 面。

### 6.7 CI（`.github/workflows/ci.yml`）—— 部分完成

- **已做**：删掉"编译 Preloader"与"保存 Preloader 产物"两步 —— 那个工程随注入管线一起删了，而 CI 还在发布它的产物；顺带去掉已不存在的 `-p:DeployToGame=false`。
- **待定（整段与旧 BepInEx 构建绑死）**：CI 现在下载一份 deps 包并把它当 `BepInExPath`，而当前构建需要的是**框架**（`nuget.config` 指向 `../MystiaExtensionFramework/artifacts/nuget`）与 `MystiaInteropDir`；产物路径 `MetaMystia-v*.dll` 也是旧 csproj 的 `RenamedAssembly`（已在移植时删除），现在产出的是 `MetaMystia.dll`。这需要决定"CI 怎么拿到框架与互操作"，不宜由执行者猜。

## 7. 之后的顺序

1. §6.6 `L10n`（三选一）与 §6.7 CI（整段要重新设计）—— 都需要先定。
2. §6.1（框架补"模组自开列表菜单"面 → 删 `Il2CppOutDelegate`）—— 需要框架面 + 实机。
3. §6.4 的值类型段（≈110 条，机械、无新面），再引擎对象段（含 `DialogRegistry` 迁到框架对话构建器）。
4. §11-D（`CharacterSpriteSetStyle` 补两个字段）与 §11 第 6 项（阶段时钟钩子）。
5. 全量验收与文档收尾。

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

- **`ImageConversion` 整体缺失**（`LoadImage`/`EncodeToPNG` 在 shipped metadata 里连字符串都没有）：框架自带 PNG 解码器与编码器（`PngImage`／`PngWriter`），**JPEG 不支持**；模组侧的 `ExportUtils` 已删除（§6 第 8 项）。
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

1. ~~`Utils/ExportUtils.cs` 去留~~ —— 已裁决：删除，PNG 编码器上移 MEFX（已完成，见 §6 第 8 项）。
2. ~~`IPortraitProvider` 是否代理化 + 是否需要"包装游戏自带精灵集"的入口~~ —— 已裁决：两者都做（已完成，见 §6 第 7 项）。
3. ~~`PeerPlayer` 的碰撞方案~~ —— 已裁决：方案 B（用游戏自己的参数）。已完成。
4. ~~`NoteBookSkinPortrait` 开关去留~~ —— 已裁决：让开关真生效。已完成：框架把"哪个面板在要立绘"作为 `PortraitTarget` 交给提供者，模组在笔记本上按开关过滤。
5. ~~`PlayerSkin` 游戏自带皮肤的旋转覆盖~~ —— 已裁决：路 2（复制游戏那份集）+ 接线。已完成。
6. 是否需要把阶段时钟的写入时机做成"时钟启动前"的钩子（现在的每帧幂等下放会让一阶段在"与挑战启动同帧"时漏掉拉伸）。
7. **新**：`CharacterSpriteSetStyle` 要不要补上那两个"只有美术资源才带"的字段（见下 D / §6 第 4 项）。

### A. `PeerPlayer` 的碰撞方案

**现状**：远端角色是游戏 `CharacterBase` prefab 的克隆，构造时 `Initialize(skin, speed, shouldTurnOnCollider: true)` 保留碰撞体，随后 `cl2d.isTrigger = true`（`PeerPlayer.cs:87/126-129`）。原因是互操作里 `Physics2D` 只剩查询，`IgnoreCollision`/`IgnoreLayerCollision` 都没有、2D 碰撞矩阵也不能运行时改，做不到「碰撞对」级过滤。

**方案 A（现状）**：保留触发器碰撞体。挡不住任何人（本地玩家与其他远端角色都能穿过），也不与地图障碍碰撞（位置完全由网络位置驱动）；但游戏自己的 `CharacterControllerUnit.hasCollider` 仍是 **true**，且触发器仍会产生触发事件。

**方案 B**：`Initialize(..., shouldTurnOnCollider: false)` —— 游戏自己的「无碰撞体」状态（`CharacterControllerUnit.cs:187` 直接 `Destroy(cl2d)`、`hasCollider = false`）；游戏剧情角色走的就是这条（`SceneDirector.cs:406`）。

**两者的差别（都有源码证据，除标注外）**：

| | 方案 A（触发器） | 方案 B（游戏自己销毁） |
| --- | --- | --- |
| 阻挡本地玩家/其他远端 | 不挡 | 不挡 |
| 与地图障碍碰撞 | 不碰（位置纯网络驱动） | 不碰 |
| 游戏 `hasCollider` | true → 角色携带的可拆卸装饰（`RemovableTrim`）会**带碰撞体**（`CharacterControllerUnit.cs:519-530`），`UpdateColliderStatus` 可用 | false → 装饰不带碰撞体；`UpdateColliderStatus` 会打一行错误日志（`CharacterControllerUnit.cs:289`，目前只有 `Spell_Shinmyoumaru` 调它，且只对本地玩家） |
| 触发器事件 | **会发**：全游戏只有 4 个 `OnTrigger*2D` 处理器，其中 `Day/Interactables/Entities/InteractableArea.cs:51` 用 `CompareTag("Player")` 过滤 —— 远端角色若仍是 `Player` tag（prefab 继承，未在模组侧改过），白天交互区会被远端角色触发 | 不发（没有碰撞体） |
| 依赖碰撞体的查询 | 找得到碰撞体 | 找不到 |

**要判的点**：白天交互区（`InteractableArea`）被远端角色触发是否会造成可见问题（例如互相刷出交互提示）；以及是否有任何逻辑依赖远端角色存在碰撞体（模组旧注释称"联机角色需保留碰撞体"，但没有给出具体依赖，游戏自己的剧情角色证明引擎不需要它）。

**已定（方案 B）**：`PeerPlayer` 现在 `Initialize(..., shouldTurnOnCollider: false)`，删掉 `MakeColliderNonBlocking`。待实机确认的只有"远端角色不再发触发事件"是否符合预期（白天交互区不再被远端角色触发）。

### B. `Experimental/NoteBookSkinPortrait` 开关（已定：选项 ②）

**背景**：开关（默认 **false**）原本由已退役的 `NoteBookProfilePannelPatch` 使用，它挂在 `NoteBookProfilePannel.OnPanelOpen` 的 postfix 上：开关开着且 `/skin` 覆盖生效时，把皮肤立绘写进 `mystiaPic.sprite`。迁移后两个面板共用一条提供者链，开关因此空转。

**查清的两件事**（旧补丁源码 `1908e4a^`）：

1. 迁移前的 `SetupPortrayalVisual` 前缀对 `/skin` 分支返回 `SkipOriginal`（跳过原方法），于是调用方 `NoteBookProfilePannel` 的 `if (!SetupPortrayalVisual(...))` 分支会把页面设成 `DefaultPic`——**这才是那个开关真正在修的东西**：开关关着时笔记本显示默认图，开着才显示皮肤立绘。
2. 对该前缀的"ResourceEx 服装立绘"分支（返回 `RunOriginal`），开关不起作用。

**落地**：`PortraitTarget.NoteBook` 时开关关闭 → 提供者不回答 → 框架的前缀照常跑游戏自己的逻辑 → 该页显示游戏自己的立绘（比迁移前的 `DefaultPic` 更合理）。开关打开 → 与白天 HUD 同一条链（先 `/skin`，再资源包立绘）。

### C. `PlayerSkin` 游戏自带皮肤的旋转覆盖（已定：路 2 + 接线，已完成）

**原本的问题**：玩家皮肤分两类——在线皮肤（框架按帧自建像素集）与游戏自带皮肤（`ResolveSkin()` 拿到游戏自己的集）。旋转覆盖（`RotateOverride`，由对端皮肤描述带来）对前者用 `CharacterSpriteSetStyle` 重建即可；对后者原本只记一次警告。

**为什么"按帧重建"到不了**：框架造集走**"值 → 集"**，而 `CharacterSpriteSetStyle` 只镜像*移动*类标志；集里另有 ① 裁剪（`RemovableTrimProperty[]`，每个 trim 本身又是一整个 `CharacterSpriteSetCompact`，外加前后 trim 贴图数组与两条帧速）与 ② 三个 `Initialize` 根本不接受的序列化字段（`spriteOffsetInNoteBook`、`daySceneInteractableHighlightOffset`、`daySceneInteractableColliderAdditiveRadius`）。① 可以加镜像补上；② 无论怎么补都补不了——游戏没有写入入口（除非直接写私有序列化字段）。**而且 ② 今天就已经在所有框架自建的集上丢失**（含在线皮肤），不是自带皮肤独有的问题。

**落地（路 2）**：框架新增 `IAssetFactory.TryCopyCharacterSpriteSet(object set, CharacterSpriteSetStyle style, out CharacterSpriteSetHandle? copy)` —— 用引擎自己的 `Object.Instantiate` 复制那份集，只写调用方声明的标志；帧、裁剪、②的三个字段全部随副本保留（已用互操作成员表核实 `isHina`/`animSpeedMultiplier`/`spriteOffsetInNoteBook` 等都是可读写属性，且 `Instantiate<T>(T)` 在互操作里）。模组侧 `PlayerSkin.ApplyToUnit` 在"自带皮肤 + 旋转覆盖"时走复制（按来源集与覆盖值缓存），经 `IPresentationServices.ApplyCharacterSprite` 套用；无覆盖时仍直接交给角色（行为不变）。顺带把"把集排进场景循环套用"抽成一处（网络皮肤与复制的自带皮肤同形）。

**残留**：见 §6 第 4 项（按帧自建这条路对 ② 的丢失；在线皮肤是否有替代来源需要单独讨论）。

### D. 按帧自建的集缺的两个字段（待裁决）

**是什么**：`CharacterSpriteSetCompact` 里有两个字段既不被游戏自己的 `Initialize` 写入、也没有对应的 SDK 成员：

| 字段 | 游戏里的读者 | 作用 | 按帧自建的集拿到什么 | 游戏自带集有什么 |
| --- | --- | --- | --- | --- |
| `spriteOffsetInNoteBook`（Vector2） | `SpecialGuestDescriber.cs:280,282`、`DLC5_RogueLikePurchasePanel.cs:313,321`、`CreatorsBoxTimelineElement.cs:73` | 把角色像素画摆进这些面板时用的锚点偏移 | `Vector2.zero`（`CreateInstance` 的默认值） | 该美术资源自己的值 |
| `daySceneInteractableColliderAdditiveRadius`（float） | `CharacterConditionComponent.cs:100` | 白天交互区圆形碰撞体的附加半径 | `0` | 该美术资源自己的值 |
| （`daySceneInteractableHighlightOffset`） | `CharacterConditionComponent.cs:99,215` | 交互提示与高亮的偏移 | `Vector2.zero` | **游戏自己的 `Initialize` 就把它置零**（`CharacterSpriteSetCompact.cs:107`），所以自建集与游戏集在这一点上一致 → **不算缺口** |

**影响范围**：只有**按帧自建**的集（目前只有在线皮肤）会差；游戏自带皮肤、以及 `TryCopyCharacterSpriteSet` 的副本（保留源对象的一切）都不受影响。可见后果是几个面板里角色像素画的摆放位置、以及白天交互区提示/碰撞半径，属于小尺寸的观感差异。

**为什么之前写得比实际严重**：我把三个字段都算成了"丢值"，但其中一个（高亮偏移）是游戏 `Initialize` 主动置零的、自建集本就该是零；真正"少了一个值"的只有两个。

**选项**：

1. **把这两个当成普通样式成员**（推荐）：`CharacterSpriteSetStyle` 加 `NotebookOffset`（`Vector2`）与 `InteractableColliderAdditiveRadius`（`float`，可空），桥接在 `Build*` 里按 `style.X ?? 游戏 fallback 像素集.X` 写过去（互操作三个字段都有可写属性，已核实），`TryUnwrapCharacterSpriteSet` 顺带把它们报出来 → 于是"未声明即用游戏自带像素画的值"这句话对它们也成立，且读数-重建/复制往返无损。代价：SDK 两个成员 + 桥接约 10 行 + 契约测试；无引擎进不去单测，需实机看一眼在线皮肤在那几个面板里的摆放。
2. **只改文档**：在 `CharacterSpriteSetStyle` 文档里写明"按帧自建的集这两个字段是 0"，不补成员。零风险、零新面，但把"自建集不如游戏集"固定成现状，且模组作者无从声明自己的偏移。
3. **不动**（现状）。

**建议**：选项 1——它把两条件（自建 / 复制）在"未声明"这一点上的语义对齐，也顺手给了模组声明自己美术偏移的能力；代价很小。

## 12. 必须实机验证的清单（全部结论目前都是源码/编译级）

幽幽子挑战：阶段推进与时钟、两段刷客闸门、吞厨具重放（主机原版吞食的过滤时序）、评价 seam（改写结果/台词/倍率）、失败与重打、本体生命值镜像、终局离开场景闸门；存档载体：带模组写入 → 完全卸载 → 原版读档推进保存 → 重装校验一致（含中文/嵌套/大字符串）；资产：PNG 解码与精灵集上屏、白天地图构建与切图、Addressables 位置注册、特效/遮罩/音频、浮字与名牌、联网皮肤（含旋转覆盖）；挑战时间线挂点的启动校验；派发契约在真实 Harmony 下的表现；`AssetBundle` 的 stall 语义。

## 13. 文档地图

- **当前事实来源**：本文件、`mystia-extension-port-pass2.md`（API 形状与计划）、框架 `docs/extension-api-plan.md`（框架侧已实现面）。
- **历史记录**（不要当现状读）：`mystia-extension-port.md`、`mystia-extension-port-plan.md`、`mystia-extension-port-report.md`、`docs/port/**` 的 handoff/audit 文档。
- **需要校正**：`docs/multiplayer-architecture.md`（构建与部署段仍写 Costura/BepInEx plugins/Preloader）、`docs/mystia-extension-port-gaps.md`（首轮口径，16 个缺口已被 pass2/pass3 全面覆盖）。
