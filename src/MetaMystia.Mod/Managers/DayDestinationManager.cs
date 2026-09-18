using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Common.UI;

using MetaMystia.Multiplayer;
using MetaMystia.Multiplayer.Messages;
using MetaMystia.Network;
using MetaMystia.Patch;
using MetaMystia.UI;

namespace MetaMystia;

public enum DayDestination
{
    None,
    Business,
    FinalTrial,
    FinalTrialAgain,
}

/// <summary>白天入口只提交意向；主机确定本轮目标后，各端继续原入口。</summary>
[AutoLog]
public static partial class DayDestinationManager
{
    public static int Round { get; private set; } = 1;
    private static readonly Dictionary<int, DayDestination> intents = new();
    private static readonly Dictionary<DayDestination, System.Action> continuations = new();
    private static DayDestination localIntent;
    private static bool committed;
    private static bool businessReleased;
    private static bool firstTrialPending;
    private static int generation;
    private static bool closing;
    private static bool admissionClosed;
    public static bool ReplayingChallenge { get; private set; }
    public static bool ReplayingBusiness { get; private set; }
    public static bool IsStoryLocked => firstTrialPending;
    public static bool IsEntering => committed || businessReleased;
    public static bool HasLocalIntent => localIntent != DayDestination.None || intents.Count != 0 || closing;

    public static DayDestination GetIntent(int uid) => intents.GetValueOrDefault(uid);
    public static Dictionary<int, DayDestination> Snapshot() => new(intents);

    public static string ReadyText(DayDestination destination) => destination switch
    {
        DayDestination.Business => TextId.DestinationBusiness.Get(),
        DayDestination.FinalTrial => TextId.DestinationFinalTrial.Get(),
        DayDestination.FinalTrialAgain => TextId.DestinationFinalTrialAgain.Get(),
        _ => "…",
    };

    public static void Reset()
    {
        generation++;
        intents.Clear();
        continuations.Clear();
        localIntent = DayDestination.None;
        committed = false;
        businessReleased = false;
        firstTrialPending = false;
        closing = false;
        admissionClosed = false;
        ReplayingChallenge = false;
        ReplayingBusiness = false;
    }

    public static void ResetSession()
    {
        Reset();
        RunTimeSchedulerPatch.ResetFirstTrialGuest();
        Round = 1;
    }

    public static void InitializeSession(int round, Dictionary<int, DayDestination> state)
    {
        Round = round;
        ApplyState(round, state);
        if (localIntent != DayDestination.None && GameSession.IsRoomClient)
            DayDestinationIntentMessage.Send(Round, localIntent);
    }

    public static void Submit(DayDestination destination, System.Action continuation)
    {
        if (!GameSession.IsInRoom || GameFlow.LocalScene != Scene.DayScene) return;
        // 白天结束剧情仍可能要求进入首次挑战，此时属于下一轮入口。
        if (committed && !(businessReleased && destination is DayDestination.FinalTrial or DayDestination.FinalTrialAgain)) return;
        if (businessReleased && destination == DayDestination.Business) return;
        if (IsStoryLocked && destination != DayDestination.FinalTrial) return;
        committed = false;
        continuations[destination] = continuation;
        if (localIntent == destination) return;
        localIntent = destination;
        firstTrialPending = destination == DayDestination.FinalTrial;
        if (GameSession.IsRoomHost)
            ReceiveIntent(PlayerManager.Local.Uid, Round, destination);
        else
            DayDestinationIntentMessage.Send(Round, destination);
    }

    public static void ReceiveIntent(int uid, int round, DayDestination destination)
    {
        if (!GameSession.IsRoomHost || round != Round
            || destination is < DayDestination.None or > DayDestination.FinalTrialAgain) return;
        if (uid != PlayerManager.Local.Uid && !PlayerManager.Peers.ContainsKey(uid)) return;
        if (destination == DayDestination.None)
        {
            intents.Remove(uid);
            DayDestinationStateMessage.Send(Round, Snapshot());
            return;
        }
        if (committed && businessReleased && destination is DayDestination.FinalTrial or DayDestination.FinalTrialAgain) committed = false;
        if (committed) return;
        if (businessReleased && destination == DayDestination.Business) return;
        if (GetIntent(uid) == DayDestination.FinalTrial && destination != DayDestination.FinalTrial) return;
        if (GetIntent(uid) == destination) return;
        intents[uid] = destination;
        Notify(uid, destination);
        DayDestinationStateMessage.Send(Round, Snapshot());
        TryConfirm();
    }

    public static void ApplyState(int round, Dictionary<int, DayDestination> state)
    {
        if (round == Round && businessReleased && state.Values.Any(value => value is DayDestination.FinalTrial or DayDestination.FinalTrialAgain)) committed = false;
        if (round != Round || committed) return;
        foreach (var pair in state)
            if (GetIntent(pair.Key) != pair.Value) Notify(pair.Key, pair.Value);
        intents.Clear();
        foreach (var pair in state) intents[pair.Key] = pair.Value;
    }

    private static void Notify(int uid, DayDestination destination)
    {
        var text = destination switch
        {
            DayDestination.Business => TextId.DestinationBusinessReady,
            DayDestination.FinalTrial => TextId.DestinationFinalTrialReady,
            DayDestination.FinalTrialAgain => TextId.DestinationFinalTrialAgainReady,
            _ => (TextId?)null,
        };
        if (text.HasValue) InGameConsole.ShowPassive(text.Value.Get(LiveModeManager.GetDisplayName(uid)));
    }

    public static void OnPeerLeft(int uid)
    {
        intents.Remove(uid);
        if (!GameSession.IsRoomHost || committed || GameFlow.LocalScene != Scene.DayScene) return;
        DayDestinationStateMessage.Send(Round, Snapshot());
        TryConfirm();
    }

    public static void TryConfirm()
    {
        if (!GameSession.IsRoomHost || !GameSession.IsInRoom || committed || closing
            || GameFlow.LocalScene != Scene.DayScene) return;
        var target = GetIntent(PlayerManager.Local.Uid);
        if (target == DayDestination.None) return;
        if (GameFlow.Stage != GameStage.Day && !(businessReleased && GameFlow.Stage == GameStage.DayEnd)) return;
        if (GameSession.Room.Members.Any(p => p.Uid != PlayerManager.Local.Uid
            && p.Stage != GameStage.Day && !(businessReleased && p.Stage == GameStage.DayEnd))) return;
        if (!admissionClosed)
        {
            closing = true;
            PluginHost.Instance.StartManagedCoroutine(CloseAdmission(generation, GameSession.RoomMembershipId));
            return;
        }
        foreach (var uid in PlayerManager.Peers.Keys)
        {
            var peerTarget = GetIntent(uid);
            if (peerTarget == DayDestination.None) return;
            if (target == DayDestination.Business || peerTarget == DayDestination.Business)
            {
                if (target != peerTarget) return;
            }
            else if (peerTarget == DayDestination.FinalTrial)
            {
                target = DayDestination.FinalTrial;
            }
        }
        // 在发送确认前锁定，后续改选不能改变本轮结果。
        committed = true;
        DayDestinationConfirmMessage.Send(Round, target);
        ApplyConfirmation(Round, target);
    }

    public static void WithdrawLocal()
    {
        if (committed) return;
        generation++;
        continuations.Clear();
        localIntent = DayDestination.None;
        firstTrialPending = false;
        closing = false;
        admissionClosed = false;
        intents.Remove(PlayerManager.Local.Uid);
        if (GameSession.IsRoomHost) DayDestinationStateMessage.Send(Round, Snapshot());
        else if (GameSession.IsRoomClient) DayDestinationIntentMessage.Send(Round, DayDestination.None);
    }

    private static IEnumerator CloseAdmission(int entryGeneration, long membership)
    {
        var task = GameSession.SetJoinable(false);
        while (!task.IsCompleted) yield return null;
        if (entryGeneration != generation || membership != GameSession.RoomMembershipId) yield break;
        closing = false;
        if (!task.IsCompletedSuccessfully || !GameSession.IsRoomHost) yield break;
        admissionClosed = true;
        // 关闭确认之前接纳的新成员已经安装，重新检查全员意向。
        TryConfirm();
    }

    public static void ApplyConfirmation(int round, DayDestination destination)
    {
        if (round != Round) return;
        if (!continuations.TryGetValue(destination, out var continuation))
        {
            if (destination == DayDestination.FinalTrial && continuations.ContainsKey(DayDestination.FinalTrialAgain))
                continuation = RunTimeSchedulerPatch.EnterFirstTrialAsGuest;
            else
            {
                Log.Error($"Missing local entry for destination {destination}, round {round}");
                return;
            }
        }
        committed = true;
        GameFlow.BeginCooperative(destination);
        Round++;
        localIntent = DayDestination.None;
        firstTrialPending = destination == DayDestination.FinalTrial;
        intents.Clear();
        continuations.Clear();
        if (destination == DayDestination.Business)
        {
            businessReleased = true;
            PlayerManager.LocalIsDayOver = true;
            foreach (var peer in PlayerManager.Peers.Values) peer.IsDayOver = true;
        }
        Log.Info($"Confirmed day destination {destination}, round {round}");
        var notice = destination switch
        {
            DayDestination.Business => TextId.DestinationBusinessConfirmed,
            DayDestination.FinalTrial => TextId.DestinationFinalTrialConfirmed,
            _ => TextId.DestinationFinalTrialAgainConfirmed,
        };
        InGameConsole.ShowPassive(notice.Get());
        PluginHost.Instance.StartManagedCoroutine(ContinueEntry(continuation, generation, destination != DayDestination.Business));
    }

    public static void FinishDayEnd(Il2CppSystem.Action continuation)
    {
        if (!firstTrialPending)
        {
            continuation?.Invoke();
            return;
        }
        PluginHost.Instance.StartManagedCoroutine(WaitForDayEnd(continuation, generation));
    }

    private static IEnumerator WaitForDayEnd(Il2CppSystem.Action continuation, int entryGeneration)
    {
        while (firstTrialPending)
        {
            if (generation != entryGeneration) yield break;
            yield return null;
        }
        // 挑战已接管并离开白天时，旧白天的选店流程不能继续。
        if (generation == entryGeneration && GameFlow.LocalScene == Scene.DayScene) continuation?.Invoke();
    }

    private static IEnumerator ContinueEntry(System.Action continuation, int entryGeneration, bool challenge)
    {
        // 不在确认按钮或剧情奖励的调用栈内递归切场景。
        yield return null;
        while (GameFlow.InStory)
        {
            if (generation != entryGeneration || !GameSession.IsInRoom) yield break;
            yield return null;
        }
        if (generation != entryGeneration || !GameSession.IsInRoom || GameFlow.LocalScene != Scene.DayScene) yield break;
        if (challenge) SgrYuki.Utils.Panel.CloseActivePanelsBeforeSceneTransit();
        ReplayingChallenge = challenge;
        ReplayingBusiness = !challenge;
        try
        {
            continuation();
        }
        finally
        {
            ReplayingChallenge = false;
            ReplayingBusiness = false;
        }
    }
}
