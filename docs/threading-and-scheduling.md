# 线程与调度规范

## 线程边界

项目主要存在以下执行环境：

- Unity 主线程：生命周期、场景、UI 和绝大多数游戏对象访问。
- `MpWire` IO 线程：TCP 接收、发送、组帧、反序列化、直连转发和连接维护。
- `Task` 或异步 IO：HTTP、文件及 WebDebugger 等外部操作。
- 协程：由 Unity 主线程推进的延迟、等待和周期逻辑。
- `CommandScheduler`：计划弃用的旧条件调度。

禁止在非主线程调用 Unity API，或读取、修改 Unity 对象及游戏对象。后台线程只能处理已明确证明线程安全的纯托管数据；相关操作必须通过 `RunOnMainThread()` 切回主线程执行。

## 网络线程

`DirectTcp` 只由 `MpWire` IO 线程驱动。IO 线程负责：

- 接受或维护连接；
- 读取字节并由 `PacketBuffer` 组帧和反序列化；
- 在 DirectHost 模式下按 Relay 标记转发 Action；
- 将已反序列化的 Action 放入 `_inbox`；
- 从 `_outbox` 取出数据并发送。

IO 线程不得直接修改玩家、场景、UI 或其他游戏状态。`PluginManager.Update()` 在主线程调用 `MpWire.FlushInbox()`，再由 `NetActionDispatcher` 执行接收约束和对应的 `Behavior.Handle()`。

出站 Action 在调用 `Enqueue()` 的线程完成序列化，再进入并发队列。`Send()` 不会自动切换线程；从 IO 线程或后台任务发送时，只能构造和读取纯托管且线程安全的数据。普通玩法 Behavior 应从主线程读取游戏状态后再发送。

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

`PluginManager.Update()` 先执行 `RunOnMainThread()` 队列，再处理网络 `_inbox`。不得依赖两个队列跨帧交错的未声明顺序；需要严格先后关系时，应在同一个主线程回调或 Behavior 中明确表达。

## CommandScheduler

`CommandScheduler` 计划弃用，应减少使用。新增延迟、等待和周期逻辑优先使用协程。仅在维护现有调度代码或确有兼容需要时继续使用。

- 不得为新增逻辑扩展 `CommandScheduler` 接口或增加包装层。
- 修改现有调用时，应评估能否直接迁移为协程。
- 超时应记录明确原因，不能无限等待。
- 不得用调度器掩盖未知的调用时序；时序不明时先审计实际执行流。

`CommandScheduler` 在主线程更新，但这不代表入队调用本身一定来自主线程。闭包捕获游戏对象前必须确认其生命周期。

## 协程

新增延迟、条件等待和周期逻辑优先使用协程。具体规则见 [`coroutine-style.md`](coroutine-style.md)。

## 异步与 IO

- 网络、HTTP 和文件操作可以使用 `Task`、`async` 和 `try-catch`。
- IO 完成后，只将结果数据切回主线程，不要在主线程重复执行 IO。
- 禁止使用 `async void`，Unity 生命周期或事件签名强制要求时除外，并需明确处理异常。
- 不得使用 `.Result`、`.Wait()` 或其他方式阻塞 Unity 主线程。

## 检查表

- 当前代码实际运行在哪个线程。
- 是否访问 Unity 或游戏对象。
- 是否需要 `RunOnMainThread()`。
- Action 构造和序列化是否只读取线程安全数据。
- 入站逻辑是否由 Dispatcher 在主线程执行。
- 是否可以使用协程替代 `CommandScheduler`。
- 调度条件是否来自已确认的游戏时序。
- 是否设置超时和取消条件。
- 是否遵循协程规范。
- IO 与游戏状态更新是否明确分离。
