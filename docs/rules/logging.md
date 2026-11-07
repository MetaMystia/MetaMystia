# 日志与调用追踪规范

## AutoLog

需要日志的类型使用 `[AutoLog]`，并声明为 `partial`：

```csharp
[AutoLog]
public static partial class ExampleManager
{
    public static void Run()
    {
        Log.Info("Run");
    }
}
```

Source Generator 会为该类型生成 `LogWrapper`，日志最终写入 BepInEx。禁止使用 `UnityEngine.Debug`，也不要为普通类型手动保存 `Plugin.Instance.Log`。

`Log.Debug`、`Log.Info`、`Log.Message`、`Log.Warning`、`Log.Error` 和 `Log.Fatal` 是主要接口。`Log.LogInfo` 等兼容接口可用于现有代码；新增代码优先使用较短形式。

## 日志级别

- `Debug`：高频诊断、状态采样和仅调试需要的信息。
- `Info`：正常但具有诊断价值的状态变化。
- `Message`：启动完成、连接结果等重要正常事件。
- `Warning`：可恢复异常、输入被拒绝、降级或兼容性风险。
- `Error`：当前操作失败或状态不一致。
- `Fatal`：插件关键初始化或 Patch 应用失败，后续功能不可信。

不得通过提高日志级别强调普通流程。

## 日志内容

日志应包含定位问题所需的关键上下文，例如角色、UID、Action、场景、资源包、目标方法或状态变化。

禁止记录：

- Token、密钥和私钥；
- 未脱敏的敏感用户数据；
- 大块二进制、完整纹理或资源内容；
- 每帧重复且没有诊断价值的信息；
- 只有“进入函数”但无法说明状态的固定日志。

高频路径应使用 `Debug`、降低频率或仅在状态变化时记录。

## Action 日志

网络 Action 的发送和接收日志由 `Action` 基类统一处理。具体 Action 只在需要时覆盖日志级别、仅记录名称或精简 `ToLogString()`。

不得在每个 `Send()` 和 `OnReceivedDerived()` 中重复记录同一条收发日志。

## TracePatch

`[TracePatch]` 用于临时分析 Harmony 调用顺序。Source Generator 会生成 Prefix、Postfix 和 Finalizer，并由 `TraceLog` 维护调用栈。

- 仅在确有调用链调查需求时添加。
- 必须确认目标方法和重载签名。
- 调查完成后评估是否仍需长期保留，避免无意义扩大 Patch 面。
- Trace 输出不能替代对逆向源码和实际执行流的审计。

## 异常日志

IO 边界捕获异常时，应记录操作对象和异常信息。非 IO 逻辑不得为了记录日志而新增 `try-catch`。

记录异常时优先保留完整 `Exception` 或足以定位问题的类型与堆栈；面向用户的提示只显示可理解的简短信息。
