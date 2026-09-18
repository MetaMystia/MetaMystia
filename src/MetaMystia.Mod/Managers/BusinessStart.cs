using System.Collections.Generic;
using System.Linq;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Actions;
using MetaMystia.Network;

namespace MetaMystia;

/// <summary>普通营业初始化完成后，等待全员就绪再启动原刷客与计时。</summary>
public static class BusinessStart
{
    private static readonly HashSet<int> ready = new();
    private static System.Action continuation;
    private static bool started;

    public static bool IsWaitingForStart => GameSession.IsInRoom
        && GameFlow.Destination == DayDestination.Business && !started;

    public static void Reset(bool resume = false)
    {
        var pending = continuation;
        continuation = null;
        ready.Clear();
        started = false;
        if (resume) pending?.Invoke();
    }

    public static void Wait(System.Action callback)
    {
        if (!GameSession.IsInRoom || GameFlow.Destination != DayDestination.Business)
        {
            callback?.Invoke();
            return;
        }
        continuation = callback;
        if (GameSession.IsRoomHost) ReceiveReady(PlayerManager.Local.Uid);
        else BusinessStartAction.Send(false);
    }

    public static void ReceiveReady(int uid)
    {
        if (GameSession.IsRoomHost && GameFlow.Destination == DayDestination.Business
            && GameSession.Room.Members.Any(p => p.Uid == uid)) ready.Add(uid);
    }

    public static void TryStart()
    {
        if (!GameSession.IsRoomHost || started || continuation == null || GameFlow.Stage != GameStage.Work
            || !GameSession.Room.Members.All(p => ready.Contains(p.Uid)
                && (p.Uid == PlayerManager.Local.Uid || p.Stage == GameStage.Work))) return;
        BusinessStartAction.Send(true);
        ApplyStart();
    }

    public static void ApplyStart()
    {
        if (started || continuation == null || GameFlow.Destination != DayDestination.Business) return;
        started = true;
        var callback = continuation;
        continuation = null;
        callback.Invoke();
    }
}
