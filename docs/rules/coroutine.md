# 协程规范

## 类型与启动

项目协程使用 `System.Collections.IEnumerator`：

- 全局或跨场景任务由持久化的 `PluginManager` 承载，通过 `PluginManager.StartManagedCoroutine()` 启动。
- 与具体游戏对象生命周期一致的任务，使用 BepInEx 提供的扩展挂在对应 `MonoBehaviour` 上。
- 协程必须从主线程启动，并由 Unity 主线程推进。

游戏侧协程接口使用 `Il2CppSystem.Collections.IEnumerator`，而 C# 中通过 `yield return` 编写的 Mod 协程会生成 `System.Collections.IEnumerator`。托管协程必须通过 BepInEx 提供的启动扩展或 `WrapToIl2Cpp()` 适配后交给游戏运行时，两种 `IEnumerator` 不可直接混用。

```csharp
private static System.Collections.IEnumerator Routine()
{
    yield return null;
}
```

不得在返回 `Il2CppSystem.Collections.IEnumerator` 的方法中直接编写 `yield return`，因为 C# 编译器生成的是托管状态机。

普通方法中的 `yield return` 也可能只是数据迭代器。只有交给 Unity 协程入口执行的 `IEnumerator` 才属于协程，不得仅凭语法判断。

## 等待与控制流

- 使用 `yield return null` 等待下一帧。
- 使用 `WaitForSeconds` 等待固定时间。循环复用同一等待对象时，应在循环外创建。
- 延迟、条件等待和周期逻辑直接通过协程控制流表达，不得仿照 `CommandScheduler` 新增队列或调度包装。
- 不得用协程掩盖未知的调用时序；时序不明时先审计实际执行流。

## 生命周期

- 无限循环必须有明确的宿主生命周期或退出条件。
- 需要主动取消、替换或去重时，应保存 `Coroutine` 句柄并停止旧协程。
- 每次从 `yield` 恢复后，必须重新确认可能已销毁的 Unity 对象和游戏对象仍然有效。
- 不得将生命周期不明确的游戏对象长期捕获在协程中。

## 检查表

- 协程使用的是托管还是 Il2Cpp 接口。
- 宿主是否与任务生命周期一致。
- 是否从主线程启动。
- 是否存在明确的结束或取消方式。
- 恢复执行后是否重新检查对象有效性。
- 是否可以直接表达时序而不增加包装。
