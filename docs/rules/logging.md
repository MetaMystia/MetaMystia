# 日志与调用追踪规范

## Log

模组代码直接调用静态类 `MetaMystia.Log`，日志最终写入 BepInEx：

```csharp
public static class ExampleManager
{
    public static void Run(int id)
    {
        Log.Info($"Run {id}");
    }
}
```

输出为 `[时间] [标签] 正文`，标签是调用处的源文件名，到第一个 `.` 为止。正文不再重复类名。

- 插值字符串在级别未被监听时不求值，插值中不得有副作用。
- 级别由变量决定时用 `Log.Write(level, $"...")`。
- 禁止使用 `UnityEngine.Debug`，不要手动保存 `Plugin.Instance.Log`。`Plugin` 类中的 `Log` 是 BepInEx 的属性，应写 `MetaMystia.Log`。
- MystiaLib 和 `RuntimeAddressables` 使用各自的日志源。

## 日志级别

- `Debug`：高频诊断、状态采样和仅调试需要的信息。
- `Info`：正常但具有诊断价值的状态变化。
- `Message`：启动完成、连接结果等重要正常事件。
- `Warning`：可恢复异常、输入被拒绝、降级或兼容性风险。
- `Error`：当前操作失败或状态不一致。
- `Fatal`：插件关键初始化或 Patch 应用失败，后续功能不可信。

不得通过提高日志级别强调普通流程。

资源注册每项只在最终结果记一条 `Info`，中间步骤用 `Debug`；对话包只记总数。

## 日志内容

正文使用英文，包含定位问题所需的关键上下文，例如角色、UID、消息类型、场景、资源包、目标方法或状态变化。

禁止记录：

- Token、密钥和私钥；
- 未脱敏的敏感用户数据；
- 大块二进制、完整纹理或资源内容；
- 每帧重复且没有诊断价值的信息；
- 只有“进入函数”但无法说明状态的固定日志。

高频路径应使用 `Debug`、降低频率或仅在状态变化时记录。

## 消息日志

联机消息的收发日志由 `MultiplayerMessage` 基类统一记录。具体消息只在需要时覆盖日志级别、仅记录名称或精简 `ToLogString()`。

不得在每个 `Send()` 和 `OnReceivedDerived()` 中重复记录同一条收发日志。

## TracePatch

`[TracePatch]` 用于临时分析 Harmony 调用顺序。Source Generator 会生成 Prefix、Postfix 和 Finalizer，并由 `TraceLog` 维护调用栈。追踪方法只存在于 Debug 构建。

- 仅在确有调用链调查需求时添加。
- 必须确认目标方法和重载签名。CI 只对 Release 产物做 ICF 检查，新增目标后对 Debug 产物运行 `tools/icf check`。
- 调查完成后评估是否仍需长期保留，避免无意义扩大 Patch 面。
- Trace 输出不能替代对逆向源码和实际执行流的审计。

## 异常日志

IO 边界捕获异常时，应记录操作对象和异常信息。非 IO 逻辑不得为了记录日志而新增 `try-catch`。

记录异常时优先保留完整 `Exception` 或足以定位问题的类型与堆栈；面向用户的提示只显示可理解的简短信息。
