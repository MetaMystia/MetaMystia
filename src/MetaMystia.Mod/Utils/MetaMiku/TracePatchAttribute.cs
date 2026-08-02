using System;

#nullable enable

namespace MetaMystia;

/// <summary>
/// 标记需要自动生成 Harmony Prefix/Postfix/Finalizer 追踪方法的目标方法。
/// 放在 HarmonyPatch 类上，配合 Source Generator 自动生成调用栈追踪代码。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class TracePatchAttribute : Attribute
{
    public string MethodName { get; }

    /// <summary>追踪日志中显示的名称，默认为 "目标类名.方法名"</summary>
    public string? DisplayName { get; set; }

    /// <summary>用于区分重载方法的参数类型列表</summary>
    public Type[]? ParameterTypes { get; }

    /// <param name="methodName">要 Patch 的方法名</param>
    public TracePatchAttribute(string methodName)
    {
        MethodName = methodName;
    }

    /// <param name="methodName">要 Patch 的方法名</param>
    /// <param name="parameterTypes">参数类型列表，用于区分重载方法</param>
    public TracePatchAttribute(string methodName, Type[] parameterTypes)
    {
        MethodName = methodName;
        ParameterTypes = parameterTypes;
    }
}
