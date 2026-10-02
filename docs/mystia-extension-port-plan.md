# MetaMystia 迁移实施计划

配套文档：[`mystia-extension-port.md`](mystia-extension-port.md)（迁移规范与验收）、`docs/mystia-extension-port-gaps.md`（缺口清单，实施后生成）。

本文只记录**怎么做**与**并行化契约**，不重复规范里的迁法与验收标准。全部结论均来自源码审计（未做游戏内实测）。中间件侧的能力清单见 MystiaExtensionFramework 的 `docs/extension-api-plan.md`。

## 1. 决策记录

| 项 | 决定 |
| --- | --- |
| 可拦截钩子签名 | 统一 `void OnPreXxx(..., ref bool cancelInvocation)`；需要改写值时该值本身为 `ref` |
| 协程 | 框架自建托管泵；句柄与等待令牌为不透明类型（`CoroutineHandle` / `CoroutineAwait`） |
| 配置 | 只读走 `ICommonServices.Config`，写入与持久化走 `ICommonServices.Caching` |
| 运行时立绘 | `[AutoWire] IPortraitProvider.TryResolvePortrait(ClothesProfile.Clothes clothes, out Sprite sprite)`，按模组顺序询问，命中后写 `overrideSprite` 并放行原版 |
| 故事回放 | 保留；`CollabBehaviourComponentPatch` 与 `DaySceneUIManagerPatch` 由缺口升为迁移项，框架补「聊天菜单构造」扩展点 |
| Result 场景 `GuestsManager.Initialize` | 删除（Result 场景无该组件，`MonoSingleton` 会凭空造空壳）；记入行为变更 |
| 缺口文件 | 集中到 `Patches/Compat/`，静态检查按白名单排除 |
| 本地 SDK 引用 | `nuget.config` 用相对路径指向兄弟仓库的 `artifacts/nuget`；MEFX 发布到 nuget.org 后改回普通包引用 |
| 游戏互操作 | 用 `Mystia.InteropGen` 从游戏工程的 `Library/ScriptAssemblies` 生成，不复用 BepInEx 的 `interop` |

## 2. 环境与工具链（Phase 0）

1. 扩展 `Mystia.InteropGen`：Source 取游戏工程 `Library/ScriptAssemblies`；`UnityBaseLibsDir` 与 Source 分离（Unity 编辑器 `Data/Managed/UnityEngine` 或 BepInEx `unity-libs`）；输出 `artifacts/interop`。
2. `dotnet pack sdk/Mystia.Extension.Sdk -c Release`，产物进 `artifacts/nuget`。
3. 本仓库新建 `nuget.config`：nuget.org + 相对路径本地源。
4. 本仓库新建（已被忽略）`MetaMystia.local.props`（`BepInExPath`、`MystiaInteropDir`）与 `AGENTS.local.md`（游戏工程路径）。
5. 迁移前基线：`dotnet build src/MetaMystia.Mod -p:DeployToGame=false`、`dotnet run --project src/MetaMystia.Network.Tests`、`dotnet run --project src/MetaMystia.Flow.Tests`，记录既有失败。

## 3. 中间件能力扩展（Phase 1）

按工作包（WP）划分；每个 WP 只创建自己的新文件，共享文件由主线统一改动（见第 5 节）。

| WP | 内容 | 产出 |
| --- | --- | --- |
| WP-M0 | 工具链：InteropGen 双目录支持、SDK 打包 | `src/Mystia.InteropGen/Program.cs`（改）、`artifacts/` |
| WP-M1 | 全局宿主：`IGlobalGameLoop`（含 `FixedUpdate`）、`IGlobalServices`、`IIMGUIProvider` 与 `IIMGUIDrawer`（转发 `GUI`/`GUILayout`/`GUIUtility`/`Event`/`Screen`） | SDK：`GlobalLoops.cs`、`Imgui.cs`；桥接：`GlobalHost.cs` |
| WP-M2 | 协程：`ICoroutineDispatcher`、不透明 `CoroutineHandle`/`CoroutineAwait`、托管泵（解释 `null`／`CoroutineAwait`／`WaitForSeconds`／`WaitForSecondsRealtime`／`WaitForEndOfFrame`／`WaitForFixedUpdate`／嵌套 `IEnumerator`／`Il2CppSystem.Collections.IEnumerator`） | SDK：`Coroutines.cs`；桥接：`CoroutinePump.cs` |
| WP-M3 | 宿主能力：`Caching`、`Config`、`ILog` 扩展（`Message`/`Fatal`/`Log(LogLevel,…)` 与模组 `Id`/`Version`）、`Dialogs`、`Records` | SDK：`ModStorage.cs`、`DialogCatalog.cs`、`GuestRecords.cs`；桥接：`DialogCatalogHost.cs` |
| WP-M4 | 监听管线：15 项（11 个 `OnPre*` 与 4 个通知），全部 `[AutoWire]` | SDK：`Listeners.cs`（改）；桥接：`ListenerSeams.cs`（新） |
| WP-M5 | 服务与开关：`SetSeatingEnabled`、`IWorkSceneIzakaya.SetCloseEnabled`、`IWorkSceneCook.SetCallEnabled`、`Swap(..., onFinished)`、`BeginOrderSession`、`OpenDialog(package, onFinished, replaceText)` | SDK：`SceneLoops.cs`（改）；桥接：`SceneServices.cs`（改） |
| WP-M6 | 数据注入：6 个新 `OnInject*` 与 4 个 DB seam、结构字段（kizuna 28、对话 actions 与行级 flag、`NpcData.Name`、`SpecialGuestData` 扩展、`GuestRequestLine.Enable`）、3 张映射表（写本模组 id）、商人完整链路（`allMerchants` 与 `trackedMerchants` 双写、缺 key 安全、已拥有食谱过滤）、`IPortraitProvider` | SDK：`Database.cs`（改）、`Portraits.cs`；桥接：`DatabaseInject.cs`（改）、`MerchantPipeline.cs`、`PortraitProviders.cs` |

依赖：WP-M1/M2/M3 需要接线到同一批共享文件（`CommonServices`、`RuntimeInstall.cs`）；WP-M4/M5/M6 相互独立。

## 4. 模组迁移（Phase 2 至 4）

| WP | 内容 | 允许改动的范围 |
| --- | --- | --- |
| WP-A | 工程与入口：SDK 工程化、`net10.0`、`mod.json`、删除 Costura/Fody/Preloader、`Network`/`Server`/测试工程升框架、`LogGenerator` 改 `ILog`、`TraceGenerator` 删除、`Plugin`/`PluginHost` 换入口 | `*.csproj`、`Network.props`、`Versions.props`、`mod.json`、`Plugin.cs`、`PluginHost.cs`、`Managers/PluginManager.cs`、`Utils/SgrYuki/LogWrapper.cs`、`MetaMystia.Generators/*` |
| WP-B | 场景进入与白天：`ISceneListener`、`IDayListener`、`IDayInputListener`、`Swap`、邀请记录、快进提交、聊天菜单、商店面板 | `Patches/SceneManager/*`、`Patches/DayScene/*`、`Patches/Common/UniversalGameManagerPatch.cs` |
| WP-C | 备菜：`IPrepListener`、`IPrepNightMenuServices`、`IPrepNightSessionServices`、`OnConfigTabSelected`、`OnConfigureUpdated` | `Patches/PrepScene/*` |
| WP-D | 营业：`IWorkListener`、`ICookListener`、`IGuestGroupListener`、`IGuestSpawnModifier`、各开关与服务、服务面板钩子、时制 | `Patches/NightScene/*`（不含 `Yuyuko*`）、`Patches/Common/CharacterControllerInputGeneratorComponentPatch.cs` |
| WP-E | 数据注入：ResourceEx 改经 `IDatabaseExtension`（商人、地图、NPC、符卡、任务、服饰、Buff），立绘改路径与 `IPortraitProvider` | `Patches/DataBase/*`、`Patches/CoreLanguage/*`、`Patches/RunTime/*`、`ResourceEx/Registries/*`、`ResourceEx/Core.cs` |
| WP-F | 清理与交付：删除已迁类型与 `PatchRegistry` 条目、缺口清单、行为变更清单、三列对照表 | `Patches/PatchRegistry.cs`、`docs/mystia-extension-port-gaps.md`、`Patches/Compat/` |

WP-E 可再分：E1 食材/食物/饮料/菜谱/道具/徽章；E2 特殊客人/NPC/对话；E3 商人；E4 地图/任务/事件/符卡/服饰/Buff。WP-B 与 WP-D 同样可按补丁文件再分。

## 5. 并行化契约

- 单写者文件（只由主线改动）：`Patches/PatchRegistry.cs`、`MetaMystia.sln`、`MetaMystia.local.props`、`Patches/HarmonyPrefixFlow.cs`、`Patches/PatchBypassToken.cs`；中间件侧：`CommonServices` 接线处、`RuntimeInstall.cs`。
- 工作包自持文件：每个 WP 只在自己列出的范围内改动；同一文件属于两个 WP 时，由主线先拆分或串行处理。
- 新增优先：中间件的监听与服务实现尽量落到新文件（`ListenerSeams.cs`、`MerchantPipeline.cs`、`PortraitProviders.cs`、`CoroutinePump.cs`、`GlobalHost.cs`），减少与既有 `HarmonySeams.cs` 的冲突。
- 合并点：每个 WP 完成后由主线执行一次全量编译与静态检查，再做下一批；跨 WP 的接口签名在施工前冻结。
- 验证分层：可编译性与静态检查由主线统一跑；子代理只报告自己改动范围内的事实与未决疑点。
- 不做无法合并的猜测性改动；游戏逻辑存疑时以游戏工程源码为准。

## 6. 验收

- 命令：`dotnet build src/MetaMystia.Mod/MetaMystia.csproj`、`dotnet run --project src/MetaMystia.Network.Tests`、`dotnet run --project src/MetaMystia.Flow.Tests`。规范写的 `dotnet test` 与这两个 Exe 型工程不符，两种都实测并在交付说明记录结论。
- 静态检查：`src/MetaMystia.Mod` 无 `BepInEx`／`HarmonyLib`／`HarmonyPatch`／`BasePlugin`（缺口文件除外且在清单内）；无 `IGuestDirector` 实现；无作用域外的场景服务调用；`ref` 参数不进 lambda；特殊客人立绘是路径而非 `Sprite`。
- 交付物：三列对照表（原补丁类型／归宿／是否编译通过）、缺口清单、行为变更清单。

## 7. 行为变更与边界

已知行为变更：

- Result 场景删除 `GuestsManager.Initialize` 调用（原先会在无该组件的场景重建空壳管理器）。
- ResourceEx 的 NPC 注入改经框架写入，`SpecialGuestRegistry.RegisterNPCs` 由空实现变为真实注入，属行为增量。
- ResourceEx 中依赖 kizuna、对话 actions 的内容随数据面扩展而启用。
- 日志改走框架 `ILog`，落点与可见性与原 BepInEx／游戏控制台不同。
- 缺口补丁（约 27 项）仍以兼容形式存在，其行为不变。

不做：游戏内实测（除非另行要求）、网络协议改动、ResourceEx 包格式改动、控制台命令对外行为改动、`.github/workflows/ci.yml` 调整（依赖外部依赖包与 SDK 包，需配套改动）。
