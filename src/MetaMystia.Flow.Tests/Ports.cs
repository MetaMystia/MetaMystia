extern alias GameInterop;

global using Scene = GameInterop::Common.UI.Scene;
global using IzakayaLevel = GameInterop::Common.UI.IzakayaLevel;

using System.Collections;

using Common.UI;

using MetaMystia.Network;

// 仅替代游戏与传输入口；入口协调和营业等待直接编译模组源码。
namespace MetaMystia
{
    sealed class AutoLogAttribute : Attribute { }
    public static partial class DayDestinationManager
    {
        static class Log
        {
            public static void Info(string message) { }
            public static void Error(string message) => throw new Exception(message);
        }
    }
    public static partial class GameFlow
    {
        public static Scene LocalScene = Scene.DayScene;
        public static bool InStory;
        public static DayDestination Destination;
        public static GameStage Stage => LocalScene == Scene.WorkScene ? GameStage.Work
            : LocalScene != Scene.DayScene ? GameStage.Loading
            : Destination == DayDestination.None ? GameStage.Day : GameStage.DayEnd;
        public static void BeginCooperative(DayDestination destination) => Destination = destination;
        public static void ResetGameplay()
        {
            Destination = DayDestination.None;
            BusinessStart.Reset();
            DayDestinationManager.Reset();
        }
    }
    sealed class FlowPlayer
    {
        public int Uid;
        public bool IsDayOver;
        public bool IsPrepOver;
        public MapLabel IzakayaMapLabel;
        public int IzakayaLevel;
    }
    static class PlayerManager
    {
        public static FlowPlayer Local = new() { Uid = 1 };
        public static Dictionary<int, FlowPlayer> Peers = new();
        public static bool LocalIsDayOver;
        public static bool LocalIsPrepOver { get => Local.IsPrepOver; set => Local.IsPrepOver = value; }
        public static System.Collections.Generic.HashSet<int> AvailableRecipes = new() { 1, 2, 3 };
        public static bool RecipeAvailable(int id) => AvailableRecipes.Contains(id);
        public static bool BeverageAvailable(int id) => true;
        public static bool CookerAvailable(int id) => true;
        public static void ResetState() => Local.IsPrepOver = false;
        public static bool AllPeersSelectedSameIzakaya(MapLabel map, int level) =>
            Peers.Values.All(peer => peer.IzakayaMapLabel == map && peer.IzakayaLevel == level);
        public static string GetFirstMismatchSelection(MapLabel map, int level) => "mismatch";
    }
    /// <summary>备菜阶段协调的端口：只保留 PrepSync 使用到的成员。</summary>
    static class PrepSceneManager
    {
        public static bool IsOpeningPanel { get; set; }
        public static bool CanSyncEdits { get; set; }
        public static bool IsYuyukoChallenge => false;
        public static bool IsYuyukoPrepActive => false;
        public static int BeginPrepCount { get; private set; }
        public static int UpdateGroupsCount { get; private set; }
        public static int UpdateUICount { get; private set; }
        public static void Reset()
        {
            IsOpeningPanel = false;
            CanSyncEdits = false;
            BeginPrepCount = 0;
            UpdateGroupsCount = 0;
            UpdateUICount = 0;
        }
        public static void BeginPrep() => BeginPrepCount++;
        public static void TryBeginYuyukoPrep() { }
        public static void EndYuyukoPrep() { }
        public static void TryCompletePrep() { }
        public static void UpdateGroups() => UpdateGroupsCount++;
        public static void UpdateCookers() { }
        public static void UpdateUI() => UpdateUICount++;
    }
    sealed class ConfigEntry<T>
    {
        public T Value;
    }
    static class ConfigManager
    {
        public static ConfigEntry<float> CheatFlowRate = new() { Value = 1f };
    }
    static class LiveModeManager
    {
        public static bool IsActive;
        public static string GetDisplayName(int uid, string fallback = null) => IsActive ? $"UID-{uid}" : fallback ?? uid.ToString();
    }
    sealed class PluginHost
    {
        public static readonly PluginHost Instance = new();
        readonly List<IEnumerator> routines = new();
        public void StartManagedCoroutine(IEnumerator routine)
        {
            if (routine.MoveNext()) routines.Add(routine);
        }
        public void Tick()
        {
            Multiplayer.GameSession.DispatchAdmission();
            foreach (var routine in routines.ToArray())
                if (!routine.MoveNext()) routines.Remove(routine);
        }
        public void Clear() => routines.Clear();
    }
}
namespace MetaMystia.Multiplayer
{
    static class GameSession
    {
        public static object Client = new();
        public static long RoomMembershipId = 1;
        public static Room Room = new();
        public static bool IsInRoom = true;
        public static bool IsConnecting;
        public static Snapshot State = new();
        public static int Leaves, Stops;
        public static bool Resumed;
        public static void LeaveRoom(bool resumeGameplay = true)
        { Leaves++; Resumed = resumeGameplay; IsInRoom = false; }
        public static void Stop(bool resumeGameplay = true)
        { Stops++; Resumed = resumeGameplay; IsConnecting = false; IsInRoom = false; }
        public static bool IsRoomHost = true;
        public static bool IsRoomClient => IsInRoom && !IsRoomHost;
        public static bool HasRoomPeers => IsInRoom && PlayerManager.Peers.Count > 0;
        public static Task Admission = Task.CompletedTask;
        public static System.Action<bool> AdmissionCompleted;
        public static Task SetJoinable(bool allowed, System.Action<bool> completed = null)
        {
            AdmissionCompleted = completed;
            DispatchAdmission();
            return Admission;
        }
        public static void DispatchAdmission()
        {
            if (!Admission.IsCompleted) return;
            var completed = AdmissionCompleted;
            AdmissionCompleted = null;
            completed?.Invoke(Admission.IsCompletedSuccessfully);
        }
    }
}
namespace MetaMystia.Multiplayer.Messages
{
    static class DayDestinationConfirmMessage
    {
        public static readonly List<(int Round, DayDestination Target)> Sent = new();
        public static void Send(int round, DayDestination destination) => Sent.Add((round, destination));
    }
    static class DayDestinationIntentMessage
    {
        public static void Send(int round, DayDestination destination) { }
    }
    static class DayDestinationStateMessage
    {
        public static void Send(int round, Dictionary<int, DayDestination> state) { }
    }
    static class BusinessStartMessage
    {
        public static readonly List<bool> Sent = new();
        public static void Send(bool start) => Sent.Add(start);
    }
}
namespace MetaMystia.Listeners
{
    static class ScheduleSync
    {
        public static int FirstTrialEntries;
        public static void ResetFirstTrialGuest() => FirstTrialEntries = 0;
        public static void EnterFirstTrialAsGuest() => FirstTrialEntries++;
    }
}
namespace MetaMystia.UI
{
    enum TextId
    {
        DestinationBusiness, DestinationFinalTrial, DestinationFinalTrialAgain,
        DestinationBusinessReady, DestinationFinalTrialReady, DestinationFinalTrialAgainReady,
        DestinationBusinessConfirmed, DestinationFinalTrialConfirmed, DestinationFinalTrialAgainConfirmed,
        WaitingForHostConfirm, SelectedIzakaya, SelectedIzakayaMismatch,
        MystiaReadyForWork, DLCPeerRecipeNotAvailable, DLCPeerBeverageNotAvailable, DLCPeerCookerNotAvailable,
        CheatFlowRateActive,
        NetworkFlowInterrupted, MultiplayerDisconnected,
        PeerConnected, PeerDisconnected, PeerJoined, PeerLeft, NetworkRoomLeft,
    }
    static class Text
    {
        public static string Get(this TextId text, params object[] args) => text is
            TextId.PeerConnected or TextId.PeerDisconnected or TextId.PeerJoined or TextId.PeerLeft or TextId.NetworkRoomLeft
            ? $"{text}: {string.Join(", ", args)}" : text.ToString();
    }
    static class InGameConsole
    {
        public static readonly List<string> Messages = new();
        public static void ShowPassive(string text) => Messages.Add(text);
        public static void ShowPassiveFromAnyThread(string text) => Messages.Add(text);
    }
}
namespace SgrYuki.Utils
{
    static class Panel
    {
        public static void CloseActivePanelsBeforeSceneTransit() { }
        public static void ClosePanelUntil(string name, string[] exclude) { }
    }
}
