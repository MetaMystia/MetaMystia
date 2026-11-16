# 控制台命令规范

## 组织方式

- 每组命令放在 `Console/Commands/` 下的独立静态类中。
- 命令类提供 `public static void Register(RootCommand root)`。
- 顶级命令由 `CommandRegistry.Initialize()` 统一注册。
- 相关子命令在同一个 `Register()` 中构建并挂到父命令。

不要为单个简单命令增加额外注册器或包装层。

## 命名与参数

- 命令名使用简短、稳定的小写名称；已有下划线命令保持兼容。
- 参数使用 `Argument<T>` 明确类型。
- 可选参数必须通过默认值或 `ArgumentArity` 明确表达。
- 参数名应能直接说明含义，不使用 `arg1` 等无语义名称。
- 修改已发布命令名、层级或参数顺序前，必须考虑用户脚本和文档兼容性。

## Handler

- 简单 Handler 可以使用 Lambda；逻辑较长或可独立阅读时使用命名方法。
- 通过 `InvocationContext` 和 `ParseResult` 读取参数。
- 使用提前返回处理权限、场景、连接状态和输入错误。
- 输出使用 `ctx.Log()` 和 `ConsoleFormat`。
- 面向用户的文本使用 `TextId`，不得直接输出内部异常。
- 涉及游戏对象的 Handler 必须在主线程执行；异步 IO 完成后需切回主线程再修改游戏状态。

命令解析和调用异常由 `CommandRegistry` 在统一边界处理。具体 Handler 和业务逻辑不得自行增加 `try-catch` 隐藏错误。

## 帮助与默认行为

帮助由命令树生成。没有 Handler 的父命令自动输出子命令列表，与 `/help <命令>` 相同；父命令不再手写帮助。

## 补全与提示

命令和参数的说明以 `TextId` 传入构造函数，同时用于帮助、补全列表和输入时的用法提示。补全位置由命令树和参数顺序确定，不另行登记。

- 固定候选值：`FromAmong`（同时校验）或 `AddCompletions`（只补全）；
- 运行时候选值：`AddCompletions(_ => ...)`；
- 保留兼容但不列出的命令：`IsHidden = true`。

动态补全函数应快速、无副作用，不执行 IO，也不修改游戏状态。

固定候选值应与 Handler 实际接受的值共用同一语义，不能出现补全允许但执行拒绝的情况。

## 输出

- 普通结果使用 `ctx.Log()`。
- 错误使用 `ConsoleFormat.Err()`。
- 警告使用 `ConsoleFormat.Warn()`。
- 标题、命令和参数分别使用对应的 `ConsoleFormat` 方法。
- 被动游戏内通知仅用于需要离开控制台也能看到的状态，不替代命令返回值。
