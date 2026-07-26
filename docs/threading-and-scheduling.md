# 线程与调度规范

## 线程边界

项目主要存在以下执行环境：

- Unity 主线程：生命周期、场景、UI 和绝大多数游戏对象访问。
- `MpWire` IO 线程：TCP 接收、发送和连接维护。
- `Task` 或异步 IO：HTTP、文件及 WebDebugger 等外部操作。
- `CommandScheduler`：由 Unity 主线程更新的条件调度。

禁止在非主线程调用 Unity API，或读取、修改 Unity 对象及游戏对象。后台线程只能处理已明确证明线程安全的纯托管数据；相关操作必须通过 `RunOnMainThread()` 切回主线程执行。

## 网络线程

`DirectTcp` 只由 `MpWire` IO 线程驱动。IO 线程负责：

- 接受或维护连接；
- 读取和组装网络帧；
- 将入站 Action 放入 `_inbox`；
- 从 `_outbox` 取出数据并发送。

IO 线程不得直接修改玩家、场景、UI 或其他游戏状态。入站 Action 由主线程统一取出并调用 `OnReceived()`。

需要从连接事件更新游戏状态时，使用 `PluginManager.Instance?.RunOnMainThread(...)`。

## 主线程切换

`PluginManager.RunOnMainThread(Action)` 将操作加入主线程队列，由 `PluginManager.Update()` 执行。

```csharp
PluginManager.Instance?.RunOnMainThread(() =>
{
    // 访问 Unity 或游戏对象
});
```

- 后台线程只准备普通托管数据，进入主线程后再访问游戏对象。
- 不得把生命周期不明确的游戏对象跨线程长期保存后再使用。
- 主线程操作必须短小，不得在其中执行阻塞式网络或文件 IO。
- `[OnMainThread]` 目前只表达调用约束，不会自动切换线程。调用方仍需保证实际执行线程正确。

## CommandScheduler

`CommandScheduler` 适合处理依赖游戏状态的延迟执行，例如等待对象初始化、等待前一操作完成或按固定间隔执行同步任务。

- 使用 `Enqueue` 表达条件、操作和超时。
- 使用 `EnqueueKey` 合并或替换同一语义的待执行任务。
- 使用 `EnqueueInterval` 管理周期任务，并在不再需要时取消。
- 超时应记录明确原因，不能无限等待。
- 不得用调度器掩盖未知的调用时序；时序不明时先审计实际执行流。

`CommandScheduler` 在主线程更新，但这不代表入队调用本身一定来自主线程。闭包捕获游戏对象前必须确认其生命周期。

## 协程

项目的托管协程使用 `System.Collections.IEnumerator`。通过 `PluginManager.StartManagedCoroutine()` 或 BepInEx 提供的扩展启动。

当游戏 API 要求 `Il2CppSystem.Collections.IEnumerator` 时，托管迭代器必须使用 `WrapToIl2Cpp()` 转换。两种 `IEnumerator` 不可直接混用。

```csharp
private static System.Collections.IEnumerator Routine()
{
    yield return null;
}
```

不得在返回 `Il2CppSystem.Collections.IEnumerator` 的方法中直接编写 `yield return`，因为 C# 编译器生成的是托管状态机。

## 异步与 IO

- 网络、HTTP 和文件操作可以使用 `Task`、`async` 和 `try-catch`。
- IO 完成后，只将结果数据切回主线程，不要在主线程重复执行 IO。
- 禁止使用 `async void`，Unity 生命周期或事件签名强制要求时除外，并需明确处理异常。
- 不得使用 `.Result`、`.Wait()` 或其他方式阻塞 Unity 主线程。

## 检查表

- 当前代码实际运行在哪个线程。
- 是否访问 Unity 或游戏对象。
- 是否需要 `RunOnMainThread()`。
- 调度条件是否来自已确认的游戏时序。
- 是否设置超时和取消条件。
- 协程使用的是托管还是 Il2Cpp 接口。
- IO 与游戏状态更新是否明确分离。
