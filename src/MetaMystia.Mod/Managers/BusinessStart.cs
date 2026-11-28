using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;

namespace MetaMystia;

/// <summary>普通营业与料理对决初始化完成后，等待全员就绪再启动原刷客与计时。</summary>
public static class BusinessStart
{
    private sealed class State : IScopeEnd
    {
        public readonly RoomBallot<bool> Ready = new();
        public System.Action Continuation;
        public bool Started;

        // 退出联机时交还原版开场，单机继续营业。
        public void OnScopeEnd(bool resume)
        {
            var pending = Continuation;
            Continuation = null;
            if (resume) pending?.Invoke();
        }
    }

    private static State Current => Scopes.Coop.Get<State>();

    public static bool IsWaitingForStart => GameSession.IsInRoom
        && GameFlow.Destination is (DayDestination.Business or DayDestination.Versus) && !Current.Started;

    public static void Wait(System.Action callback)
    {
        if (!GameSession.IsInRoom || GameFlow.Destination is not (DayDestination.Business or DayDestination.Versus))
        {
            callback?.Invoke();
            return;
        }
        Current.Continuation = callback;
        Current.Ready.Submit(true, _ => BusinessStartMessage.Send(false));
    }

    public static void ReceiveReady(int uid)
    {
        if (GameSession.IsRoomHost && GameFlow.Destination is (DayDestination.Business or DayDestination.Versus)) Current.Ready.Receive(uid, true);
    }

    public static void TryStart()
    {
        var state = Current;
        if (!GameSession.IsRoomHost || state.Started || state.Continuation == null || GameFlow.Stage != GameStage.Work
            || !state.Ready.All((member, ready) => ready
                && (member.Uid == PlayerManager.Local.Uid || member.Stage == GameStage.Work))) return;
        BusinessStartMessage.Send(true);
        ApplyStart();
    }

    public static void ApplyStart()
    {
        var state = Current;
        if (state.Started || state.Continuation == null || GameFlow.Destination is not (DayDestination.Business or DayDestination.Versus)) return;
        state.Started = true;
        var callback = state.Continuation;
        state.Continuation = null;
        callback.Invoke();
    }
}
