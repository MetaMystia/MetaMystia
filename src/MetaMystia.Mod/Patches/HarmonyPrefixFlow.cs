namespace MetaMystia.Patch;

/// <summary>
/// Harmony Prefix 返回值语义化常量，用于消除 <c>return true/false</c> 的语义歧义。
/// <para>在 Harmony Prefix 中：<c>return false</c> = 跳过原方法；<c>return true</c> = 继续执行原方法。</para>
/// </summary>
/// <example>
/// <code>
/// [HarmonyPrefix]
/// static bool MyPrefix() {
///     if (someCondition) return SkipOriginal;
///     return RunOriginal;
/// }
/// </code>
/// </example>
internal static class HarmonyPrefixFlow
{
    /// <summary>
    /// 跳过原方法，不再执行。等同于 Prefix <c>return false</c>。
    /// </summary>
    internal const bool SkipOriginal = false;

    /// <summary>
    /// 继续执行原方法。等同于 Prefix <c>return true</c>。
    /// </summary>
    internal const bool RunOriginal = true;
}
