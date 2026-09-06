# AI 开发规则

对于涉及游戏运行逻辑的代码，必须严格审计对应的游戏逆向代码，不得猜测。逆向代码仓库的位置请参考 `AGENTS.local.md`。

负责开发的 AI 通常只需审计已有逆向代码，无需参考 `$il2cpp-to-csharp`。只有任务明确要求进行 IL2CPP 逆向分析或源码还原时，才应使用该技能。

如果缺少逆向仓库或相关代码，必须向用户寻求帮助；如果用户也不知道或无法获取，请让用户向 MetaMiku 寻求帮助。

## AI test 实践（按需）

默认 Agent 只需完成用户要求的开发任务，无需开展游戏内 AI 测试或验证实践，也无需主动要求用户启动游戏。只有用户明确要求游戏实测、操作验证或探索时，才开展以下实践。此约定不免除开发任务本身要求的源码审计、编译和必要测试。

开展实践前阅读 [AI 测试实践](src/MetaMystia.AITest/README.md)。该目录保留一份主文档和多个小载荷，用实际游戏操作帮助理解行为、复现问题、检查修改结果，目前为概念验证。

### 实践步骤

1. 明确玩家目标、允许改变的状态和完成条件。接手时重新读取场景、地图、面板和必要状态，不沿用上轮对象 ID、坐标或内存变量。
2. 先读对应逆向代码，再核对 Interop 接口；未知逻辑不靠猜测补齐。
3. 优先使用玩家互动、按钮提交和游戏原有流程。若直接调用配置或业务方法，记录跳过了哪些 UI 检查，不能据此宣称完整玩家操作已通过。
4. 每次只完成一个可核验动作。等待对话、协程或切图结束，再比较前后状态；购买、邀请、委托等请求结果不明时先查状态，避免重复执行。
5. 把观察反馈给开发：记录前置条件、操作入口、预期、实测差异、对应代码、验证范围。用户要求验证修复时，重放最小场景并检查相关边界情况。

### 证据与脚本

- 明确标注“运行时实测”“源码推断”“待验证”。按钮返回成功、界面台词、状态登记和夜间实际效果是不同证据，不能互相替代。
- 默认返回与当前问题有关的摘要，保留角色标签、资源 ID 和状态变化。显示名不唯一，隐藏缓存对象和重复文本不能当作多个实体或多次收益。
- 通用观察、通用操作与场景专用载荷分开。脚本写明适用场景、参数和核验方式，不将某次路线和商品 ID 包装成通用规则。
- 所有调试 HTTP 请求，包括 `/help` 和只读请求，均须由执行工具提权审批。Token 仅作本次参数，禁止写进脚本、文档或输出记录。
- 用户关闭游戏后只整理已有材料，不重连或重启。离线检查与游戏实测分开报告；单机成功不能推出主客机一致。

## 开发提示

游戏逆向仓库是通过 Il2CppDumper 导出，并结合人工与 AI 逆向还原的 C# 代码，主要用于理解游戏逻辑，但可能不完全准确，应结合上下文进行合理质疑和交叉验证。

逆向代码与实际运行时接口存在差异：逆向代码保留 `public`、`private`、`protected` 等访问级别，以及较完整的协程、异步和匿名函数结构；项目实际使用的是 BepInEx/Il2CppInterop 生成并由项目引用 DLL 提供的壳代码，其中成员均以 `public` 形式暴露，协程和异步逻辑会被拆分，匿名函数等编译器生成结构会使用特殊名称。

逆向代码用于审计游戏行为，项目引用的 Interop DLL 是编译和调用的实际依据。禁止因逆向代码中的访问级别而使用反射。相关差异和映射规则将由独立参考文档补充。

## 开发参考

- Harmony Hook：[`docs/harmony-hook-style.md`](docs/harmony-hook-style.md)
- 网络 Action：[`docs/network-action-style.md`](docs/network-action-style.md)
- 线程与调度：[`docs/threading-and-scheduling.md`](docs/threading-and-scheduling.md)
- 协程：[`docs/coroutine-style.md`](docs/coroutine-style.md)
- Il2CppInterop：[`docs/il2cpp-interop-guide.md`](docs/il2cpp-interop-guide.md)
- Il2CppInterop 缺陷：[`docs/il2cppinterop-defects.md`](docs/il2cppinterop-defects.md)
- 日志与调用追踪：[`docs/logging-and-tracing.md`](docs/logging-and-tracing.md)
- ResourceEx 资源包：[`docs/resourceex-package-contract.md`](docs/resourceex-package-contract.md)
- ResourceEx 模块结构：[`docs/resourceex-module-structure.md`](docs/resourceex-module-structure.md)
- 本地化：[`docs/localization-rules.md`](docs/localization-rules.md)
- 控制台命令：[`docs/console-command-style.md`](docs/console-command-style.md)

## 代码风格

- 代码务必极简，减少包装，禁止无意义的代码、转发层和调用链。没有明确复用、隔离或抽象价值时，应直接调用目标逻辑。发现具有通用性的代码时，应适时提醒用户考虑重构。
- 在语义清晰且不影响可读性时，鼓励使用 `?.`、`??` 等 C# 语法糖简化代码。
- `CommandScheduler` 计划弃用，应减少使用。新增延迟、等待和周期逻辑优先使用协程；仅在维护现有调度代码或确有兼容需要时继续使用。
- 除网络通信、文件操作等 IO 边界外，禁止使用 `try-catch`。不得通过捕获异常掩盖逻辑错误、状态错误或尚未理解的问题。
- 禁止对游戏相关对象使用反射。必须依据逆向代码和 `BepInEx/interop/` 提供的类型与成员进行强类型调用。
- 禁止使用 `UnityEngine.Debug` 等 Unity 日志接口。应使用项目的 `AutoLog`，通过 BepInEx 日志系统记录信息。

### using 顺序

`using` 必须按以下顺序分组，组间空一行，组内按字母排序：

1. `System.*`。
2. BepInEx、HarmonyLib、Il2CppInterop、Il2CppSystem、MemoryPack、UnityEngine 等框架与第三方命名空间。
3. Common、GameData、DayScene、NightScene 等游戏命名空间。
4. MetaMystia、SgrYuki 等项目命名空间。
5. `using static` 和别名。

删除未使用或重复的 `using`，不得为排序拆散同一命名空间组。

## 文档编写规则

所有新增文档的主体必须使用中文，专有名词、类名、API、命令、文件路径和代码可以保留英文。

无论使用何种语言，内容都必须简洁明了、清晰易懂、言简意赅，并保持中立客观。禁止包装、美化、夸张或使用宣传性表述。

所有将被 Git 记录的文档，禁止包含任何可能泄露本机信息的路径。项目目录内的路径必须使用相对路径；项目目录外的路径必须记录在不提交到 Git 的 `*.local.md` 文件中。
