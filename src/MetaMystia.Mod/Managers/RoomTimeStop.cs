using System.Collections.Generic;
using System.Linq;

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
    private static readonly HashSet<int> demands = new();
    private static bool localDemand;
    private static bool sentDemand;
    private static bool frozen;

    private static bool IsBusiness => GameSession.HasRoomPeers && GameFlow.LocalScene == Scene.WorkScene
        && GameFlow.Destination == DayDestination.Business;

    public static bool Intercepts => IsBusiness && !GameFlow.InStory;

    public static void SetLocalDemand(bool freeze) => localDemand = freeze;

    public static void ReceiveDemand(int uid, bool freeze)
    {
        if (!GameSession.IsRoomHost || !IsBusiness) return;
        if (freeze) demands.Add(uid);
        else demands.Remove(uid);
    }

    /// <summary>每帧合并意向：同帧内的恢复再冻结（选菜转 QTE）不发包。剧情中的玩家不算被面板占住。</summary>
    public static void Tick()
    {
        if (!IsBusiness) return;
        bool demand = localDemand && !GameFlow.InStory;
        if (GameSession.IsRoomHost)
        {
            ReceiveDemand(PlayerManager.Local.Uid, demand);
            bool freeze = GameSession.Room.Members.All(p => demands.Contains(p.Uid)
                || p.Uid != PlayerManager.Local.Uid && p.Stage != GameStage.Work);
            // 先广播再执行：房主冻结后自身收尾无法推进，客机必须先于房主恢复。
            if (freeze != frozen)
            {
                TimeStopStateMessage.Send(freeze);
                Apply(freeze);
            }
        }
        else if (demand != sentDemand)
        {
            sentDemand = demand;
            TimeStopRequestMessage.Send(demand);
        }
        // 剧情期间原版可直接改动本地 timeScale，结束后按房间状态校正。
        if (!GameFlow.InStory && GameTimeManagerPatch.IsFrozen != frozen) GameTimeManagerPatch.Apply(frozen);
    }

    public static void Apply(bool freeze)
    {
        if (!IsBusiness) return;
        frozen = freeze;
        if (!GameFlow.InStory) GameTimeManagerPatch.Apply(freeze);
    }

    public static void Reset()
    {
        if (frozen) GameTimeManagerPatch.Apply(false);
        demands.Clear();
        localDemand = sentDemand = frozen = false;
    }
}
