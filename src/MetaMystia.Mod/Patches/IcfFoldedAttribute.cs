using System;

namespace MetaMystia.Patch;

/// <summary>
/// Hook 目标与其他方法折叠（ICF），已按 docs/rules/harmony-hook.md 核对处理；tools/icf 检查据此放行。
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IcfFoldedAttribute(string reason) : Attribute
{
    public string Reason { get; } = reason;
}
