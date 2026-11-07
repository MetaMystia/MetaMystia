# 代码风格

- 代码极简，减少包装，不写无意义的代码、转发层和调用链。没有明确的复用、隔离或抽象价值时，直接调用目标逻辑。发现有通用性的代码时，提醒用户考虑重构。
- 语义清晰时使用 `?.`、`??` 等语法糖。
- 除网络通信、文件操作等 IO 边界外，不使用 `try-catch`，不通过捕获异常掩盖逻辑错误、状态错误或尚未理解的问题。
- 不对游戏对象使用反射，依据逆向代码和 Interop 类型强类型调用，见 [il2cpp-interop](il2cpp-interop.md)。
- 日志使用 `[AutoLog]`，不使用 `UnityEngine.Debug`，见 [logging](logging.md)。
- 新增延迟、等待和周期逻辑使用协程，见 [coroutine](coroutine.md)。

## 与同级保持一致

改完后对照同一文件、同类功能的已有写法核对：

- 已有的同类实现没有注释，同模式的新实现也不加；不在文件头、类说明或文档中写明新增了某项功能。例如已有 A、B、C、D 四个同模式的实现且都没有说明，新增的 E 同样不加。
- 只有新增部分与同类在行为或约束上确有不同时，才用注释说明这一差异。
- 文档同理，见 [文档编写规则](../README.md#编写规则)。

## using 顺序

按以下分组，组间空一行，组内按字母排序：

1. `System.*`。
2. BepInEx、HarmonyLib、Il2CppInterop、Il2CppSystem、MemoryPack、UnityEngine 等框架与第三方命名空间。
3. Common、GameData、DayScene、NightScene 等游戏命名空间。
4. MetaMystia、SgrYuki 等项目命名空间。
5. `using static` 和别名。

删除未使用或重复的 `using`。
