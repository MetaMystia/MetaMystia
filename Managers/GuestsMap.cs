using System.Collections.Generic;
using System.Linq;

using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using UnityEngine;

namespace MetaMystia;

[AutoLog]
public static partial class GuestsMap
{
    private const int InvalidRuntimeId = 0;
    private static int _nextRuntimeId = 1;
    private static Dictionary<int, GuestFSM> _allGuests = new();

    private static int AllocateRuntimeId() => _nextRuntimeId++;
    private static bool HasGuest(int runtimeId) => _allGuests.ContainsKey(runtimeId);
    private static bool HasGuest(GuestGroupController controller) => controller != null && _allGuests.Values.Any(g => g.Controller != null && g.Controller.Pointer == controller.Pointer);


    public static void StoreGuest(int runtimeId, GuestFSM fsm)
    {
        if (runtimeId == InvalidRuntimeId)
        {
            Log.Error("Attempted to store guest with invalid RuntimeId 0");
            return;
        }

        if (_allGuests.TryGetValue(runtimeId, out var existing) && !ReferenceEquals(existing, fsm))
        {
            Log.Error($"RuntimeId conflict: overwriting guest #{runtimeId}, old FSM state: {existing.CurrentState}, new FSM state: {fsm.CurrentState}");
        }

        _allGuests[runtimeId] = fsm;
        if (runtimeId >= _nextRuntimeId)
        {
            _nextRuntimeId = runtimeId + 1;
        }
    }

    public static int StoreGuest(GuestFSM fsm)
    {
        var runtimeId = AllocateRuntimeId();
        StoreGuest(runtimeId, fsm);
        if (fsm.Controller != null)
        {
            Log.Warning($"Guest stored: #{runtimeId} <- 0x{fsm.Controller.Pointer:X16}");
        }
        else
        {
            Log.Warning($"Guest stored: #{runtimeId} <- null");
        }
        return runtimeId;
    }
    public static int GetRuntimeId(GuestGroupController controller)
    {
        if (!HasGuest(controller))
        {
            if (controller != null)
            {
                Log.Error($"Attempted to get RuntimeId of a guest that is not stored: 0x{controller.Pointer:X16}");
            }
            else
            {
                Log.Error("Attempted to get RuntimeId of a null guest");
            }
            return InvalidRuntimeId;
        }
        return _allGuests.First(kv => kv.Value.Controller != null && kv.Value.Controller.Pointer == controller.Pointer).Key;
    }
    public static GuestFSM GetGuestFsm(int runtimeId)
    {
        if (!HasGuest(runtimeId))
        {
            Log.Error($"Attempted to get a guest that is not stored: #{runtimeId}");
            return null;
        }
        return _allGuests[runtimeId];
    }

    public static GuestFSM GetGuestFsm(GuestGroupController controller)
    {
        if (!HasGuest(controller))
        {
            if (controller != null)
            {
                Log.Error($"Attempted to get FSM of a guest that is not stored: 0x{controller.Pointer:X16}");
            }
            else
            {
                Log.Error("Attempted to get FSM of a null guest");
            }
            return null;
        }
        return _allGuests.First(kv => kv.Value.Controller != null && kv.Value.Controller.Pointer == controller.Pointer).Value;
    }

    /// <summary>
    /// 从注册表移除某个 RuntimeId 对应的 FSM。
    /// 用于 FallBack / GuestKill 等终态清理后释放槽位，防止后续 hook 命中僵尸 FSM 导致二次 FallBack。
    /// </summary>
    public static void Remove(int runtimeId)
    {
        if (_allGuests.Remove(runtimeId))
        {
            Log.Warning($"Guest #{runtimeId} removed from GuestsMap");
        }
    }

    /// <summary>
    /// 兜底：在没有任何 To/Enqueue 触发的帧上仍能让超时项过期。
    /// 由 PluginManager.Update 每帧调用。
    /// </summary>
    public static void TickAllPending()
    {
        if (_allGuests.Count == 0) return;
        // 快照：FallBack→Remove 会在 Drain 内部修改 _allGuests，避免迭代中变更。
        foreach (var fsm in _allGuests.Values.ToList()) fsm.TickPending();
    }
}
