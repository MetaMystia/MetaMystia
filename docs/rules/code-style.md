# 代码风格

- 代码极简，减少包装，不写无意义的代码、转发层和调用链。没有明确的复用、隔离或抽象价值时，直接调用目标逻辑。发现有通用性的代码时，提醒用户考虑重构。
- 语义清晰时使用 `?.`、`??` 等语法糖。
- 除网络通信、文件操作等 IO 边界外，不使用 `try-catch`，不通过捕获异常掩盖逻辑错误、状态错误或尚未理解的问题。
- 不对游戏对象使用反射，依据逆向代码和 Interop 类型强类型调用，见 [il2cpp-interop](il2cpp-interop.md)。
- 日志使用 `[AutoLog]`，不使用 `UnityEngine.Debug`，见 [logging](logging.md)。
- 新增延迟、等待和周期逻辑使用协程，见 [coroutine](coroutine.md)。

## using 顺序

按以下分组，组间空一行，组内按字母排序：

1. `System.*`。
2. BepInEx、HarmonyLib、Il2CppInterop、Il2CppSystem、MemoryPack、UnityEngine 等框架与第三方命名空间。
3. Common、GameData、DayScene、NightScene 等游戏命名空间。
4. MetaMystia、SgrYuki 等项目命名空间。
5. `using static` 和别名。

删除未使用或重复的 `using`。
