
using System;
using System.Collections.Generic;

namespace MetaMystia;

[AutoLog]
public partial class TraceLog
{
    public Stack<string> CallStack { get; } = new Stack<string>();
    public int Depth => CallStack.Count;

    private static void FlowLog(string message)
    {
#if DEBUG
        Log.Warning(message);
#else
        Log.Info(message);
#endif
    }

    private string GetIndent(int level)
    {
        if (level <= 0) return "";
        return new string(' ', 0) + string.Create(level * 3, 0, static (span, _) =>
        {
            for (var i = 0; i < span.Length; i += 3)
            {
                span[i] = '│';
                span[i + 1] = ' ';
                span[i + 2] = ' ';
            }
        });
    }

    public void OnPrefix(string func)
    {
        var indent = GetIndent(Depth);
        FlowLog($"{indent}┌──── {func}");
        CallStack.Push(func);
    }

    public void OnPostfix(string func)
    {
        if (CallStack.Count == 0)
        {
            Log.Error($"TraceLog Postfix on empty stack! func: {func}");
            return;
        }

        var top = CallStack.Peek();
        if (top != func)
        {
            Log.Error($"TraceLog Postfix mismatch! Expected: {top}, Actual: {func}");
            // 尝试在栈中找到匹配项并修复错位
            if (CallStack.Contains(func))
            {
                while (CallStack.Count > 0 && CallStack.Peek() != func)
                {
                    var skipped = CallStack.Pop();
                    FlowLog($"{GetIndent(Depth)}└──✕─ {skipped} (auto-closed)");
                }
            }
        }

        CallStack.Pop();
        var indent = GetIndent(Depth);
        FlowLog($"{indent}└──── {func}");
    }

    public void OnFinalizer(string func, Exception __exception)
    {
        if (__exception != null)
        {
            var indent = GetIndent(Math.Max(0, Depth - 1));
            Log.Error($"{indent}└──✕─ {func} threw {__exception.GetType().Name}: {__exception.Message}");

            // 异常时确保栈状态恢复
            if (CallStack.Count > 0 && CallStack.Peek() == func)
                CallStack.Pop();
        }
    }

    public void Reset()
    {
        if (CallStack.Count > 0)
        {
            FlowLog($"TraceLog Reset: cleared {CallStack.Count} orphaned entries");
            CallStack.Clear();
        }
    }
}
