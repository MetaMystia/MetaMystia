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
| 框架测试 `dotnet test src/Mystia.Net.Sdk.Tests` | **280/280** |
| SDK 打包 `dotnet pack sdk/Mystia.Extension.Sdk/Mystia.Extension.Sdk.Pack.csproj -c Release` | 成功（`artifacts/nuget/Mystia.Extension.Sdk.2.0.0.nupkg`） |
| 样例工程 ×3（`samples/SampleMod.{A,B,Skip}`） | 0 错 0 警告（每次重打 SDK 后需重建） |
| 模组构建 `dotnet build src/MetaMystia.Mod/MetaMystia.csproj -c Debug` | **0 错 0 警告**（本轮首次全绿），产出 `MetaMystia.dll` + `mod.json` |
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
17. **最后两块面（模组清零的关键）**：`AssetBundleHandle`（`IAssetFactory.TryOpenBundle`：字节开包、名单在返回时已完整、按名字登记给表现面，包与流由框架持有）与 `ICharacterServices` 的运动/层级半边（位置、速度、运动学、碰撞体开关、层级 z，外加 `TryBindCharacter(label)`）。模组侧 `VfxBundle` 不再持有引擎对象，`NetPlayer` 不再持有 `Rigidbody2D`/`Collider2D`。顺带修掉一个真 bug：远端角色按游戏自己的参数创建后不带碰撞体，而 `PeerPlayer` 仍在每帧写那个已被销毁的碰撞体组件，会抛 `MissingReferenceException`。
18. **死代码删除**：`Utils/ExportUtils.cs`（1101 行、无调用点，其 PNG 编码器已上移 MEFX）、`ResourceEx/Registries/PixelSpriteFactory.cs`（无调用点）、`ResourceEx/Mappers/**`（779 行，无调用点）、`ResourceEx/Addressables/**`（563 行自建 Addressables 注入，只被自己的初始化调用）、`Utils/MetaMikuUtils.cs`（装箱字典 workaround，无调用点）。

### 5.1 本轮引入的行为口径变化（实机验证重点）

1. **随机流**：符卡/刷客的抽签从 `UnityEngine.Random` 改为 `System.Random.Shared` → 分布不变，但**不再扰动游戏的全局随机流**（游戏自身的随机序列因此与迁移前不同，属改善但需知悉）；`GuestSync` 两处刷客间隔由闭区间 `[min,max]` 变为 `[min,max)`。
2. **对话 `goto` 落点**：`DialogRegistry` 把 `index` 按 1 基交给框架（旧路写 0 基，整体早一行）→ 这是**修正**旧错位；若实机发现比旧版晚一行，把 `ResolveLineNumber` 的返回值减 1。
3. **一次性特效的尾巴**：`VfxBundle.PlayOneShot` 到点由"立即销毁"变为"停止发射 + 排水 6 秒"（发射窗口不变，尾巴更长）。
4. **`Spell_Mai` 的精灵读取时机**：从"`Serve` 当下捕获"改为"投掷协程内读属性"（值不会变，但时机不同）。
5. **热键配置**：类型换成 `MystiaKey`；**数字型旧值**（本仓库早期按数字写盘）会解析成未定义值 → 已加"未定义即回落默认并告警"。
6. **控制台/列表的坐标显示**：镜像 `Vector2` 的 `ToString` 是 `Vector2 { X = .., Y = .. }`，不再是引擎的 `(x, y)`——纯显示差异。
7. **`Math.Clamp` 换成 `Min/Max`**：三处上界来自运行期窗口尺寸，`Math.Clamp` 在上界小于下界时会抛，`Min/Max` 不会（与 `Mathf.Clamp` 等价）。
8. **`DialogRegistry` 的应用路径**：模组的包仍同时经**数据面**注入（线-only 的桩）与**构建器**写入同一批名字，谁在表里取决于时机；需实机确认资源包对话保住了行内动作（若没有，修法是去掉数据面的对话注入）。

## 6. 未完成的迁移

**模组侧已清零**：`Patches/`、兼容层、裸 il2cpp、反射、`UnityEngine` 全部退场，`dotnet build` 0 错并产出可交给启动器的整份目录（`MetaMystia.dll` + `MetaMystia.Network.dll` + `mod.json` + `Mystia.Net.Sdk.dll`）。剩下的是**验证与收尾**，没有待迁移的模组代码：

1. **实机验证**（§12 清单）：所有结论目前仍是源码/编译级。
2. **§11 的两条待裁决**：`CharacterSpriteSetStyle` 是否补两个"只有美术资源才带"的字段；阶段时钟是否需要"时钟启动前"的钩子。
3. **收尾**：三套测试 + `static-check.sh` + 样例工程全量验收（已随每次改动在跑）；文档校正（`docs/multiplayer-architecture.md`、`docs/mystia-extension-port-gaps.md`、`docs/port/mod-handoff.md` 里过时的句子）。

## 7. 之后的顺序

1. 实机验证（§12）：幽幽子挑战、存档载体、资产/特效/皮肤、联网皮肤，以及本轮新增的两块面（AssetBundle 开包、远端角色运动与层级）。
2. 决定 §11 剩下的两条。
3. 文档收尾与最终验收。

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

- **`ImageConversion` 整体缺失**（`LoadImage`/`EncodeToPNG` 在 shipped metadata 里连字符串都没有）：框架自带 PNG 解码器与编码器（`PngImage`／`PngWriter`），**JPEG 不支持**；模组侧的 `ExportUtils` 已删除（§5 第 11 项）。
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

1. ~~`Utils/ExportUtils.cs` 去留~~ —— 已裁决：删除，PNG 编码器上移 MEFX（已完成，见 §5 第 11 项）。
2. ~~`IPortraitProvider` 是否代理化 + 是否需要"包装游戏自带精灵集"的入口~~ —— 已裁决：两者都做（已完成，见 §5 第 7 项）。
3. ~~deps 包的公开/私有限制~~ —— 已确认：社区从游戏编译产物逆向生成的构建物料，允许存在与分发；不接受本条作为待决项，只在 §6.6 记红线（互操作不入本仓库、不进发布物）。
4. ~~`PeerPlayer` 的碰撞方案~~ —— 已裁决：方案 B（用游戏自己的参数）。已完成。
5. ~~`NoteBookSkinPortrait` 开关去留~~ —— 已裁决：让开关真生效。已完成：框架把"哪个面板在要立绘"作为 `PortraitTarget` 交给提供者，模组在笔记本上按开关过滤。
6. ~~`PlayerSkin` 游戏自带皮肤的旋转覆盖~~ —— 已裁决：路 2（复制游戏那份集）+ 接线。已完成。
7. ~~阶段时钟的拉伸要不要改成"按倍率、由框架在时钟启动那一刻施加"~~ —— **已关闭：没有缺 API**。迁移前是 Harmony prefix 在计时协程入口写 `资产 singleRoundDuration × 倍率`（`YuyukoChallengeContextPatch`，`7347e76`）；现在框架的 `SetPhaseSeconds` 把值存进 `_armed[phase]`，并在该阶段时钟的第一个步取用（`ChallengeClockSeams.StartClock`）——**写什么、何时生效都逐字一致**，模组每帧下放只是"随时早于时钟启动都有效"。所谓一帧窗口要求"阶段时钟早于营业场景循环的第一次 `Update`"，而挑战主循环在该场景里先 `yield return null` 才进阶段时钟，实际不可达。
8. ~~`CharacterSpriteSetStyle` 要不要补那两个"只有美术资源才带"的字段~~ —— **已关闭：不是回归、也没有缺 API**。迁移前模组自建像素集走的就是游戏的 `Initialize(...)`（`PixelSpriteFactory`），而那两个字都不在它的参数表里 → **旧代码同样拿到 0**；MEFX 想表达它们也能（两个成员 + 互操作字段 setter），只是没有任何调用方需要（框架为模组特殊客人建集的那条路 `GameRecords.Skins` 也走同一个 `Initialize`）。唯一差异是"模组美术 vs 游戏自带美术"，已列进 §12。

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

### E. 阶段时钟（已关闭：无缺 API）

**迁移前的原实现**（`Patches/NightScene/YuyukoChallengeContextPatch.cs`，`7347e76` 引入、`1908e4a` 前一直有效）：

```csharp
[HarmonyPatch(typeof(YuyukoBossData.__c__DisplayClass16_0))]                     // 三阶段共用闭包
[HarmonyPatch("Method_Internal_IEnumerator_Func_1_Boolean_0")]                  // = <MainChallengeLoop>g__Timing|2：计时协程入口
[HarmonyPrefix]
int originalDuration = __instance.__4__this.singleRoundDuration;   // 资产字段（基准），不是闭包当前值
__instance.thisSingleRoundDuration = originalDuration * 2;         // 写回闭包
```

**现在**：模组每帧下放 `SetPhaseSeconds(phase, BasePhaseSeconds × 倍率)`；框架存进 `_armed[phase]`，在该阶段时钟的第一个步取用（`ChallengeClockSeams.StartClock`）。⇒ 写的是同一个量（资产基准 × 倍率）、生效点是同一个（时钟启动），**逐字一致**；"每帧下放"等价于"随时早于启动都有效"，因为框架会一直持有到那一刻。所谓一帧窗口要求阶段时钟早于营业场景循环的第一次 `Update`，而主循环在该场景里先 `yield return null`，不可达。

**唯一仍值得在实机上看一眼的**：三阶段时长是否都是"基准 × 倍率"（含游戏自己在第三阶段把时长 +30 秒那一次——迁移前的 prefix 也是在那个时钟启动时用 `资产基准 × 倍率` 覆盖它，所以现在与迁移前一致）。

## 12. 必须实机验证的清单（全部结论目前都是源码/编译级）

幽幽子挑战：阶段推进与时钟、两段刷客闸门、吞厨具重放（主机原版吞食的过滤时序）、评价 seam（改写结果/台词/倍率）、失败与重打、本体生命值镜像、终局离开场景闸门；存档载体：带模组写入 → 完全卸载 → 原版读档推进保存 → 重装校验一致（含中文/嵌套/大字符串）；资产：PNG 解码与精灵集上屏、白天地图构建与切图、Addressables 位置注册、特效/遮罩/音频、浮字与名牌、联网皮肤（含旋转覆盖）；挑战时间线挂点的启动校验；派发契约在真实 Harmony 下的表现；`AssetBundle` 的 stall 语义（`TryOpenBundle`「返回时名单已完整」依赖它）；远端角色的位置/速度/层级与原来的 `rb2d` 读写是否一致、`TryBindCharacter("Self")` 在白天与营业场景都能取到句柄、**不带碰撞体**的远端角色表现；控制台与玩家列表的热键/坐标显示；`Spell_Mai` 的六件套特效与上酒节奏；资源包对话的行内动作（数据面与构建器同名注入的胜出者）。

## 13. 文档地图

- **当前事实来源**：本文件、`mystia-extension-port-pass2.md`（API 形状与计划）、框架 `docs/extension-api-plan.md`（框架侧已实现面）。
- **历史记录**（不要当现状读）：`mystia-extension-port.md`、`mystia-extension-port-plan.md`、`mystia-extension-port-report.md`、`docs/port/**` 的 handoff/audit 文档。
- **需要校正**：`docs/multiplayer-architecture.md`（构建与部署段仍写 Costura/BepInEx plugins/Preloader）、`docs/mystia-extension-port-gaps.md`（首轮口径，16 个缺口已被 pass2/pass3 全面覆盖）。

## 14. 首轮实机结果与当前阻塞（2026-10-05）

**注入路线已换成 DLL 劫持。** 进程注入不可行：游戏 `SteamPlatform` 构造函数调用 `SteamAPI_RestartAppIfNecessary`，凡不是 Steam 客户端亲自启动的进程都会被要求退出、再由 Steam 重新拉起一份未注入的副本。现在 `Mystia.Syringe.exe` 把 `Mystia.Proxy.dll` 安装为游戏目录里的 `version.dll`（`UnityPlayer.dll` 静态导入 `VERSION.dll`，且它不在 `KnownDLLs`），代理把 17 个导出全部转发给系统 DLL，并读同目录的 `Mystia.Proxy.txt` 找到启动器目录、加载 `Mystia.Bootstrap.dll`。游戏依旧由 Steam 启动，DRM 既不修改也不绕过（`Player.log` 里云存档同步正常）。

**已实测通过的部分**：`steam://run/1584090` → 游戏目录的 `VERSION.dll`（模块表实测）→ bootstrap 挂上 `il2cpp_init` → 托管宿主启动 → 桥装载 **188 个补丁方法、0 失败** → MetaMystia 0.29.3 加载成功（`host.log`：`Plugin MetaMystia is loaded!`、`CommandRegistry initialized`）。模组依赖由 `ModAssemblyResolver` 从模组目录解析，加载失败的模组只警告不致命。

**当前阻塞**：模组加载后约 5 秒，游戏进程以 `0xC0000409`（`coreclr.dll` 内的 fail-fast）退出，`host.log` 停在正常帧尾。已定位到崩溃发生在**控制台的被动气泡绘制**（`InGameConsole.DrawPassiveMode`，经 `ModLoop.OnGui` → 框架 `GlobalHost.DrawGui`）：

| 实验 | 结果 |
| --- | --- |
| 关掉整个 `GlobalHost.DrawGui()` | 188 个补丁全开仍稳定 45 s 以上 |
| 只关掉 `DrawPassiveMode` | 同上稳定 |
| 分别关掉 `CalcSize`、`Label`、`DrawTexture` | 仍崩 |
| 0 个补丁（无消息可画） | 不绘制、不崩 |

⇒ 只在“有内容要画”时崩；剩下的绘制调用只有 `GUI.color` 读写与**样式回写**（mod 写 `_logStyle.Normal.TextColor`、`_inputStyle.Normal.Background = null` 等，由 `TextStyleHandle` 在绘制那一刻落到引擎 `GUIStyle`/`GUIStyleState`）。这条 facade（`ImguiMirror`/`ImguiDrawer`）是本次迁移新写的，回写路径最可疑；`__fastfail` 绕过异常处理与 `DOTNET_DbgEnableMiniDump`，因此拿不到崩溃栈，只能二分。

**下一步**（未做）：在 `DrawPassiveMode` 内继续隔离——先只留 `GUI.color` 读写、再单独恢复样式回写；或在 `UnityTextStyle` 回写处改成“只在值真的变化时写”，把写入次数降到最低后再逐项恢复。
