# 迁移施工约定

读者：接手 MetaMystia 迁移的代理或开发者。本文不依赖任何会话上下文，是 `docs/port/` 全套工单的公共部分。

- 迁移规范与验收：[`../mystia-extension-port.md`](../mystia-extension-port.md)
- 实施计划与并行化契约：[`../mystia-extension-port-plan.md`](../mystia-extension-port-plan.md)
- 审计事实：[`audit-facts.md`](audit-facts.md)
- 中间件工单：[`mefx-handoff.md`](mefx-handoff.md)
- 模组工单：[`mod-handoff.md`](mod-handoff.md)
- 执行顺序与并行批次：[`execution-order.md`](execution-order.md)

## 1. 事实分级

所有结论必须标注来源：

- **源码事实**：引用文件与行号，例如 `WorkSceneServePannel.cs:93-94`。
- **源码推断**：由代码推得、未在游戏内验证，必须写明「推断」。
- **待确认**：无法从代码确定的，写进对应工单的「未决疑点」，不得用猜测补齐。

本任务不做游戏内实测，任何行为结论都只能是「源码推断」，不得写成已实测。

## 2. 仓库与路径

- 本仓库：MetaMystia（`docs/` 之外的一切代码）。
- 中间件仓库与游戏工程目录：见仓库根目录的 `AGENTS.local.md`（不入 Git）。
- 文档、提交内容一律使用相对路径或「游戏工程」「中间件仓库」这类称呼，禁止写入任何本机路径。
- 游戏逻辑的唯一权威是游戏工程源码；本仓库 `docs/` 里的旧结论若与源码冲突，以源码为准并在工单里记录冲突。

## 3. 环境

1. `MetaMystia.local.props`（被忽略）提供 `BepInExPath` 与 `MystiaInteropDir`。
2. `nuget.config` 用相对路径把中间件的 `artifacts/nuget` 作为本地源。
3. 互操作由中间件的 `Mystia.InteropGen` 从游戏工程的 `Library/ScriptAssemblies` 生成到中间件 `artifacts/interop`，`MystiaInteropDir` 指向它。
4. SDK 由 `dotnet pack sdk/Mystia.Extension.Sdk -c Release` 产出。
5. 构建与测试（在本仓库根目录）：
   - `dotnet build src/MetaMystia.Mod/MetaMystia.csproj`
   - `dotnet run --project src/MetaMystia.Network.Tests`
   - `dotnet run --project src/MetaMystia.Flow.Tests`
   - 中间件侧：`dotnet build MystiaExtensionFramework.slnx`
   - 本地编译不得把产物写进游戏目录；旧工程沿用 `-p:DeployToGame=false`。

## 4. 代码风格与硬性约束

遵循仓库根 `AGENTS.md`，迁移相关要点：

- 代码极简，禁止无意义的包装、转发层和调用链；有复用/隔离价值才抽象。
- 除网络通信、文件操作等 IO 边界外禁止 `try-catch`；不得用捕获异常掩盖未理解的逻辑。
- 禁止对游戏对象使用反射。BepInEx/Il2CppInterop 生成的壳代码把成员统一暴露为 `public`，私有成员同样直接调用。
- 禁止 `UnityEngine.Debug`；日志走项目的 `AutoLog`（`[AutoLog]` 类里的 `Log`）与框架 `ILog`。
- `CommandScheduler` 弃用；延迟、等待、周期逻辑改协程（`ICoroutineDispatcher`）。
- `using` 按仓库规则分组排序，删除未使用的。
- 新增延迟/等待/周期逻辑不得引入新的队列或调度器。

迁移附加约束：

- `src/MetaMystia.Mod` 不再新增 Harmony；新代码只使用中间件公开契约。
- 可拦截钩子一律 `void OnPreXxx(..., ref bool cancelInvocation)`，`true` 含义是「不取消」；值改写用 `ref` 参数本身。
- `ref` 参数不得被 lambda 捕获；桥接按玩家顺序用同一变量调用各实现。
- 场景服务只在 `Setup`/`Update`/`Shutdown` 内有效；作用域外的需求走 `IGlobalGameLoop`／`IGlobalServices`／`ICommonServices` 的非作用域成员。
- 不实现 `IGuestDirector`。跳过原版刷客用 `IWorkSceneGuests.SetSpawnEnabled(false)` 后在 `Update` 里主动生成。
- 进入场景时该场景的暂停开关会恢复为允许；要关掉原版行为在 `Setup` 里关。
- 通过服务再次调用游戏方法时桥接会自动放行，不要写反向补丁。

## 5. 静态检查清单（每个工作包完成后必须自查）

- `src/MetaMystia.Mod` 内无 `BepInEx`、`HarmonyLib`、`HarmonyPatch`、`BasePlugin`（`Patches/Compat/` 下的缺口文件除外，且必须出现在缺口清单里）。
- 无 `IGuestDirector` 实现。
- 无在 `Setup`／`Update`／`Shutdown` 之外保存或调用场景服务的代码。
- `OnGroupOrdered`／`OnGroupEvaluated` 等 `ref` 监听没有把 `ref` 参数传进 lambda。
- 新增特殊客人的立绘字段是相对路径字符串，不是 `Sprite`，也没有改写原版角色的立绘数组。
- 无新增 `HarmonyReversePatch`；仍依赖反向补丁的项必须出现在缺口清单并注明。

## 6. 缺口文件

- 缺口补丁集中到 `src/MetaMystia.Mod/Patches/Compat/`，保持原行为，不得顺手改写。
- 每个缺口文件在 `docs/mystia-extension-port-gaps.md` 有一条：文件名、原方法、现有前缀是观察还是跳过、缺的是哪一类回调、是否仍依赖反向补丁。
- 缺口清单与 `Compat/` 目录同属一个工作包（WP-F），不得由其它包增删。

## 7. 单写者文件（主线独占，子任务禁止改动）

- `src/MetaMystia.Mod/Patches/PatchRegistry.cs`
- `MetaMystia.sln`、`MetaMystia.local.props`、`nuget.config`
- `src/MetaMystia.Mod/Patches/HarmonyPrefixFlow.cs`、`src/MetaMystia.Mod/Patches/PatchBypassToken.cs`
- 中间件侧：`CommonServices` 接线处所在文件、`src/Mystia.Modding.Bridge/Game/RuntimeInstall.cs`

## 8. 禁止事项

- 不改网络协议、ResourceEx 包格式、控制台命令的对外行为。
- 不用新的 Harmony 补丁补能力缺口。
- 不为了编译通过而注释掉业务逻辑；无法表达的行为写进缺口清单。
- 不修改与当前工作包无关的文件。

## 9. 完成定义与汇报格式

完成定义：本工作包范围内编译通过（含全解决方案）、静态检查通过、工单里的验收项逐条有结论。

汇报格式（每个工作包结束时）：

1. 改动文件清单（新增/修改/删除）。
2. 新增或消费的公开签名。
3. 验收项逐条结果（命令与结论）。
4. 未决疑点与「待确认」项（带证据引用）。
5. 与工单不一致之处（若工单有误，先报告再改）。
