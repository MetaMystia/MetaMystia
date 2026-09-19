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
    }
    sealed class FlowPlayer
    {
        public int Uid;
        public bool IsDayOver;
    }
    static class PlayerManager
    {
        public static FlowPlayer Local = new() { Uid = 1 };
        public static Dictionary<int, FlowPlayer> Peers = new();
        public static bool LocalIsDayOver;
    }
    static class LiveModeManager
    {
        public static string GetDisplayName(int uid) => uid.ToString();
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
        public static bool IsRoomHost = true;
        public static bool IsRoomClient => IsInRoom && !IsRoomHost;
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
namespace MetaMystia.Patch
{
    static class RunTimeSchedulerPatch
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
    }
    static class Text
    {
        public static string Get(this TextId text, params object[] args) => text.ToString();
    }
    static class InGameConsole
    {
        public static void ShowPassive(string text) { }
    }
}
namespace SgrYuki.Utils
{
    static class Panel
    {
        public static void CloseActivePanelsBeforeSceneTransit() { }
    }
}
