using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Common.UI;

using MetaMystia.Network.Core;
using MetaMystia.Patch;
using MetaMystia.Protocol;
using MetaMystia.UI;

namespace MetaMystia.Network;

// 一次房间绑定的一次玩法阶段。准备、参与者与延迟效果只有这里能够推进。
[AutoLog]
public static partial class RoomGameplay
{
    private static RoomBinding _binding;
    private static GameplaySnapshotAction _state = new();
    private static readonly HashSet<int> ReadyIntents = new();
    private static readonly List<Coroutine> Routines = new();
    private static readonly Queue<(Action Action, long Deadline)> Effects = new();
    private static GameplayPhase _localReadyIntent;
    private static bool _readySent;
    private static long _syncDeadline;
    private static bool _leaving;
    private static bool _reopenAdmission;
    private static ClientSession Session => MpWire.Session;
    public static long PhaseId => _state.Epoch;
    public static GameplayPhase Phase => _state.Phase;
    public static bool IsReady => Session.CanPlay && !_leaving && Phase != GameplayPhase.None && ModPlayerStore.AllResourcesReceived;
    public static bool HasParticipants => Session.IsInRoom && Session.Room.Members.Count > 1;
    public static bool LocalDayReady => GameContext.EndingDay || _localReadyIntent == GameplayPhase.Day || IsPlayerReady(Session.SelfUid, GameplayPhase.Day);
    public static bool LocalPrepReady => _localReadyIntent == GameplayPhase.Prep || IsPlayerReady(Session.SelfUid, GameplayPhase.Prep);

    public static bool IsPlayerReady(int uid, GameplayPhase phase) => Phase == phase && _state.Ready.Contains(uid);
    public static PlayerSelection Selection(int uid) => _state.Selections.FirstOrDefault(p => p.Uid == uid);
    public static bool Matches(RoomBinding binding, long phase) => !_leaving && Session.CanPlay && binding == _binding && phase == PhaseId;

    public static void OnSessionChanged()
    {
        if (_binding != Session.Binding)
        {
            bool roomEnded = _binding.RoomId != Guid.Empty && Session.Binding == default && Session.IsOnline;
            CancelPhase();
            _binding = Session.Binding;
            if (roomEnded) InGameConsole.ShowPassive(MpWire.IsLanSession ? TextId.MpDisconnected.Get() : TextId.MpRoomLeft.Get());
            _state = new();
            ReadyIntents.Clear();
            _localReadyIntent = GameplayPhase.None;
            _readySent = false;
            _leaving = false;
            _reopenAdmission = false;
            PrepSceneManager.ClearPrepTable();
            GuestsMap.ClearSession();
            if (!Session.IsInRoom) return;
            _syncDeadline = MpWire.NowMs + 15_000;
            ResourceSnapshotAction.Send();
            if (Session.IsRoomHost) PublishNewPhase(GameplayPhase.Day);
            return;
        }
        if (!Session.IsRoomHost || Phase == GameplayPhase.None) return;
        var members = Session.Room.Members.Keys.ToHashSet();
        var participants = _state.Locked ? _state.Participants.Where(members.Contains).ToArray() : members.ToArray();
        if (!_state.Participants.OrderBy(x => x).SequenceEqual(participants.OrderBy(x => x)))
        {
            Publish(_state.Phase, _state.Locked, participants, _state.Ready.Where(members.Contains).ToArray(),
                _state.Selections.Where(s => members.Contains(s.Uid)).ToArray(), _state.PrepTable);
        }
        TryLockDay();
    }

    public static void OnRequestCompleted(RoomOperation? operation, string error)
    {
        if (operation == RoomOperation.SetAdmission && error.Length > 0) Fail($"无法确认入房开关：{error}");
        if (operation == RoomOperation.SetAdmission) TryLockDay();
    }

    public static void Tick()
    {
        if (!Session.IsInRoom) return;
        if (!Session.CanPlay) { CancelPhase(); return; }
        if (_leaving)
        {
            if (Session.Pending == null) MpWire.Request(RoomOperation.Leave);
            return;
        }
        if (!IsReady && MpWire.NowMs >= _syncDeadline) { Fail("房间初始数据同步超时，退出房间并保留公共连接"); return; }
        if (IsReady) _syncDeadline = MpWire.NowMs + 15_000;
        if (_reopenAdmission && Session.IsRoomHost && Session.Pending == null)
        {
            if (Session.Room.AdmissionOpen) _reopenAdmission = false;
            else MpWire.Request(RoomOperation.SetAdmission, admissionOpen: true);
        }
        if (_localReadyIntent == Phase && IsReady && !_readySent)
        {
            if (Phase == GameplayPhase.Day) DayReadyAction.Send();
            else if (Phase == GameplayPhase.Prep) PrepReadyAction.Send();
            _readySent = true;
        }
        if (Session.IsRoomHost && ReadyIntents.Count > 0 && Phase == GameplayPhase.Day && !_state.Locked && Session.Pending == null)
            MpWire.Request(RoomOperation.SetAdmission, admissionOpen: false);
        TryLockDay();
        if (Session.IsRoomHost) TryAdvance();
        while (Effects.TryPeek(out var pending))
        {
            var action = pending.Action;
            if (!Matches(action.Binding, action.PhaseId)) { Effects.Dequeue(); continue; }
            if (MpWire.NowMs >= pending.Deadline) { Fail($"等待游戏就绪超时：{action.GetType().Name}"); break; }
            if (!EffectReady(action)) break;
            Effects.Dequeue();
            action.OnReceived();
        }
    }

    public static void SubmitReady(GameplayPhase phase)
    {
        if (!Session.CanPlay || Phase != phase && Phase != GameplayPhase.None) return;
        _localReadyIntent = phase;
        _readySent = false;
        InGameConsole.ShowPassive(TextId.MystiaReadyForWork.Get());
    }

    public static void ReceiveReady(int uid, GameplayPhase phase)
    {
        if (!Session.IsRoomHost || Phase != phase || !_state.Participants.Contains(uid)) return;
        if (phase == GameplayPhase.Day && !_state.Locked)
        {
            ReadyIntents.Add(uid);
            return;
        }
        if (_state.Ready.Contains(uid)) return;
        Publish(Phase, _state.Locked, _state.Participants, _state.Ready.Append(uid).ToArray(), _state.Selections, _state.PrepTable);
        TryAdvance();
    }

    public static void ReceiveSelection(int uid, MapLabel map, int level)
    {
        if (!Session.IsRoomHost || Phase != GameplayPhase.Selection || !_state.Participants.Contains(uid)
            || !map.IsSelected() || level is < 1 or > 3) return;
        var selections = _state.Selections.Where(s => s.Uid != uid).Append(new(uid, map, level)).ToArray();
        Publish(Phase, true, _state.Participants, _state.Ready, selections, null);
        TryAdvance();
    }

    private static void TryLockDay()
    {
        if (!Session.IsRoomHost || Phase != GameplayPhase.Day || _state.Locked || Session.Room.AdmissionOpen || ReadyIntents.Count == 0) return;
        var participants = Session.Room.Members.Keys.ToArray();
        Publish(Phase, true, participants, ReadyIntents.Where(participants.Contains).ToArray(), Array.Empty<PlayerSelection>(), null);
        ReadyIntents.Clear();
        TryAdvance();
    }

    private static void TryAdvance()
    {
        if (!IsReady || !_state.Locked) return;
        if (Phase == GameplayPhase.Selection)
        {
            var selected = _state.Selections.FirstOrDefault();
            if (selected != null && _state.Participants.All(uid => _state.Selections.Any(s => s.Uid == uid && s.Map == selected.Map && s.Level == selected.Level)))
                PublishNewPhase(GameplayPhase.Prep, _state.Selections);
            return;
        }
        if (!_state.Participants.All(_state.Ready.Contains)) return;
        if (Phase == GameplayPhase.Day) PublishNewPhase(GameplayPhase.Selection);
        else if (Phase == GameplayPhase.Prep) PublishNewPhase(GameplayPhase.Night, _state.Selections, PrepSceneManager.GetLocalPrepTableSnapshot());
    }

    public static bool ForceContinue(GameplayPhase phase)
    {
        if (!Session.IsRoomHost || Phase != phase || !IsReady) return false;
        if (phase == GameplayPhase.Day && !_state.Locked)
        {
            ReadyIntents.UnionWith(Session.Room.Members.Keys);
            return true;
        }
        Publish(Phase, true, _state.Participants, _state.Participants.ToArray(), _state.Selections, _state.PrepTable);
        TryAdvance();
        return true;
    }

    public static void OnLocalSceneChanged(Scene scene)
    {
        if (scene == Scene.DayScene && Session.IsRoomHost && Phase == GameplayPhase.Night)
        {
            PublishNewPhase(GameplayPhase.Day);
            _reopenAdmission = true;
        }
    }

    private static void PublishNewPhase(GameplayPhase phase, PlayerSelection[] selections = null, UpdatePrepAction.Table table = null) =>
        Publish(phase, phase != GameplayPhase.Day, Session.Room.Members.Keys.ToArray(), Array.Empty<int>(), selections ?? Array.Empty<PlayerSelection>(), table);

    private static void Publish(GameplayPhase phase, bool locked, int[] participants, int[] ready, PlayerSelection[] selections, UpdatePrepAction.Table table)
    {
        var snapshot = new GameplaySnapshotAction
        {
            Epoch = phase == Phase ? PhaseId : PhaseId + 1, Revision = _state.Revision + 1,
            Phase = phase, Locked = locked, Participants = participants.ToArray(), Ready = ready.ToArray(),
            Selections = selections.ToArray(), PrepTable = table?.Clone()
        };
        snapshot.Publish();
        ApplySnapshot(snapshot);
    }

    public static void ApplySnapshot(GameplaySnapshotAction snapshot)
    {
        if (_leaving || !Session.CanPlay || snapshot.Epoch < PhaseId || snapshot.Revision <= _state.Revision
            || !Enum.IsDefined(typeof(GameplayPhase), snapshot.Phase) || snapshot.Phase == GameplayPhase.None
            || snapshot.Participants == null || snapshot.Ready == null || snapshot.Selections == null
            || snapshot.Participants.Length == 0 || snapshot.Participants.Distinct().Count() != snapshot.Participants.Length
            || snapshot.Participants.Any(uid => !Session.Room.Members.ContainsKey(uid))
            || !snapshot.Participants.Contains(Session.SelfUid) || !snapshot.Participants.Contains(Session.HostUid)
            || snapshot.Ready.Any(uid => !snapshot.Participants.Contains(uid))
            || snapshot.Selections.Select(s => s.Uid).Distinct().Count() != snapshot.Selections.Length
            || snapshot.Selections.Any(s => !snapshot.Participants.Contains(s.Uid) || !s.Map.IsSelected() || s.Level is < 1 or > 3)
            || snapshot.Phase is GameplayPhase.Prep or GameplayPhase.Night && snapshot.Selections.Length != snapshot.Participants.Length
            || snapshot.Phase == GameplayPhase.Night && snapshot.PrepTable == null) return;
        var previous = Phase;
        bool changed = snapshot.Epoch != PhaseId;
        if (changed)
        {
            CancelPhase();
            if (previous != GameplayPhase.None) _localReadyIntent = GameplayPhase.None;
            _readySent = false;
            ReadyIntents.Clear();
            ModPlayerStore.ClearNightMotion();
            GuestsMap.ClearSession();
        }
        _state = snapshot;
        if (!changed || previous == GameplayPhase.None) return;
        Log.Message($"Gameplay phase {previous} -> {Phase}; binding={_binding}; epoch={PhaseId}");
        Run(ApplyTransition(previous, snapshot));
    }

    private static IEnumerator ApplyTransition(GameplayPhase previous, GameplaySnapshotAction snapshot)
    {
        long deadline = MpWire.NowMs + 30_000;
        if (previous == GameplayPhase.Day && snapshot.Phase == GameplayPhase.Selection)
        {
            while (MpManager.LocalScene != Scene.DayScene || DayScene.SceneManager.Instance == null)
            { if (MpWire.NowMs >= deadline) { Fail("白天场景尚未就绪"); yield break; } yield return null; }
            DaySceneManagerPatch.OnDayOver();
        }
        else if (previous == GameplayPhase.Selection && snapshot.Phase == GameplayPhase.Prep)
        {
            while (IzakayaSelectorPanelPatch.instanceRef == null)
            { if (MpWire.NowMs >= deadline) { Fail("选店面板尚未就绪"); yield break; } yield return null; }
            var selected = snapshot.Selections.First(s => s.Uid == Session.SelfUid);
            IzakayaSelectorPanelPatch.TryProceedWithConfirmedSelection(selected.Map, (IzakayaLevel)selected.Level);
        }
        else if (previous == GameplayPhase.Prep && snapshot.Phase == GameplayPhase.Night)
        {
            while (MpManager.LocalScene != Scene.IzakayaPrepScene || IzakayaConfigPannelPatch.instanceRef == null)
            { if (MpWire.NowMs >= deadline) { Fail("备菜面板尚未就绪"); yield break; } yield return null; }
            PrepSceneManager.ApplyHostTable(snapshot.PrepTable);
            IzakayaConfigPannelPatch.PrepOver();
        }
    }

    public static void ReceiveEffect(Action action)
    {
        if (!Matches(action.Binding, action.PhaseId)) return;
        if (Effects.Count == 0 && EffectReady(action)) { action.OnReceived(); return; }
        if (Effects.Count >= 256) { Fail("等待游戏就绪的消息过多"); return; }
        Effects.Enqueue((action, MpWire.NowMs + 30_000));
    }

    private static bool EffectReady(Action action) => IsReady && (!action.ReceiveScene.HasValue || action.ReceiveScene == MpManager.LocalScene)
        && (!action.WaitForStory || !MpManager.InStory);

    // 只登记房间效果的协程句柄，绑定或阶段失效时统一停止，包括嵌套等待。
    public static void Run(IEnumerator routine)
    {
        if (Session.CanPlay && PluginHost.Instance != null)
            Routines.Add(PluginHost.Instance.StartManagedCoroutine(RunBound(routine, _binding, PhaseId)));
    }

    private static IEnumerator RunBound(IEnumerator routine, RoomBinding binding, long phase)
    {
        while (Matches(binding, phase) && routine.MoveNext()) yield return routine.Current;
    }

    public static void Abort(string reason) => Fail(reason);

    public static bool Leave()
    {
        if (!Session.IsInRoom) return false;
        _leaving = true;
        CancelPhase();
        if (Session.Pending == null) MpWire.Request(RoomOperation.Leave);
        return true;
    }

    private static void CancelPhase()
    {
        if (PluginHost.Instance != null)
            foreach (var routine in Routines) if (routine != null) PluginHost.Instance.StopCoroutine(routine);
        Routines.Clear();
        Effects.Clear();
    }

    private static void Fail(string reason)
    {
        if (_leaving) return;
        _leaving = true;
        CancelPhase();
        Log.Error($"{reason}; binding={_binding}; phase={PhaseId}");
        InGameConsole.ShowPassive(TextId.MpRoomSyncInterrupted.Get());
    }
}
