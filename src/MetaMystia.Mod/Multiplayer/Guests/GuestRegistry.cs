using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Messages;

namespace MetaMystia.Multiplayer.Guests;

/// <summary>
/// 当前场景的联机顾客登记表，存于 <see cref="Scopes.Scene"/>，离开场景整体失效。
/// 客机收到的主机事件进入各顾客的待应用队列，只在消息接收和每帧 Tick 时处理，不在原版 Hook 内处理。
/// </summary>
[AutoLog]
public sealed partial class GuestRegistry : IScopeEnd
{
    /// <summary>本地动画或计时落后时的宽限（游戏时间）；期满后追上主机。</summary>
    private const float GraceSeconds = 1.5f;

    private readonly Dictionary<int, GuestRecord> byId = new();
    private readonly Dictionary<IntPtr, GuestRecord> byController = new();
    private readonly HashSet<int> queried = new();
    private int nextId = 1;
    /// <summary>主机：上次下发的连击、断连击次数、热情断连击次数、断连击顾客组数。</summary>
    internal readonly int[] SentCombo = [-1, -1, -1, -1];

    public static GuestRegistry Current => Scopes.Scene.Get<GuestRegistry>();

    /// <summary>
    /// Hook 开头统一取模式。未登记的对象（单机、灵梦保护、剧情中本端生成的顾客）按原版运行。
    /// </summary>
    public static GuestMode Mode(GuestGroupController controller)
    {
        if (controller == null || !GameSession.HasRoomPeers) return GuestMode.Vanilla;
        var guest = Current.Find(controller);
        if (guest == null) return GuestMode.Vanilla;
        if (guest.Track == GuestTrack.Scripted) return GuestMode.Scripted;
        return GameSession.IsRoomHost ? GuestMode.Host : GuestMode.Client;
    }

    /// <summary>联机且已登记时返回记录。</summary>
    public static GuestRecord Of(GuestGroupController controller) =>
        GameSession.HasRoomPeers ? Current.Find(controller) : null;

    public GuestRecord Find(int runtimeId) => byId.GetValueOrDefault(runtimeId);

    public GuestRecord Find(GuestGroupController controller) =>
        controller == null ? null : byController.GetValueOrDefault(controller.Pointer);

    public IEnumerable<GuestRecord> All => byId.Values;

    /// <summary>登记顾客。主机传 0 分配编号；客机使用主机编号。</summary>
    public GuestRecord Add(int runtimeId, GuestTrack track, GuestSpawnInfo spawnInfo, GuestGroupController controller = null)
    {
        var replica = runtimeId != 0;
        if (!replica) runtimeId = nextId++;
        else nextId = Math.Max(nextId, runtimeId + 1);
        if (byId.TryGetValue(runtimeId, out var existing))
        {
            Log.Error($"Guest #{runtimeId} registered twice ({existing.State}), replacing");
            Remove(existing);
        }
        var guest = new GuestRecord { RuntimeId = runtimeId, Track = track, SpawnInfo = spawnInfo, Replica = replica };
        byId[runtimeId] = guest;
        if (controller != null) Attach(guest, controller);
        return guest;
    }

    public void Attach(GuestRecord guest, GuestGroupController controller)
    {
        guest.Controller = controller;
        byController[controller.Pointer] = guest;
    }

    public void Remove(GuestRecord guest)
    {
        if (byId.TryGetValue(guest.RuntimeId, out var current) && current == guest) byId.Remove(guest.RuntimeId);
        if (guest.Controller != null && byController.TryGetValue(guest.Controller.Pointer, out current) && current == guest)
            byController.Remove(guest.Controller.Pointer);
        guest.Inbox.Clear();
    }

    /// <summary>
    /// 客机接收主机事件。离场越过该顾客尚未应用的事件；剧情期间只暂存，剧情结束后由 Tick 处理。
    /// </summary>
    public void Post(IGuestEvent message)
    {
        var guest = Find(message.RuntimeId);
        if (guest == null)
        {
            if (message is GuestLeaveMessage) return;
            Query(message.RuntimeId);
            return;
        }
        if (message is GuestLeaveMessage)
        {
            guest.Inbox.Clear();
            guest.Waited = -1;
        }
        guest.Inbox.Add(message);
        if (!GameFlow.InStory) Process(guest, false);
    }

    /// <summary>主机尚有、本端没有的顾客，向主机请求快照；每个编号只请求一次。</summary>
    public void Query(int runtimeId)
    {
        if (!GameSession.IsRoomClient || !queried.Add(runtimeId)) return;
        Log.Warning($"Guest #{runtimeId} missing locally, requesting snapshot");
        GuestQueryMessage.Send(runtimeId);
    }

    /// <summary>按序应用。事件先取出再应用，应用中抛出的事件不再重试。</summary>
    private static void Process(GuestRecord guest, bool force)
    {
        while (guest.Inbox.Count > 0)
        {
            var head = guest.Inbox[0];
            guest.Inbox.RemoveAt(0);
            if (!head.Apply(guest, force))
            {
                guest.Inbox.Insert(0, head);
                if (guest.Waited < 0) guest.Waited = 0;
                return;
            }
            guest.Waited = -1;
            force = false;
        }
    }

    /// <summary>每帧调用：主机合并下发数值；客机重试等待中的事件，宽限期满后追上。</summary>
    public void Tick()
    {
        if (!GameSession.HasRoomPeers || GameFlow.LocalScene != Common.UI.Scene.WorkScene) return;
        if (GameSession.IsRoomHost)
        {
            GuestStatsMessage.Flush(this);
            return;
        }
        if (GameFlow.InStory) return;
        foreach (var guest in byId.Values.ToArray())
        {
            if (guest.Inbox.Count == 0) continue;
            if (guest.Waited >= 0) guest.Waited += Time.deltaTime;
            Process(guest, guest.Waited >= GraceSeconds);
        }
    }

    /// <summary>客机：本端改动过的顾客数值还原为主机最近下发的值。</summary>
    public void RestoreStats()
    {
        foreach (var guest in byId.Values)
            if (guest.Stats != null && guest.Controller is { } controller && controller.HaveNotLeft())
                GuestStatsMessage.ApplyTo(controller, guest.Stats);
    }

    /// <summary>客机断线（resume）时把仍在场的复制顾客交还原版，避免停在等待主机事件的位置。主机的顾客本就按原版运行。</summary>
    public void OnScopeEnd(bool resume)
    {
        if (!resume) return;
        foreach (var guest in byId.Values)
        {
            guest.Inbox.Clear();
            var controller = guest.Controller;
            if (!guest.Replica || guest.Track != GuestTrack.Replicated || controller == null || !controller.HaveNotLeft()) continue;
            switch (guest.State)
            {
                case GuestPhase.Constructed:
                    controller.MoveToSpawn();
                    break;
                case GuestPhase.Seated:
                    GuestsManager.Instance.StartCoroutine(DelayFirstOrder(controller).WrapToIl2Cpp());
                    break;
                // 吃饭中由原版延迟回调继续；已吃完说明续单入口已被截住，在此补上。
                case GuestPhase.Eaten:
                    GuestsManager.Instance.MainOrderCycle(controller, 0f);
                    break;
                case GuestPhase.Leaving:
                    GuestsManager.Instance.LeaveFromDesk(controller, GuestGroupController.LeaveType.Move, null, true);
                    break;
            }
            Log.Info($"Guest #{guest.RuntimeId} handed back to vanilla at {guest.State}");
        }
    }

    /// <summary>原版到座后延迟首单；重放入座省略了这一步，交还或断线后补上。</summary>
    internal static IEnumerator DelayFirstOrder(GuestGroupController controller)
    {
        yield return new WaitForSeconds(10f / EventManager.Instance.CurrentOrderSpeedRate);
        if (controller.HaveNotLeft() && !GameSession.HasRoomPeers) GuestsManager.Instance.FirstOrder(controller);
    }
}
