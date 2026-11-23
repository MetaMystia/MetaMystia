using System;
using System.Collections.Generic;

namespace MetaMystia.Multiplayer;

/// <summary>作用域结束时收尾。resume 为 true 时，把等待中的流程交还原版继续执行。</summary>
public interface IScopeEnd
{
    void OnScopeEnd(bool resume);
}

/// <summary>一段联机状态的生命周期。结束后整体替换；异步回调保存作用域，以 <see cref="Ended"/> 判断是否过期。</summary>
public sealed class Scope
{
    private readonly Dictionary<Type, object> states = new();
    private readonly List<object> order = new();

    public bool Ended { get; private set; }

    public T Get<T>() where T : class, new()
    {
        if (states.TryGetValue(typeof(T), out var state)) return (T)state;
        var created = new T();
        states.Add(typeof(T), created);
        order.Add(created);
        return created;
    }

    internal void End(bool resume)
    {
        Ended = true;
        for (int i = 0; i < order.Count; i++)
            if (order[i] is IScopeEnd end) end.OnScopeEnd(resume);
    }
}

/// <summary>
/// Coop 为一次共同推进，Scene 为当前场景内的联机状态。结束 Coop 时先结束 Scene。
/// 新作用域先就位再通知旧状态收尾，收尾回调访问到的已是新状态。
/// </summary>
public static class Scopes
{
    public static Scope Coop { get; private set; } = new();
    public static Scope Scene { get; private set; } = new();

    public static void EndScene(bool resume = false)
    {
        var ended = Scene;
        Scene = new();
        ended.End(resume);
    }

    public static void EndCoop(bool resume = false)
    {
        EndScene(resume);
        var ended = Coop;
        Coop = new();
        ended.End(resume);
    }
}
