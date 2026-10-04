using System.Collections.Generic;
using System.Linq;

using Mystia.Scenes;

namespace MetaMystia;

/// <summary>
/// 顾客注册表：运行时流水号（<see cref="GuestFSM.RuntimeId"/>，联机消息的身份）与框架句柄
/// （<see cref="GuestHandle"/>，一次营业会话内的顾客身份）双向查找。
/// 取代原来按控制器指针扫描的写法：控制器在模组侧已经不可见，句柄才是框架给的身份。
/// </summary>
[AutoLog]
public static partial class GuestsMap
{
    private const int InvalidRuntimeId = 0;
    private static int _nextRuntimeId = 1;
    private static readonly Dictionary<int, GuestFSM> ByRuntimeId = [];
    private static readonly Dictionary<GuestHandle, GuestFSM> ByHandle = [];

    private static int AllocateRuntimeId() => _nextRuntimeId++;

    public static void StoreGuest(int runtimeId, GuestFSM fsm)
    {
        if (runtimeId == InvalidRuntimeId)
        {
            Log.Error("Attempted to store guest with invalid RuntimeId 0");
            return;
        }

        if (ByRuntimeId.TryGetValue(runtimeId, out var existing) && !ReferenceEquals(existing, fsm))
        {
            Log.Error($"RuntimeId conflict: overwriting guest #{runtimeId}, old FSM state: {existing.CurrentState}, new FSM state: {fsm.CurrentState}");
        }

        fsm.RuntimeId = runtimeId;
        ByRuntimeId[runtimeId] = fsm;
        if (runtimeId >= _nextRuntimeId)
        {
            _nextRuntimeId = runtimeId + 1;
        }

        Bind(fsm);
    }

    public static int StoreGuest(GuestFSM fsm)
    {
        var runtimeId = AllocateRuntimeId();
        StoreGuest(runtimeId, fsm);
        Log.Warning(fsm.Handle.IsNone
            ? $"Guest stored: #{runtimeId} <- no handle yet"
            : $"Guest stored: #{runtimeId} <- {fsm.Handle}");
        return runtimeId;
    }

    /// <summary>
    /// 顾客拿到框架句柄后登记。主机的句柄随生成事件一起来，客机的句柄要等重放生成、框架铸造出组之后才有，
    /// 所以这一步与 <see cref="StoreGuest(int, GuestFSM)"/> 分开。
    /// </summary>
    public static void Bind(GuestFSM fsm)
    {
        if (fsm == null || fsm.Handle.IsNone) return;
        ByHandle[fsm.Handle] = fsm;
    }

    public static int GetRuntimeId(GuestHandle handle)
    {
        if (TryGet(handle, out var fsm)) return fsm.RuntimeId;

        Log.Error(handle.IsNone
            ? "Attempted to get RuntimeId of a guest that has no handle"
            : $"Attempted to get RuntimeId of a guest that is not stored: {handle}");
        return InvalidRuntimeId;
    }

    public static GuestFSM GetGuestFsm(int runtimeId)
    {
        if (!ByRuntimeId.TryGetValue(runtimeId, out var fsm))
        {
            Log.Error($"Attempted to get a guest that is not stored: #{runtimeId}");
            return null;
        }

        return fsm;
    }

    public static GuestFSM GetGuestFsm(GuestHandle handle)
    {
        if (TryGet(handle, out var fsm)) return fsm;

        Log.Error(handle.IsNone
            ? "Attempted to get FSM of a guest that has no handle"
            : $"Attempted to get FSM of a guest that is not stored: {handle}");
        return null;
    }

    /// <summary>句柄对应的 FSM，句柄为空或未登记时为 false（不记日志，供内部查询用）。</summary>
    public static bool TryGet(GuestHandle handle, out GuestFSM fsm)
    {
        fsm = null;
        return !handle.IsNone && ByHandle.TryGetValue(handle, out fsm);
    }

    /// <summary>
    /// 桌上那一组的 FSM。游戏保证一张桌只有一个组，这里按句柄投影的桌号扫描：投影解析不依赖场景服务
    /// 作用域，因此监听器回调与收包线程都能用（它们不在作用域内，不能调 <c>IWorkSceneGuests</c>）。
    /// </summary>
    public static GuestFSM GetGuestFsmAtDesk(int deskCode)
    {
        if (deskCode < 0) return null;

        foreach (var fsm in ByRuntimeId.Values)
        {
            if (fsm.Proxy is { HasLeft: false } guest && guest.DeskCode == deskCode) return fsm;
        }

        return null;
    }

    /// <summary>
    /// 从注册表移除某个 RuntimeId 对应的 FSM。
    /// 用于 FallBack / GuestKill 等终态清理后释放槽位，防止后续 hook 命中僵尸 FSM 导致二次 FallBack。
    /// </summary>
    public static void Remove(int runtimeId)
    {
        if (!ByRuntimeId.Remove(runtimeId, out var fsm))
        {
            return;
        }

        if (!fsm.Handle.IsNone && ByHandle.TryGetValue(fsm.Handle, out var bound) && ReferenceEquals(bound, fsm))
        {
            ByHandle.Remove(fsm.Handle);
        }

        Log.Warning($"Guest #{runtimeId} removed from GuestsMap");
    }

    /// <summary>
    /// 兜底：在没有任何 To/Enqueue 触发的帧上仍能让超时项过期。
    /// 由 PluginHost.Update 每帧调用。
    /// </summary>
    public static void TickAllPending()
    {
        if (ByRuntimeId.Count == 0) return;
        // 快照：FallBack→Remove 会在 Drain 内部修改 ByRuntimeId，避免迭代中变更。
        foreach (var fsm in ByRuntimeId.Values.ToList()) fsm.TickPending();
    }
}
