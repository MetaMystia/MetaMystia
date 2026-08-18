using System;
using System.Linq;
using System.Reflection;

namespace SgrYuki.Utils;

public static class Functional
{

    // Note: only available for those functions are patched. Not patched functions will not appear in the stacktrace
    public static bool CheckStacktraceContains(string funcName)
    {
        var stack = new System.Diagnostics.StackTrace();
        // foreach (var frame in stack.GetFrames())
        // {
        //     var method = frame.GetMethod();
        //     Log.LogInfo($"{LOG_TAG} \t at {method.Name}");
        // }
        return stack.GetFrames().Any(frame => frame.GetMethod().Name.Contains(funcName));
    }

    public static void LogStacktrace(BepInEx.Logging.ManualLogSource Log)
    {
        string stack = Environment.StackTrace;
        string[] lines = stack.Split([Environment.NewLine], StringSplitOptions.None);

        System.Text.StringBuilder sb = new();
        // ignore this layer and upper layer
        for (int i = 2; i < lines.Length; i++)
        {
            sb.AppendLine(lines[i]);
        }

        Log.LogInfo(sb.ToString());
    }

    public static string GetCallerName(int layer = 1) => new System.Diagnostics.StackTrace().GetFrame(layer)?.GetMethod()?.Name;

    public static void ModifyReadonlyField<T, FieldT>(T instance, string fieldName, FieldT newValue)
    {
        var field = typeof(T).GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        field.SetValue(instance, newValue);
    }
}
