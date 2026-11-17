using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;
using MetaMystia.Patch;

namespace MetaMystia;

/// <summary>
/// 普通营业的面板时停：各端上报本地是否被面板占住，房主在夜间场景内全员都请求时停时冻结全房间，任一人恢复即解除。
/// 原版 timeScale 由各端同时执行，计时、耐心与刷客保持一致。
/// </summary>
public static class RoomTimeStop
{
    private sealed class State : IScopeEnd
    {
        public readonly RoomBallot<bool> Demands = new();
        public bool LocalDemand;
        public bool Frozen;

        public void OnScopeEnd(bool resume)
        {
            if (Frozen) GameTimeManagerPatch.Apply(false);
        }
    }

    private static State Current => Scopes.Scene.Get<State>();

    private static bool IsBusiness => GameSession.HasRoomPeers && GameFlow.LocalScene == Scene.WorkScene
        && GameFlow.Destination == DayDestination.Business;

    public static bool Intercepts => IsBusiness && !GameFlow.InStory;

    public static void SetLocalDemand(bool freeze) => Current.LocalDemand = freeze;

    public static void ReceiveDemand(int uid, bool freeze)
    {
        if (GameSession.IsRoomHost && IsBusiness) Current.Demands.Receive(uid, freeze);
    }

    /// <summary>每帧合并意向：同帧内的恢复再冻结（选菜转 QTE）不发包。剧情中的玩家不算被面板占住。</summary>
    public static void Tick()
    {
        if (!IsBusiness) return;
        var state = Current;
        state.Demands.Submit(state.LocalDemand && !GameFlow.InStory, TimeStopRequestMessage.Send);
        if (GameSession.IsRoomHost)
        {
            bool freeze = state.Demands.All((_, demand) => demand,
                member => member.Uid != PlayerManager.Local.Uid && member.Stage != GameStage.Work);
            // 先广播再执行：房主冻结后自身收尾无法推进，客机必须先于房主恢复。
            if (freeze != state.Frozen)
            {
                TimeStopStateMessage.Send(freeze);
                Apply(freeze);
            }
        }
        // 剧情期间原版可直接改动本地 timeScale，结束后按房间状态校正。
        if (!GameFlow.InStory && GameTimeManagerPatch.IsFrozen != state.Frozen) GameTimeManagerPatch.Apply(state.Frozen);
    }

    public static void Apply(bool freeze)
    {
        if (!IsBusiness) return;
        Current.Frozen = freeze;
        if (!GameFlow.InStory) GameTimeManagerPatch.Apply(freeze);
    }
}
