using System;
using System.Collections.Generic;

using Il2CppInterop.Runtime.InteropTypes;

using MetaMystia.Multiplayer;

namespace MetaMystia.Patch;

/// <summary>
/// 重放原版调用时放行指定对象的钩子。按底层对象地址匹配，不影响其他对象。
/// 同步调用用 <see cref="Enter(IntPtr)"/>；不能确认同步到达的钩子用 <see cref="Grant(IntPtr)"/>，换场景时作废。
/// </summary>
public static class Bypass
{
    private sealed class Grants
    {
        public readonly Dictionary<IntPtr, int> Counts = new();
    }

    private static readonly Dictionary<IntPtr, int> entered = new();

    public static Entry Enter(IntPtr key)
    {
        entered[key] = entered.GetValueOrDefault(key) + 1;
        return new(key);
    }

    public static Entry Enter(Il2CppObjectBase target) => Enter(target.Pointer);

    public static void Grant(IntPtr key)
    {
        var counts = Scopes.Scene.Get<Grants>().Counts;
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    public static void Grant(Il2CppObjectBase target) => Grant(target.Pointer);

    /// <summary>钩子据此放行；一次性许可在此消耗。</summary>
    public static bool Active(IntPtr key)
    {
        if (entered.ContainsKey(key)) return true;
        var counts = Scopes.Scene.Get<Grants>().Counts;
        if (!counts.TryGetValue(key, out int count)) return false;
        if (count == 1) counts.Remove(key);
        else counts[key] = count - 1;
        return true;
    }

    public static bool Active(Il2CppObjectBase target) => target != null && Active(target.Pointer);

    public readonly struct Entry : IDisposable
    {
        private readonly IntPtr key;

        internal Entry(IntPtr key) => this.key = key;

        public void Dispose()
        {
            if (--entered[key] == 0) entered.Remove(key);
        }
    }
}
